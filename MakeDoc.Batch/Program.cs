using System.Diagnostics;
using System.Text.Json;
using MakeDoc.Core.Data;
using MakeDoc.Core.Models;
using MakeDoc.Core.Services;

namespace MakeDoc.Batch
{
	/// <summary>
	/// Headless batch document generator for MAKEDOC.
	///
	/// Reuses the exact assembly pipeline from AssemblyForm.OnGenerateClicked
	/// (create-new-document.md), minus all UI and minus DB writes:
	///   - The database is opened READ-ONLY in spirit: no Instance rows, no
	///     UC- nodes, and no line-item re-keying are written.
	///   - Fill-in values are synthesized per run; if a pools JSON file is
	///     supplied (or found next to the exe), values are sampled from it.
	///
	/// Usage:
	///   MakeDoc.Batch --count 1000 --out C:\temp\assembled_documents
	///                 [--db C:\Users\skeye\BOOK2\MAKEDOC\db]
	///                 [--pools fillin-pools.json] [--seed 42]
	///                 [--doctype DT-0001 --doctype DT-0002]
	///
	/// MAKEDOC_DB resolution: --db wins; otherwise the existing MAKEDOC_DB
	/// environment variable is used.
	///
	/// Outputs:
	///   <out>\*.docx           one document per successful run
	///   <out>\manifest.jsonl   one JSON line per run (doctype, nodes, fill-ins)
	/// </summary>
	internal static class Program
	{
		static int Main(string[] args)
		{
			// ── Parse arguments ───────────────────────────────────────
			int      count    = 10;
			string?  outDir   = null;
			string?  dbDir    = null;
			string?  poolPath = null;
			int?     seed     = null;
			var      docTypeFilter = new List<string>();

			for (int i = 0; i < args.Length; i++)
			{
				switch (args[i])
				{
					case "--count":   count    = int.Parse(args[++i]); break;
					case "--out":     outDir   = args[++i];            break;
					case "--db":      dbDir    = args[++i];            break;
					case "--pools":   poolPath = args[++i];            break;
					case "--seed":    seed     = int.Parse(args[++i]); break;
					case "--doctype": docTypeFilter.Add(args[++i]);    break;
					case "--help": case "-h": case "/?":
						PrintUsage(); return 0;
					default:
						Console.Error.WriteLine($"Unknown argument: {args[i]}");
						PrintUsage(); return 2;
				}
			}

			if (string.IsNullOrWhiteSpace(outDir))
			{
				Console.Error.WriteLine("Required: --out <directory>");
				PrintUsage(); return 2;
			}

			if (!string.IsNullOrWhiteSpace(dbDir))
				Environment.SetEnvironmentVariable("MAKEDOC_DB", dbDir);

			Directory.CreateDirectory(outDir);

			var rng = seed.HasValue ? new Random(seed.Value) : new Random();

			// ── Optional fill-in value pools ──────────────────────────
			// Format: { "variable_name": ["value 1", "value 2", ...], ... }
			Dictionary<string, string[]> pools = LoadPools(poolPath);

			// ── Services (same wiring as AssemblyForm) ────────────────
			MakDocDb             db;
			NodeService          nodeService;
			NodeHierarchyService hierarchyService;
			var                  assemblyService = new DocumentAssemblyService();
			var                  fillinService   = new FillinService();

			try
			{
				db               = new MakDocDb();
				nodeService      = new NodeService(db);
				hierarchyService = new NodeHierarchyService(db);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"Database error: {ex.Message}");
				return 1;
			}

			// ── Eligible doc types ────────────────────────────────────
			var docTypes = db.GetAllDocTypes()
				.Where(dt => !string.IsNullOrWhiteSpace(dt.HeaderNodeID))
				.Where(dt => docTypeFilter.Count == 0
				          || docTypeFilter.Contains(dt.DocTypeID))
				.ToList();

			if (docTypes.Count == 0)
			{
				Console.Error.WriteLine(
					"No eligible document types (need a configured HeaderNodeID" +
					(docTypeFilter.Count > 0 ? " and a matching --doctype filter)." : ")."));
				return 1;
			}

			Console.WriteLine(
				$"MAKEDOC batch: {count} run(s), {docTypes.Count} doc type(s), out: {outDir}");

