using Microsoft.Data.Sqlite;
using MakeDoc.Core.Models;

namespace MakeDoc.Core.Data
{
	public class MakDocDb
	{
		private readonly string _connectionString;

		// ─────────────────────────────────────────────
		// Constructor — resolves MAKEDOC_DB env variable
		// ─────────────────────────────────────────────
		public MakDocDb()
		{
			string? dbDir = Environment.GetEnvironmentVariable("MAKEDOC_DB");

			if (string.IsNullOrWhiteSpace(dbDir))
				throw new InvalidOperationException(
					"Environment variable MAKEDOC_DB is not set. " +
					"Set it to the directory path that contains MAKEDOC.db.");

			if (!Directory.Exists(dbDir))
				throw new DirectoryNotFoundException(
					$"MAKEDOC_DB directory not found: {dbDir}");

			string dbPath = Path.Combine(dbDir, "MAKEDOC.db");
			_connectionString = $"Data Source={dbPath}";
		}

		// ─────────────────────────────────────────────
		// Connection and Schema
		// ─────────────────────────────────────────────
		public SqliteConnection OpenConnection()
		{
			var connection = new SqliteConnection(_connectionString);
			connection.Open();
			return connection;
		}

		public void InitializeSchema()
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                PRAGMA foreign_keys = ON;

                CREATE TABLE IF NOT EXISTS Node (
                    NodeID   TEXT PRIMARY KEY,
                    Type     TEXT NOT NULL,
                    NodeType TEXT NOT NULL CHECK(NodeType IN (
                                'Document','Clause','HeaderNode')),
                    Title    TEXT NULL,
                    Sequence INTEGER NOT NULL DEFAULT 0,
					IsUserClause INTEGER NOT NULL DEFAULT 0,
					IsSpecialClause INTEGER NOT NULL DEFAULT 0,
					DerivedFrom TEXT NULL,
                    Content  BLOB NULL,

				FOREIGN KEY (DerivedFrom) REFERENCES Node(NodeID)
                );

                CREATE TABLE IF NOT EXISTS DocType (
                    DocTypeID     TEXT PRIMARY KEY,
                    Name          TEXT NOT NULL,
                    InclusionTags TEXT,
                    HeaderNodeID  TEXT NULL,
                    Tier TEXT NULL, -- 'micro' | 'standard' | 'complex'
                    FOREIGN KEY (HeaderNodeID) REFERENCES Node(NodeID)
                );

                CREATE TABLE IF NOT EXISTS NodeHierarchy (
                    ParentNodeID TEXT NOT NULL,
                    ChildNodeID  TEXT NOT NULL,
                    DocTypeID    TEXT NOT NULL,
                    Sequence     INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (ParentNodeID, ChildNodeID, DocTypeID),
                    FOREIGN KEY (ParentNodeID) REFERENCES Node(NodeID),
                    FOREIGN KEY (ChildNodeID)  REFERENCES Node(NodeID),
                    FOREIGN KEY (DocTypeID)    REFERENCES DocType(DocTypeID)
                );

                CREATE TABLE IF NOT EXISTS Instance (
					InstanceID TEXT PRIMARY KEY,
					DocTypeID TEXT NOT NULL,
					PrevEditionID TEXT NULL,
					BuildFromID TEXT NULL,
					GeneratedDate TEXT DEFAULT (datetime('now')),
					IsArchived INTEGER DEFAULT 0, -- 0 = active, 1 = archived
					ArchiveDate TEXT NULL,
					Title TEXT NOT NULL DEFAULT '',
        
					-- JSON data columns
					InclusionData TEXT, -- JSON: {""tags"": [""DEI"", ""HAZMAT""], ""tier"": ""standard""}
					FillinData TEXT, -- JSON: {""delivery_days"": ""30"", ""contractor_name"": ""ABC Corp""}
					NodeList TEXT, -- JSON array of NodeIDs included: [NL-0001, NL-0003, NL-0005, NL-0012, NL-0047, NL-0082]

					FOREIGN KEY (DocTypeID)     REFERENCES DocType(DocTypeID), 
					FOREIGN KEY (PrevEditionID) REFERENCES Instance(InstanceID),
					FOREIGN KEY (BuildFromID)   REFERENCES Instance(InstanceID)
				);

