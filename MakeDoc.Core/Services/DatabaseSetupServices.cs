using Microsoft.Data.Sqlite;

namespace MakeDoc.Core.Services
{
	public class DatabaseSetupService
	{
		private readonly string _connectionString;

		public DatabaseSetupService()
		{
			string? dbDir = Environment.GetEnvironmentVariable("MAKEDOC_DB");

			if (string.IsNullOrWhiteSpace(dbDir))
				throw new InvalidOperationException(
					"Environment variable MAKEDOC_DB is not set. " +
					"Set it to the directory path containing MAKEDOC.db.");

			string dbPath = Path.Combine(dbDir, "MAKEDOC.db");
			_connectionString = $"Data Source={dbPath}";
		}

		public void Initialize()
		{
			using var connection = new SqliteConnection(_connectionString);
			connection.Open();
			RunSchema(connection);
		}

		public string ConnectionString => _connectionString;

		private static void RunSchema(SqliteConnection connection)
		{
			var sql = @"
                PRAGMA foreign_keys = ON;
-- =============================================
-- DOCMAKE Database Schema 3.9.2026 - SQLite3
-- =============================================

-- Node Table (created first to avoid circular dependency)
CREATE TABLE IF NOT EXISTS Node (
    NodeID TEXT PRIMARY KEY,
    NodeType TEXT NOT NULL CHECK(NodeType IN ('Document', 'Section', 'Subsection', 'Clause', 'HeaderNode', 'Template')),
    Title TEXT NULL,
    Sequence INTEGER NOT NULL DEFAULT 0,
    Content BLOB NULL
);

-- NodeHierarchy Table
CREATE TABLE IF NOT EXISTS NodeHierarchy (
    ParentNodeID  TEXT NOT NULL,
    ChildNodeID   TEXT NOT NULL,
    DocTypeID     TEXT NOT NULL,   -- which document type this relationship belongs to
    Sequence      INTEGER NOT NULL DEFAULT 0,

    PRIMARY KEY (ParentNodeID, ChildNodeID, DocTypeID),
    FOREIGN KEY (ParentNodeID) REFERENCES Node(NodeID),
    FOREIGN KEY (ChildNodeID)  REFERENCES Node(NodeID),
    FOREIGN KEY (DocTypeID)    REFERENCES DocType(DocTypeID)
);

-- DocType Table
CREATE TABLE IF NOT EXISTS DocType (
    DocTypeID TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    InclusionTags TEXT, -- JSON array: [""DEI"", ""HAZMAT"", ""INTERNATIONAL""]
    HeaderNodeID TEXT NULL,
	TemplateBlobID TEXT NULL,
       
    FOREIGN KEY (HeaderNodeID) REFERENCES Node(NodeID),
	FOREIGN KEY (TemplateBlobID) REFERENCES Node(NodeID)
);

-- Instance Table (DocumentInstance)
CREATE TABLE IF NOT EXISTS Instance (
    InstanceID TEXT PRIMARY KEY,
    DocTypeID TEXT NOT NULL,
    PrevEditionID TEXT NULL,
    BuildFromID TEXT NULL,
    GeneratedDate TEXT DEFAULT (datetime('now')),
    IsArchived INTEGER DEFAULT 0, -- 0 = active, 1 = archived
    ArchiveDate TEXT NULL,
        
    -- JSON data columns
    InclusionData TEXT, -- JSON: {""tags"": [""DEI"", ""HAZMAT""], ""tier"": ""standard""}
    FillinData TEXT, -- JSON: {""delivery_days"": ""30"", ""contractor_name"": ""ABC Corp""}
    NodeList TEXT, -- JSON array of NodeIDs included: [NL-0001, NL-0003, NL-0005, NL-0012, NL-0047, NL-0082]

    FOREIGN KEY (DocTypeID)     REFERENCES DocType(DocTypeID), 
    FOREIGN KEY (PrevEditionID) REFERENCES Instance(InstanceID),
    FOREIGN KEY (BuildFromID)   REFERENCES Instance(InstanceID)
                );";

			using var cmd = connection.CreateCommand();
			cmd.CommandText = sql;
			cmd.ExecuteNonQuery();
		}
	}
}