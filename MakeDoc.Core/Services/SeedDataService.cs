using MakeDoc.Core.Data;
using Microsoft.Data.Sqlite;
using System.Text;

namespace MakeDoc.Core.Services
{
	public class SeedDataService
	{
		private readonly MakDocDb _db;
		private readonly string _csvDirectory;

		// ─────────────────────────────────────────────────────────────
		// Constructor — csvDirectory is the path to the db/ folder
		// containing the seed CSV files.
		// ─────────────────────────────────────────────────────────────
		public SeedDataService(MakDocDb db, string csvDirectory)
		{
			_db = db;
			_csvDirectory = csvDirectory;
		}

		// ─────────────────────────────────────────────────────────────
		// SEED ALL — checks each table and seeds if empty
		// Safe to call on every startup.
		// ─────────────────────────────────────────────────────────────
		public void SeedIfEmpty()
		{
			if (IsTableEmpty("DocType"))
				SeedDocTypes();

			if (IsTableEmpty("NodeHierarchy"))
				SeedNodeHierarchy();

			if (IsTableEmpty("Node"))
				SeedNodes();
		}

		// ─────────────────────────────────────────────────────────────
		// SEED DOCTYPES from seed_Doctype_table.csv
		// Columns: DocTypeID, Name, InclusionTags, HeaderNodeID
		// ─────────────────────────────────────────────────────────────
		public void SeedDocTypes()
		{
			string csvPath = Path.Combine(_csvDirectory, "seed_Doctype_table.csv");

			if (!File.Exists(csvPath))
				throw new FileNotFoundException(
					$"DocType seed file not found: {csvPath}");

			var rows = ReadCsv(csvPath);

			using var connection = _db.OpenConnection();
			using var transaction = connection.BeginTransaction();

			try
			{
				foreach (var row in rows)
				{
					using var cmd = connection.CreateCommand();
					cmd.Transaction = transaction;
					cmd.CommandText = @"
                        INSERT OR IGNORE INTO DocType 
                            (DocTypeID, Name, InclusionTags, HeaderNodeID)
                        VALUES 
                            (@docTypeId, @name, @tags, @headerNodeId)";

					cmd.Parameters.AddWithValue("@docTypeId",
						GetColumn(row, "DocTypeID"));
					cmd.Parameters.AddWithValue("@name",
						GetColumn(row, "Name"));
					cmd.Parameters.AddWithValue("@tags",
						GetColumnOrNull(row, "InclusionTags"));
					cmd.Parameters.AddWithValue("@headerNodeId",
						GetColumnOrNull(row, "HeaderNodeID"));

					cmd.ExecuteNonQuery();
				}

				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
		}

		// ─────────────────────────────────────────────────────────────
		// SEED NODE HIERARCHY from seed_NodeHierarchy_table.csv
		// Columns: ParentNodeID, ChildNodeID, DocTypeID, Sequence
		// ─────────────────────────────────────────────────────────────
		public void SeedNodeHierarchy()
		{
			string csvPath = Path.Combine(_csvDirectory,
				"seed_NodeHierarchy_table.csv");

			if (!File.Exists(csvPath))
				throw new FileNotFoundException(
					$"NodeHierarchy seed file not found: {csvPath}");

			var rows = ReadCsv(csvPath);

			using var connection = _db.OpenConnection();
			using var transaction = connection.BeginTransaction();

			try
			{
				foreach (var row in rows)
				{
					using var cmd = connection.CreateCommand();
					cmd.Transaction = transaction;
					cmd.CommandText = @"
                        INSERT OR IGNORE INTO NodeHierarchy 
                            (ParentNodeID, ChildNodeID, DocTypeID, Sequence)
                        VALUES 
                            (@parentId, @childId, @docTypeId, @seq)";

					cmd.Parameters.AddWithValue("@parentId",
						GetColumn(row, "ParentNodeID"));
					cmd.Parameters.AddWithValue("@childId",
						GetColumn(row, "ChildNodeID"));
					cmd.Parameters.AddWithValue("@docTypeId",
						GetColumn(row, "DocTypeID"));
					cmd.Parameters.AddWithValue("@seq",
						int.Parse(GetColumn(row, "Sequence")));

					cmd.ExecuteNonQuery();
				}

				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
		}

		// ─────────────────────────────────────────────────────────────
		// SEED NODES from seed_Node_table.csv
		// Columns: NodeID, NodeType, Title, Sequence
		// Note: Content (DOCX blob) is NOT in the CSV —
		// it is loaded separately via the Admin form.
		// ─────────────────────────────────────────────────────────────
		public void SeedNodes()
		{
			string csvPath = Path.Combine(_csvDirectory, "seed_Node_table.csv");

			if (!File.Exists(csvPath))
				throw new FileNotFoundException(
					$"Node seed file not found: {csvPath}");

			var rows = ReadCsv(csvPath);

			using var connection = _db.OpenConnection();
			using var transaction = connection.BeginTransaction();

			try
			{
				foreach (var row in rows)
				{
					using var cmd = connection.CreateCommand();
					cmd.Transaction = transaction;
					cmd.CommandText = @"
                        INSERT OR IGNORE INTO Node 
                            (NodeID, NodeType, Title, Sequence, 
                             IsActive, CreatedDate)
                        VALUES 
                            (@nodeId, @nodeType, @title, @seq,
                             1, @created)";

					cmd.Parameters.AddWithValue("@nodeId",
						GetColumn(row, "NodeID"));
					cmd.Parameters.AddWithValue("@nodeType",
						GetColumnOrNull(row, "NodeType"));
					cmd.Parameters.AddWithValue("@title",
						GetColumnOrNull(row, "Title"));
					cmd.Parameters.AddWithValue("@seq",
						int.TryParse(GetColumn(row, "Sequence"),
							out int seq) ? seq : 0);
					cmd.Parameters.AddWithValue("@created",
						DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"));

					cmd.ExecuteNonQuery();
				}

				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
		}

		// ─────────────────────────────────────────────────────────────
		// UPDATE NODE METADATA
		// Patches NodeType, Title, Sequence for existing nodes
		// without touching the Content blob.
		// Useful when the CSV is updated with missing metadata.
		// ─────────────────────────────────────────────────────────────
		public void UpdateNodeMetadata()
		{
			string csvPath = Path.Combine(_csvDirectory, "seed_Node_table.csv");

			if (!File.Exists(csvPath))
				throw new FileNotFoundException(
					$"Node seed file not found: {csvPath}");

			var rows = ReadCsv(csvPath);

			using var connection = _db.OpenConnection();
			using var transaction = connection.BeginTransaction();

			try
			{
				foreach (var row in rows)
				{
					using var cmd = connection.CreateCommand();
					cmd.Transaction = transaction;
					cmd.CommandText = @"
                        UPDATE Node SET
                            NodeType     = @nodeType,
                            Title        = @title,
                            Sequence     = @seq,
                            ModifiedDate = @modified
                        WHERE NodeID = @nodeId";

					cmd.Parameters.AddWithValue("@nodeId",
						GetColumn(row, "NodeID"));
					cmd.Parameters.AddWithValue("@nodeType",
						GetColumnOrNull(row, "NodeType"));
					cmd.Parameters.AddWithValue("@title",
						GetColumnOrNull(row, "Title"));
					cmd.Parameters.AddWithValue("@seq",
						int.TryParse(GetColumn(row, "Sequence"),
							out int seq) ? seq : 0);
					cmd.Parameters.AddWithValue("@modified",
						DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"));

					cmd.ExecuteNonQuery();
				}

				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
		}

		// ─────────────────────────────────────────────────────────────
		// HELPERS
		// ─────────────────────────────────────────────────────────────

		private bool IsTableEmpty(string tableName)
		{
			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = $"SELECT COUNT(*) FROM {tableName}";
			var result = cmd.ExecuteScalar();
			return Convert.ToInt64(result) == 0;
		}

		// Reads a CSV file into a list of dictionaries (column -> value).
		// Handles quoted fields containing commas.
		private static List<Dictionary<string, string>> ReadCsv(string path)
		{
			var rows = new List<Dictionary<string, string>>();
			var lines = File.ReadAllLines(path);

			if (lines.Length < 2) return rows;

			var headers = ParseCsvLine(lines[0]);

			for (int i = 1; i < lines.Length; i++)
			{
				if (string.IsNullOrWhiteSpace(lines[i])) continue;

				var values = ParseCsvLine(lines[i]);
				var row = new Dictionary<string, string>(
					StringComparer.OrdinalIgnoreCase);

				for (int j = 0; j < headers.Count && j < values.Count; j++)
					row[headers[j]] = values[j];

				rows.Add(row);
			}

			return rows;
		}

		// Parses a single CSV line respecting quoted fields.
		private static List<string> ParseCsvLine(string line)
		{
			var fields = new List<string>();
			var current = new StringBuilder();
			bool inQuotes = false;

			for (int i = 0; i < line.Length; i++)
			{
				char c = line[i];

				if (c == '"')
				{
					// Handle escaped quotes ("")
					if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
					{
						current.Append('"');
						i++;
					}
					else
					{
						inQuotes = !inQuotes;
					}
				}
				else if (c == ',' && !inQuotes)
				{
					fields.Add(current.ToString().Trim());
					current.Clear();
				}
				else
				{
					current.Append(c);
				}
			}

			fields.Add(current.ToString().Trim());
			return fields;
		}

		private static string GetColumn(
			Dictionary<string, string> row, string column)
		{
			return row.TryGetValue(column, out var value) ? value : string.Empty;
		}

		private static object GetColumnOrNull(
			Dictionary<string, string> row, string column)
		{
			if (row.TryGetValue(column, out var value)
				&& !string.IsNullOrWhiteSpace(value))
				return value;
			return DBNull.Value;
		}
	}
}