				CREATE TABLE IF NOT EXISTS LineItem (
					LineItemID   TEXT PRIMARY KEY,
					DocTypeID    TEXT NULL,
					InstanceID   TEXT NULL,
					LineNum      INTEGER NOT NULL,
					Description  TEXT NOT NULL,
					NAICS        INTEGER DEFAULT 0,
					Unit         TEXT NOT NULL,
					Quantity     REAL NOT NULL,
					UnitPrice    REAL NOT NULL,

					FOREIGN KEY (DocTypeID)  REFERENCES DocType(DocTypeID),
					FOREIGN KEY (InstanceID) REFERENCES Instance(InstanceID),
					CHECK (
						(DocTypeID IS NOT NULL AND InstanceID IS NULL) OR
						(DocTypeID IS NULL AND InstanceID IS NOT NULL)
					)
				);


				CREATE INDEX IF NOT EXISTS idx_Node_NodeType ON Node(NodeType);
				CREATE INDEX IF NOT EXISTS idx_Node_Sequence ON Node(Sequence);
				CREATE INDEX IF NOT EXISTS idx_DocType_Name ON DocType(Name);
				CREATE INDEX IF NOT EXISTS idx_Instance_DocTypeID ON Instance(DocTypeID);
				CREATE INDEX IF NOT EXISTS idx_Instance_GeneratedDate ON Instance(GeneratedDate);
            ";

			cmd.ExecuteNonQuery();
		}

		// ═════════════════════════════════════════════
		// NODE QUERIES
		// ═════════════════════════════════════════════

		public void InsertNode(Node node)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                INSERT INTO Node (NodeID, NodeType, Sequence, Title, Content)
                VALUES (@NodeID, @NodeType, @Sequence, @Title, @Content);";

			cmd.Parameters.AddWithValue("@NodeID", node.NodeID);
			cmd.Parameters.AddWithValue("@NodeType", node.NodeType);
			cmd.Parameters.AddWithValue("@Title", node.Title ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@Content", node.Content ?? (object)DBNull.Value);

			cmd.ExecuteNonQuery();
		}

		public Node? GetNode(string nodeID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                SELECT NodeID, NodeType, Title, Content
                FROM Node
                WHERE NodeID = @NodeID;";

			cmd.Parameters.AddWithValue("@NodeID", nodeID);

			using var reader = cmd.ExecuteReader();
			if (!reader.Read()) return null;

			return MapNode(reader);
		}

		public List<Node> GetAllNodes()
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                SELECT NodeID, NodeType, Title, Content
                FROM Node
                ORDER BY Sequence;";

			using var reader = cmd.ExecuteReader();
			var nodes = new List<Node>();
			while (reader.Read())
				nodes.Add(MapNode(reader));