			// ── Run loop ──────────────────────────────────────────────
			var sw = Stopwatch.StartNew();
			int ok = 0, failed = 0;

			using var manifest = new StreamWriter(
				Path.Combine(outDir, "manifest.jsonl"), append: true);

			for (int run = 1; run <= count; run++)
			{
				var docType = docTypes[rng.Next(docTypes.Count)];
				try
				{
					string file = GenerateOne(
						run, docType, outDir, rng, pools,
						db, nodeService, hierarchyService,
						fillinService, assemblyService, manifest);

					ok++;
					Console.WriteLine($"[{run}/{count}] OK    {Path.GetFileName(file)}");
				}
				catch (Exception ex)
				{
					failed++;
					Console.Error.WriteLine(
						$"[{run}/{count}] FAIL  {docType.Name}: {ex.Message}");
				}
			}

			sw.Stop();
			Console.WriteLine(
				$"Done: {ok} succeeded, {failed} failed in {sw.Elapsed:hh\\:mm\\:ss}.");
			return failed == 0 ? 0 : 1;
		}

		// ─────────────────────────────────────────────────────────────
		// Single document generation — mirrors AssemblyForm steps 1, 5, 6.
		// Steps 2-4 (FillinForm, UC- nodes, Instance persist) are replaced
		// by synthetic fill-in values and no DB writes.
		// ─────────────────────────────────────────────────────────────
		private static string GenerateOne(
			int run, DocType docType, string outDir, Random rng,
			Dictionary<string, string[]> pools,
			MakDocDb db, NodeService nodeService,
			NodeHierarchyService hierarchyService,
			FillinService fillinService,
			DocumentAssemblyService assemblyService,
			StreamWriter manifest)
		{
			// Canonical node list for this doc type.
			var orderedIds = hierarchyService.GetOrderedNodeIds(
				docType.DocTypeID, docType.HeaderNodeID!);

			if (orderedIds.Count == 0)
				throw new InvalidOperationException("Doc type has no nodes.");

			// Resolve nodes + blobs once.
			var entries = new List<(string Id, string NodeType, byte[] Blob)>();
			foreach (var id in orderedIds)
			{
				var node = nodeService.GetById(id);
				if (node?.Content is { Length: > 0 } blob)
					entries.Add((id, node.NodeType ?? NodeTypes.Clause, blob));
			}

			if (entries.Count == 0)
				throw new InvalidOperationException("No content blobs available.");

			// Step 1 — scan clause AND header-node blobs for fill-ins.
			var scanBlobs = entries
				.Where(e => e.NodeType is NodeTypes.Clause or NodeTypes.HeaderNode)
				.Select(e => e.Blob);
			var fillinNames = fillinService.ScanFillins(scanBlobs);

			// Step 2 (replaced) — synthesize values, sampling pools when present.
			var fillinValues = fillinNames.ToDictionary(
				name => name,
				name => MakeValue(name, run, rng, pools));

			// Line items staged against the doc type — READ ONLY (the UI
			// re-keys them to the instance; batch must not consume staging).
			var lineItems = db.GetLineItemsForDocType(docType.DocTypeID).AsReadOnly();

			// Step 5 — substitute and assemble.
			var assemblyBlobs = new List<byte[]>();
			int secNo = 0;

			foreach (var (id, nodeType, original) in entries)
			{
				byte[] blob = original;

				if (nodeType == NodeTypes.Clause)
				{
					secNo++;
					if (fillinValues.Count > 0)
						blob = fillinService.SubstituteFillins(blob, fillinValues);
					blob = fillinService.SubstitutePlainText(
						blob, "{SecNo}", secNo.ToString());
					blob = fillinService.SubstituteLineItemTable(blob, lineItems);
				}
				else if (nodeType == NodeTypes.HeaderNode && fillinValues.Count > 0)
				{
					blob = fillinService.SubstituteFillins(blob, fillinValues);
				}

				assemblyBlobs.Add(blob);
			}

			byte[] assembled = assemblyService.Assemble(assemblyBlobs);

			// Step 6 — write file (no dialog; unique deterministic-ish name).
			string fileName = SanitizeFilename(
				$"{docType.Name}_{run:D4}_{Guid.NewGuid().ToString("N")[..8]}.docx");
			string filePath = Path.Combine(outDir, fileName);
			File.WriteAllBytes(filePath, assembled);

			// Manifest line for downstream document analysis.
			manifest.WriteLine(JsonSerializer.Serialize(new
			{
				run,
				file      = fileName,
				docTypeId = docType.DocTypeID,
				docType   = docType.Name,
				tier      = docType.Tier,
				nodes     = entries.Select(e => e.Id),
				fillins   = fillinValues,
				generated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")
			}));
			manifest.Flush();

			return filePath;
		}

