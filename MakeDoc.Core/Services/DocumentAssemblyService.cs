using System.IO.Compression;
using System.Xml.Linq;

namespace MakeDoc.Core.Services
{
	/// <summary>
	/// Stitches a sequence of DOCX byte arrays into a single assembled DOCX.
	///
	/// Algorithm:
	///   - The first blob is treated as the template (its styles, numbering,
	///     theme, headers/footers, and sectPr are preserved as-is).
	///   - For each subsequent blob, the body's child elements are appended
	///     directly to the template's body. No separators (page breaks,
	///     horizontal rules, blank paragraphs) are injected by code — any
	///     section/clause boundary formatting must live in the source DOCX
	///     for each node. This mirrors the Document Builder convention and
	///     keeps formatting decisions in the hands of document authors.
	///   - The template's trailing sectPr is moved to the very end so the
	///     final document still has well-formed section properties.
	///
	/// Limitations (acceptable when all inputs share the same Word template,
	/// which is the case for clauses uploaded by UploadNodes.R from the
	/// procurement template family):
	///   - styles.xml, numbering.xml, theme.xml are NOT merged across inputs.
	///     Inputs that reference style or numbering IDs not present in the
	///     template will render with default styling.
	///   - word/_rels/document.xml.rels is NOT merged. Inputs containing
	///     hyperlinks, images, footnotes, or embedded objects will produce
	///     dangling references unless those rels happen to exist in the
	///     template.
	///
	/// If those limitations bite later, the cleanest upgrade path is to
	/// replace this service with one built on OpenXmlPowerTools.DocumentBuilder
	/// (DocumentFormat.OpenXml is already a project dependency).
	/// </summary>
	public class DocumentAssemblyService
	{
		private static readonly XNamespace W =
			"http://schemas.openxmlformats.org/wordprocessingml/2006/main";

		/// <summary>
		/// Assembles the supplied DOCX blobs into a single DOCX, returned
		/// as a byte array. Order of <paramref name="docxBlobs"/> determines
		/// the order of content in the assembled output.
		/// </summary>
		public byte[] Assemble(IList<byte[]> docxBlobs)
		{
			ArgumentNullException.ThrowIfNull(docxBlobs);
			if (docxBlobs.Count == 0)
				throw new ArgumentException(
					"At least one DOCX blob is required.", nameof(docxBlobs));

			// All temp scratch lives under a single dir we can wipe on exit.
			string workDir = Path.Combine(
				Path.GetTempPath(),
				"MakeDocAssembly_" + Guid.NewGuid().ToString("N"));
			string templateDir = Path.Combine(workDir, "template");
			Directory.CreateDirectory(templateDir);

			try
			{
				// 1. Extract the first blob as our working template.
				string templateZipPath = Path.Combine(workDir, "template.docx");
				File.WriteAllBytes(templateZipPath, docxBlobs[0]);
				ZipFile.ExtractToDirectory(templateZipPath, templateDir);

				string mainDocXmlPath = Path.Combine(templateDir, "word", "document.xml");
				if (!File.Exists(mainDocXmlPath))
					throw new InvalidDataException(
						"document.xml not found inside the first DOCX blob; " +
						"input does not look like a valid Word file.");

				XDocument mainDoc = XDocument.Load(mainDocXmlPath);
				XElement body = mainDoc.Descendants(W + "body").First();

				// Pull the trailing sectPr aside; we'll re-attach at the very end.
				XElement? trailingSectPr = body.Elements(W + "sectPr").LastOrDefault();
				trailingSectPr?.Remove();

				// 2. For each subsequent blob, copy its body elements in.
				for (int i = 1; i < docxBlobs.Count; i++)
				{
					AppendBodyFromBlob(body, docxBlobs[i]);
				}

				// 3. Re-attach the trailing sectPr.
				if (trailingSectPr != null)
					body.Add(trailingSectPr);

				// 4. Save document.xml back into the extracted template, then re-zip.
				mainDoc.Save(mainDocXmlPath);

				string outputPath = Path.Combine(workDir, "assembled.docx");
				ZipFile.CreateFromDirectory(
					templateDir,
					outputPath,
					CompressionLevel.Fastest,
					includeBaseDirectory: false);

				return File.ReadAllBytes(outputPath);
			}
			finally
			{
				// Best-effort cleanup; never throw out of finally.
				if (Directory.Exists(workDir))
				{
					try { Directory.Delete(workDir, recursive: true); }
					catch { /* leave the temp dir behind rather than crash */ }
				}
			}
		}

		// Reads the body of one DOCX blob and appends its top-level body
		// elements (minus any sectPr) to <paramref name="targetBody"/>.
		// No separator is injected — boundary formatting is the
		// responsibility of the source DOCX for each node.
		private static void AppendBodyFromBlob(XElement targetBody, byte[] docxBlob)
		{
			using var ms = new MemoryStream(docxBlob, writable: false);
			using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

			ZipArchiveEntry? entry = archive.GetEntry("word/document.xml");
			if (entry == null)
				return;  // skip malformed input rather than fail the whole assembly

			XDocument fragmentDoc;
			using (Stream entryStream = entry.Open())
				fragmentDoc = XDocument.Load(entryStream);

			XElement fragmentBody = fragmentDoc.Descendants(W + "body").First();

			// sectPr inside an appended fragment would force a section break
			// with that fragment's page setup — drop them so the template's
			// section properties win.
			fragmentBody.Elements(W + "sectPr").Remove();

			foreach (XElement element in fragmentBody.Elements())
				targetBody.Add(new XElement(element));
		}
	}
}