			return nodes;
		}

		public List<Node> GetNodesByType(string nodeType)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                SELECT NodeID, NodeType, Title, Content
                FROM Node
                WHERE NodeType = @NodeType
                ORDER BY Sequence;";

			cmd.Parameters.AddWithValue("@NodeType", nodeType);

			using var reader = cmd.ExecuteReader();
			var nodes = new List<Node>();
			while (reader.Read())
				nodes.Add(MapNode(reader));

			return nodes;
		}

		public void UpdateNode(Node node)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                UPDATE Node
                SET NodeType = @NodeType,
                    Title    = @Title,
                    Content  = @Content
                WHERE NodeID = @NodeID;";

			cmd.Parameters.AddWithValue("@NodeID", node.NodeID);
			cmd.Parameters.AddWithValue("@NodeType", node.NodeType);
			cmd.Parameters.AddWithValue("@Title", node.Title ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@Content", node.Content ?? (object)DBNull.Value);

			cmd.ExecuteNonQuery();
		}

		public void DeleteNode(string nodeID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = "DELETE FROM Node WHERE NodeID = @NodeID;";
			cmd.Parameters.AddWithValue("@NodeID", nodeID);
			cmd.ExecuteNonQuery();
		}

		private static Node MapNode(SqliteDataReader reader)
		{
			return new Node
			{
				NodeID = reader.GetString(0),
				NodeType = reader.GetString(1),
				Title = reader.IsDBNull(2) ? null : reader.GetString(2),
				Content = reader.IsDBNull(3) ? null : (byte[])reader[3]
			};
		}

		// ═════════════════════════════════════════════
		// DOCTYPE QUERIES
		// ═════════════════════════════════════════════

		public void InsertDocType(DocType docType)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                INSERT INTO DocType (DocTypeID, Name, InclusionTags, HeaderNodeID, Tier)
                VALUES (@DocTypeID, @Name, @InclusionTags, @HeaderNodeID, @Tier);";

			cmd.Parameters.AddWithValue("@DocTypeID", docType.DocTypeID);
			cmd.Parameters.AddWithValue("@Name", docType.Name);
			cmd.Parameters.AddWithValue("@InclusionTags", docType.InclusionTags ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@HeaderNodeID", docType.HeaderNodeID ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@Tier", docType.Tier ?? (object)DBNull.Value);

			cmd.ExecuteNonQuery();
		}

		public DocType? GetDocType(string docTypeID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                SELECT DocTypeID, Name, InclusionTags, HeaderNodeID, Tier
                FROM DocType
                WHERE DocTypeID = @DocTypeID;";

			cmd.Parameters.AddWithValue("@DocTypeID", docTypeID);

			using var reader = cmd.ExecuteReader();
			if (!reader.Read()) return null;

			return MapDocType(reader);
		}

		public List<DocType> GetAllDocTypes()
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                SELECT DocTypeID, Name, InclusionTags, HeaderNodeID, Tier
                FROM DocType
                ORDER BY Name;";

			using var reader = cmd.ExecuteReader();
			var docTypes = new List<DocType>();
			while (reader.Read())
				docTypes.Add(MapDocType(reader));

			return docTypes;
		}

		public void UpdateDocType(DocType docType)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                UPDATE DocType
                SET Name           = @Name,
                    InclusionTags  = @InclusionTags,
                    HeaderNodeID   = @HeaderNodeID,
                    Tier           = @Tier
                WHERE DocTypeID = @DocTypeID;";

			cmd.Parameters.AddWithValue("@DocTypeID", docType.DocTypeID);
			cmd.Parameters.AddWithValue("@Name", docType.Name);
			cmd.Parameters.AddWithValue("@InclusionTags", docType.InclusionTags ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@HeaderNodeID", docType.HeaderNodeID ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@Tier", docType.Tier ?? (object)DBNull.Value);

			cmd.ExecuteNonQuery();
		}

		public void DeleteDocType(string docTypeID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = "DELETE FROM DocType WHERE DocTypeID = @DocTypeID;";
			cmd.Parameters.AddWithValue("@DocTypeID", docTypeID);
			cmd.ExecuteNonQuery();
		}

		private static DocType MapDocType(SqliteDataReader reader)
		{
			return new DocType
			{
				DocTypeID = reader.GetString(0),
				Name = reader.GetString(1),
				InclusionTags = reader.IsDBNull(2) ? null : reader.GetString(2),
				HeaderNodeID = reader.IsDBNull(3) ? null : reader.GetString(3),
				Tier = reader.IsDBNull(4) ? null : reader.GetString(4)
			};
		}

		// ═════════════════════════════════════════════
		// NODE HIERARCHY QUERIES
		// ═════════════════════════════════════════════

		public void InsertNodeHierarchy(NodeHierarchy hierarchy)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                INSERT INTO NodeHierarchy (ParentNodeID, ChildNodeID, DocTypeID, Sequence)
                VALUES (@ParentNodeID, @ChildNodeID, @DocTypeID, @Sequence);";

			cmd.Parameters.AddWithValue("@ParentNodeID", hierarchy.ParentNodeID);
			cmd.Parameters.AddWithValue("@ChildNodeID", hierarchy.ChildNodeID);
			cmd.Parameters.AddWithValue("@DocTypeID", hierarchy.DocTypeID);
			cmd.Parameters.AddWithValue("@Sequence", hierarchy.Sequence);

			cmd.ExecuteNonQuery();
		}

		// Returns all children of a node within a document type, in sequence order
		public List<NodeHierarchy> GetChildren(string parentNodeID, string docTypeID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                SELECT ParentNodeID, ChildNodeID, DocTypeID, Sequence
                FROM NodeHierarchy
                WHERE ParentNodeID = @ParentNodeID
                  AND DocTypeID    = @DocTypeID
                ORDER BY Sequence;";

			cmd.Parameters.AddWithValue("@ParentNodeID", parentNodeID);
			cmd.Parameters.AddWithValue("@DocTypeID", docTypeID);

			using var reader = cmd.ExecuteReader();
			var rows = new List<NodeHierarchy>();
			while (reader.Read())
				rows.Add(MapNodeHierarchy(reader));

			return rows;
		}

		// Returns all parents of a node — useful for breadcrumb/navigation
		public List<NodeHierarchy> GetParents(string childNodeID, string docTypeID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                SELECT ParentNodeID, ChildNodeID, DocTypeID, Sequence
                FROM NodeHierarchy
                WHERE ChildNodeID = @ChildNodeID
                  AND DocTypeID   = @DocTypeID;";

			cmd.Parameters.AddWithValue("@ChildNodeID", childNodeID);
			cmd.Parameters.AddWithValue("@DocTypeID", docTypeID);

			using var reader = cmd.ExecuteReader();
			var rows = new List<NodeHierarchy>();
			while (reader.Read())
				rows.Add(MapNodeHierarchy(reader));

			return rows;
		}

		public void DeleteNodeHierarchy(string parentNodeID, string childNodeID, string docTypeID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                DELETE FROM NodeHierarchy
                WHERE ParentNodeID = @ParentNodeID
                  AND ChildNodeID  = @ChildNodeID
                  AND DocTypeID    = @DocTypeID;";

			cmd.Parameters.AddWithValue("@ParentNodeID", parentNodeID);
			cmd.Parameters.AddWithValue("@ChildNodeID", childNodeID);
			cmd.Parameters.AddWithValue("@DocTypeID", docTypeID);

			cmd.ExecuteNonQuery();
		}

		private static NodeHierarchy MapNodeHierarchy(SqliteDataReader reader)
		{
			return new NodeHierarchy
			{
				ParentNodeID = reader.GetString(0),
				ChildNodeID = reader.GetString(1),
				DocTypeID = reader.GetString(2),
				Sequence = reader.GetInt32(3)
			};
		}

		// ═════════════════════════════════════════════
		// INSTANCE QUERIES
		// ═════════════════════════════════════════════

		public void InsertInstance(Instance instance)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                INSERT INTO Instance
                    (InstanceID, DocTypeID, PrevEditionID, BuildFromID,
                     InclusionData, FillinData, NodeList, Title)
                VALUES
                    (@InstanceID, @DocTypeID, @PrevEditionID, @BuildFromID,
                     @InclusionData, @FillinData, @NodeList, @Title);";

			cmd.Parameters.AddWithValue("@InstanceID", instance.InstanceID);
			cmd.Parameters.AddWithValue("@DocTypeID", instance.DocTypeID);
			cmd.Parameters.AddWithValue("@PrevEditionID", instance.PrevEditionID ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@BuildFromID", instance.BuildFromID ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@InclusionData", instance.InclusionData ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@FillinData", instance.FillinData ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@NodeList", instance.NodeList ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@Title", instance.Title ?? (object)DBNull.Value);

			// GeneratedDate defaults to datetime('now'); IsArchived defaults to 0.
			cmd.ExecuteNonQuery();
		}

		public Instance? GetInstance(string instanceID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                SELECT InstanceID, DocTypeID, PrevEditionID, BuildFromID,
                       GeneratedDate, IsArchived, ArchiveDate,
                       InclusionData, FillinData, NodeList
                FROM Instance
                WHERE InstanceID = @InstanceID;";

			cmd.Parameters.AddWithValue("@InstanceID", instanceID);

			using var reader = cmd.ExecuteReader();
			if (!reader.Read()) return null;

			return MapInstance(reader);
		}

		public List<Instance> GetInstancesByDocType(string docTypeID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                SELECT InstanceID, DocTypeID, PrevEditionID, BuildFromID,
                       GeneratedDate, IsArchived, ArchiveDate,
                       InclusionData, FillinData, NodeList
                FROM Instance
                WHERE DocTypeID = @DocTypeID
                ORDER BY GeneratedDate DESC;";

			cmd.Parameters.AddWithValue("@DocTypeID", docTypeID);

			using var reader = cmd.ExecuteReader();
			var instances = new List<Instance>();
			while (reader.Read())
				instances.Add(MapInstance(reader));

			return instances;
		}

		// Returns active (IsArchived=0) or archived (IsArchived=1) instances,
		// joined to DocType so the form can display the document type name.
		public List<Instance> GetInstancesByArchiveStatus(bool archived)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                SELECT i.InstanceID, i.DocTypeID, d.Name,
                       i.PrevEditionID, i.BuildFromID, i.GeneratedDate,
                       i.IsArchived, i.ArchiveDate,
                       i.InclusionData, i.FillinData, i.NodeList,
                       d.Tier, i.Title
                FROM Instance i
                LEFT JOIN DocType d ON i.DocTypeID = d.DocTypeID
                WHERE i.IsArchived = @IsArchived
                ORDER BY i.GeneratedDate DESC;";

			cmd.Parameters.AddWithValue("@IsArchived", archived ? 1 : 0);

			using var reader = cmd.ExecuteReader();
			var instances = new List<Instance>();
			while (reader.Read())
				instances.Add(MapInstanceWithDocType(reader));

			return instances;
		}

		// Returns all instances (active + archived) joined to DocType for Name + Tier,
		// ordered newest first. Used by MainDashboard to render the Instance table where
		// archived rows are visible-but-de-emphasized (per ADR-007 / build-from.md).
		public List<Instance> GetAllInstancesWithDocType()
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                SELECT i.InstanceID, i.DocTypeID, d.Name,
                       i.PrevEditionID, i.BuildFromID, i.GeneratedDate,
                       i.IsArchived, i.ArchiveDate,
                       i.InclusionData, i.FillinData, i.NodeList,
                       d.Tier, i.Title
                FROM Instance i
                LEFT JOIN DocType d ON i.DocTypeID = d.DocTypeID
                ORDER BY i.GeneratedDate DESC;";

			using var reader = cmd.ExecuteReader();
			var instances = new List<Instance>();
			while (reader.Read())
				instances.Add(MapInstanceWithDocType(reader));

			return instances;
		}

		// Flips the archive flag on a single instance.
		// Passing archived=true sets IsArchived=1 and records today's date.
		// Passing archived=false clears both fields.
		public void SetArchiveStatus(string instanceID, bool archived)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                UPDATE Instance
                SET IsArchived  = @IsArchived,
                    ArchiveDate = @ArchiveDate
                WHERE InstanceID = @InstanceID;";

			cmd.Parameters.AddWithValue("@InstanceID", instanceID);
			cmd.Parameters.AddWithValue("@IsArchived", archived ? 1 : 0);
			cmd.Parameters.AddWithValue("@ArchiveDate",
				archived
					? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")
					: (object)DBNull.Value);

			cmd.ExecuteNonQuery();
		}

		// Returns the full edition chain for a given instance.
		public List<Instance> GetEditionHistory(string instanceID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
                WITH RECURSIVE EditionChain AS (
                    SELECT InstanceID, DocTypeID, PrevEditionID, BuildFromID,
                           GeneratedDate, IsArchived, ArchiveDate,
                           InclusionData, FillinData, NodeList
                    FROM Instance
                    WHERE InstanceID = @InstanceID

                    UNION ALL

                    SELECT i.InstanceID, i.DocTypeID, i.PrevEditionID, i.BuildFromID,
                           i.GeneratedDate, i.IsArchived, i.ArchiveDate,
                           i.InclusionData, i.FillinData, i.NodeList
                    FROM Instance i
                    INNER JOIN EditionChain ec ON i.InstanceID = ec.PrevEditionID
                )
                SELECT * FROM EditionChain;";

			cmd.Parameters.AddWithValue("@InstanceID", instanceID);

			using var reader = cmd.ExecuteReader();
			var instances = new List<Instance>();
			while (reader.Read())
				instances.Add(MapInstance(reader));

			return instances;
		}

		public void DeleteInstance(string instanceID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = "DELETE FROM Instance WHERE InstanceID = @InstanceID;";
			cmd.Parameters.AddWithValue("@InstanceID", instanceID);
			cmd.ExecuteNonQuery();
		}

		// Columns: InstanceID(0), DocTypeID(1), PrevEditionID(2), BuildFromID(3),
		//          GeneratedDate(4), IsArchived(5), ArchiveDate(6),
		//          InclusionData(7), FillinData(8), NodeList(9)
		private static Instance MapInstance(SqliteDataReader reader)
		{
			return new Instance
			{
				InstanceID = reader.GetString(0),
				DocTypeID = reader.GetString(1),
				PrevEditionID = reader.IsDBNull(2) ? null : reader.GetString(2),
				BuildFromID = reader.IsDBNull(3) ? null : reader.GetString(3),
				GeneratedDate = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
				IsArchived = reader.GetInt32(5) == 1,
				ArchiveDate = reader.IsDBNull(6) ? null : reader.GetString(6),
				InclusionData = reader.IsDBNull(7) ? null : reader.GetString(7),
				FillinData = reader.IsDBNull(8) ? null : reader.GetString(8),
				NodeList = reader.IsDBNull(9) ? null : reader.GetString(9)
			};
		}

		// Columns: InstanceID(0), DocTypeID(1), DocTypeName(2),
		//          PrevEditionID(3), BuildFromID(4), GeneratedDate(5),
		//          IsArchived(6), ArchiveDate(7),
		//          InclusionData(8), FillinData(9), NodeList(10),
		//          DocTypeTier(11), Title(12)
		private static Instance MapInstanceWithDocType(SqliteDataReader reader)
			{
			return new Instance
			{
				InstanceID = reader.GetString(0),
				DocTypeID = reader.GetString(1),
				DocTypeName = reader.IsDBNull(2) ? null : reader.GetString(2),
				PrevEditionID = reader.IsDBNull(3) ? null : reader.GetString(3),
				BuildFromID = reader.IsDBNull(4) ? null : reader.GetString(4),
				GeneratedDate = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
				IsArchived = reader.GetInt32(6) == 1,
				ArchiveDate = reader.IsDBNull(7) ? null : reader.GetString(7),
				InclusionData = reader.IsDBNull(8) ? null : reader.GetString(8),
				FillinData = reader.IsDBNull(9) ? null : reader.GetString(9),
				NodeList = reader.IsDBNull(10) ? null : reader.GetString(10),
				DocTypeTier = reader.IsDBNull(11) ? null : reader.GetString(11),
				Title = reader.IsDBNull(12) ? string.Empty : reader.GetString(12)
			};
		}
		public void InsertLineItem(LineItem item)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
        INSERT INTO LineItem
            (LineItemID, DocTypeID, InstanceID, LineNum, Description, NAICS, Unit, Quantity, UnitPrice)
        VALUES
            (@LineItemID, @DocTypeID, @InstanceID, @LineNum, @Description, @NAICS, @Unit, @Quantity, @UnitPrice);";

			cmd.Parameters.AddWithValue("@LineItemID", item.LineItemID);
			cmd.Parameters.AddWithValue("@DocTypeID",  (object?)item.DocTypeID  ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@InstanceID", (object?)item.InstanceID ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@LineNum",    item.LineNum);
			cmd.Parameters.AddWithValue("@Description", item.Description);
			cmd.Parameters.AddWithValue("@NAICS",      item.NAICS);
			cmd.Parameters.AddWithValue("@Unit",       item.Unit);
			cmd.Parameters.AddWithValue("@Quantity",   item.Quantity);
			cmd.Parameters.AddWithValue("@UnitPrice",  item.UnitPrice);

			cmd.ExecuteNonQuery();
		}

		public LineItem? GetLineItem(string lineItemID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
        SELECT LineItemID, DocTypeID, InstanceID, LineNum, Description, NAICS, Unit, Quantity, UnitPrice
        FROM LineItem
        WHERE LineItemID = @LineItemID;";

			cmd.Parameters.AddWithValue("@LineItemID", lineItemID);

			using var reader = cmd.ExecuteReader();
			if (!reader.Read()) return null;

			return MapLineItem(reader);
		}

		public List<LineItem> GetLineItemsForDocType(string docTypeID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
        SELECT LineItemID, DocTypeID, InstanceID, LineNum, Description, NAICS, Unit, Quantity, UnitPrice
        FROM LineItem
        WHERE DocTypeID = @DocTypeID
        ORDER BY LineNum;";

			cmd.Parameters.AddWithValue("@DocTypeID", docTypeID);

			using var reader = cmd.ExecuteReader();
			var items = new List<LineItem>();

			while (reader.Read())
				items.Add(MapLineItem(reader));

			return items;
		}

		public List<LineItem> GetLineItemsForInstance(string instanceID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
        SELECT LineItemID, DocTypeID, InstanceID, LineNum, Description, NAICS, Unit, Quantity, UnitPrice
        FROM LineItem
        WHERE InstanceID = @InstanceID
        ORDER BY LineNum;";

			cmd.Parameters.AddWithValue("@InstanceID", instanceID);

			using var reader = cmd.ExecuteReader();
			var items = new List<LineItem>();

			while (reader.Read())
				items.Add(MapLineItem(reader));

			return items;
		}

		/// <summary>
		/// Re-keys all line items staged against a DocType to a concrete
		/// Instance. Called once at generation time so the instance owns
		/// its line items and the doctype staging area is cleared.
		/// </summary>
		public void AssignLineItemsToInstance(string docTypeID, string instanceID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
        UPDATE LineItem
        SET InstanceID = @InstanceID,
            DocTypeID  = NULL
        WHERE DocTypeID = @DocTypeID;";

			cmd.Parameters.AddWithValue("@DocTypeID",  docTypeID);
			cmd.Parameters.AddWithValue("@InstanceID", instanceID);

			cmd.ExecuteNonQuery();
		}

		/// <summary>
		/// Copies the line items owned by a source instance to a target
		/// instance (new LineItemIDs). Used when building a new document
		/// from an existing one so line items carry through.
		/// </summary>
		public void CopyLineItemsToInstance(string sourceInstanceID, string targetInstanceID)
		{
			foreach (var src in GetLineItemsForInstance(sourceInstanceID))
			{
				InsertLineItem(new LineItem
				{
					LineItemID  = Guid.NewGuid().ToString(),
					DocTypeID   = null,
					InstanceID  = targetInstanceID,
					LineNum     = src.LineNum,
					Description = src.Description,
					NAICS       = src.NAICS,
					Unit        = src.Unit,
					Quantity    = src.Quantity,
					UnitPrice   = src.UnitPrice
				});
			}
		}

		public void UpdateLineItem(LineItem item)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = @"
        UPDATE LineItem
        SET DocTypeID   = @DocTypeID,
            InstanceID  = @InstanceID,
            LineNum     = @LineNum,
            Description = @Description,
            NAICS       = @NAICS,
            Unit        = @Unit,
            Quantity    = @Quantity,
            UnitPrice   = @UnitPrice
        WHERE LineItemID = @LineItemID;";

			cmd.Parameters.AddWithValue("@LineItemID", item.LineItemID);
			cmd.Parameters.AddWithValue("@DocTypeID",  (object?)item.DocTypeID  ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@InstanceID", (object?)item.InstanceID ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@LineNum",    item.LineNum);
			cmd.Parameters.AddWithValue("@Description", item.Description);
			cmd.Parameters.AddWithValue("@NAICS",      item.NAICS);
			cmd.Parameters.AddWithValue("@Unit",       item.Unit);
			cmd.Parameters.AddWithValue("@Quantity",   item.Quantity);
			cmd.Parameters.AddWithValue("@UnitPrice",  item.UnitPrice);

			cmd.ExecuteNonQuery();
		}

		public void DeleteLineItem(string lineItemID)
		{
			using var connection = OpenConnection();
			using var cmd = connection.CreateCommand();

			cmd.CommandText = "DELETE FROM LineItem WHERE LineItemID = @LineItemID;";
			cmd.Parameters.AddWithValue("@LineItemID", lineItemID);

			cmd.ExecuteNonQuery();
		}

		private static LineItem MapLineItem(SqliteDataReader reader)
		{
			return new LineItem
			{
				LineItemID  = reader.GetString(0),
				DocTypeID   = reader.IsDBNull(1) ? null : reader.GetString(1),
				InstanceID  = reader.IsDBNull(2) ? null : reader.GetString(2),
				LineNum     = reader.GetInt32(3),
				Description = reader.GetString(4),
				NAICS       = reader.GetInt32(5),
				Unit        = reader.GetString(6),
				Quantity    = reader.GetDouble(7),
				UnitPrice   = reader.GetDouble(8)
			};
		}

	}
}