		// ─────────────────────────────────────────────────────────────
		// Fill-in value synthesis
		// ─────────────────────────────────────────────────────────────
		private static readonly string[] CompanyNames =
		{
			"Acme Industrial Supply", "Northwind Logistics", "Cascade Fabrication",
			"Bluepine Services Group", "Irongate Manufacturing", "Veridian Systems",
			"Summit Ridge Contractors", "Harbor Point Solutions"
		};

		private static string MakeValue(
			string name, int run, Random rng, Dictionary<string, string[]> pools)
		{
			// Pool value wins when the variable has one.
			if (pools.TryGetValue(name, out var pool) && pool.Length > 0)
				return pool[rng.Next(pool.Length)];

			string n = name.ToLowerInvariant();

			if (n.Contains("date"))
				return DateTime.Today.AddDays(rng.Next(-180, 366)).ToString("MM/dd/yyyy");
			if (n.Contains("day"))
				return rng.Next(5, 121).ToString();
			if (n.Contains("amount") || n.Contains("price") || n.Contains("cost") || n.Contains("value"))
				return (rng.Next(1_000, 250_000) + rng.Next(0, 100) / 100m).ToString("F2");
			if (n.Contains("qty") || n.Contains("quantity") || n.Contains("count") || n.Contains("number"))
				return rng.Next(1, 500).ToString();
			if (n.Contains("percent") || n.Contains("rate"))
				return rng.Next(1, 26).ToString();
			if (n.Contains("contractor") || n.Contains("vendor") || n.Contains("company") || n.Contains("name"))
				return CompanyNames[rng.Next(CompanyNames.Length)];
			if (n.Contains("email"))
				return $"contact{rng.Next(1, 99)}@example.com";
			if (n.Contains("phone"))
				return $"({rng.Next(200, 990)}) 555-{rng.Next(1000, 9999)}";

			return $"{name}_{run:D4}";
		}

		private static Dictionary<string, string[]> LoadPools(string? poolPath)
		{
			// Explicit path, or fillin-pools.json next to the executable / cwd.
			string? path = poolPath;
			if (path is null)
			{
				foreach (var candidate in new[]
				{
					Path.Combine(AppContext.BaseDirectory, "fillin-pools.json"),
					Path.Combine(Environment.CurrentDirectory, "fillin-pools.json")
				})
				{
					if (File.Exists(candidate)) { path = candidate; break; }
				}
			}

			if (path is null) return new Dictionary<string, string[]>();

			try
			{
				var pools = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
					File.ReadAllText(path));
				Console.WriteLine(
					$"Loaded fill-in pools for {pools?.Count ?? 0} variable(s) from {path}");
				return pools ?? new Dictionary<string, string[]>();
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"Warning: could not parse pools file {path}: {ex.Message}");
				return new Dictionary<string, string[]>();
			}
		}

		private static string SanitizeFilename(string name)
		{
			foreach (var c in Path.GetInvalidFileNameChars())
				name = name.Replace(c, '_');
			return name;
		}

		private static void PrintUsage()
		{
			Console.WriteLine("""
				MakeDoc.Batch — headless MAKEDOC document generator (no DB writes)

				  --count N          number of documents to generate (default 10)
				  --out DIR          output directory (required)
				  --db DIR           directory containing MAKEDOC.db
				                     (default: MAKEDOC_DB environment variable)
				  --pools FILE       fill-in value pools JSON:
				                     { "contractor_name": ["ABC Corp", "..."], ... }
				                     (auto-detected as fillin-pools.json if present)
				  --seed N           RNG seed for reproducible runs
				  --doctype ID       restrict to this DocTypeID (repeatable)

				Example:
				  dotnet run --project MakeDoc.Batch -- --count 1000 ^
				    --out C:\temp\assembled_documents --db C:\Users\skeye\BOOK2\MAKEDOC\db --seed 42
				""");
		}
	}
}
