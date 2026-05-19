using Microsoft.Data.Sqlite;
using MakeDoc.Core.Data;
using MakeDoc.Core.Models;

namespace MakeDoc.Core.Services
{
	public class TemplateService
	{
		private readonly MakDocDb _db;
		private readonly NodeHierarchyService _hierarchyService;

		// ─────────────────────────────────────────────────────────────
		// Constructor
		// ─────────────────────────────────────────────────────────────
		public TemplateService(MakDocDb db, NodeHierarchyService hierarchyService)
		{
			_db = db;
			_hierarchyService = hierarchyService;
		}

		// ─────────────────────────────────────────────────────────────
		// GET ALL INSTANCES
		// ─────────────────────────────────────────────────────────────
		public List<Instance> GetAll()
		{
			var instances = new List<Instance>();

			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
                SELECT InstanceID, DocTypeID, PrevEditionID, BuildFromID,
                       GeneratedDate, InclusionData, FillinData, NodeList
                FROM Instance
                ORDER BY GeneratedDate DESC";

			using var reader = cmd.ExecuteReader();
			while (reader.Read())
				instances.Add(MapRow(reader));

			return instances;
		}

		// ─────────────────────────────────────────────────────────────
		// GET BY ID
		// ─────────────────────────────────────────────────────────────
		public Instance? GetById(string instanceId)
		{
			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
                SELECT InstanceID, DocTypeID, PrevEditionID, BuildFromID,
                       GeneratedDate, InclusionData, FillinData, NodeList
                FROM Instance
                WHERE InstanceID = @id";
			cmd.Parameters.AddWithValue("@id", instanceId);

			using var reader = cmd.ExecuteReader();
			return reader.Read() ? MapRow(reader) : null;
		}

		// ─────────────────────────────────────────────────────────────
		// GET BY DOCTYPE
		// Returns all instances for a given DocTypeID, newest first.
		// ─────────────────────────────────────────────────────────────
		public List<Instance> GetByDocType(string docTypeId)
		{
			var instances = new List<Instance>();

			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
                SELECT InstanceID, DocTypeID, PrevEditionID, BuildFromID,
                       GeneratedDate, InclusionData, FillinData, NodeList
                FROM Instance
                WHERE DocTypeID = @docTypeId
                ORDER BY GeneratedDate DESC";
			cmd.Parameters.AddWithValue("@docTypeId", docTypeId);

			using var reader = cmd.ExecuteReader();
			while (reader.Read())
				instances.Add(MapRow(reader));

			return instances;
		}

		// ─────────────────────────────────────────────────────────────
		// CREATE NEW INSTANCE
		// Builds a fresh Instance from a DocType, using the
		// NodeHierarchy to populate the default NodeList.
		// ─────────────────────────────────────────────────────────────
		public Instance CreateFromDocType(string docTypeId, string headerNodeId)
		{
			var nodeList = _hierarchyService.GetOrderedNodeIds(docTypeId, headerNodeId);

			var instance = new Instance
			{
				InstanceID    = Guid.NewGuid().ToString(),
				DocTypeID     = docTypeId,
				PrevEditionID = null,
				BuildFromID   = null,
				NodeList      = System.Text.Json.JsonSerializer.Serialize(nodeList),
			};

			Save(instance);
			return instance;
		}

		// ─────────────────────────────────────────────────────────────
		// CREATE FROM EXISTING INSTANCE (new edition)
		// Clones an existing instance as a new edition, preserving
		// the node list and fill-in data as a starting point.
		// ─────────────────────────────────────────────────────────────
		public Instance CreateNewEdition(string sourceInstanceId)
		{
			var source = GetById(sourceInstanceId)
				?? throw new InvalidOperationException(
					$"Source instance not found: {sourceInstanceId}");

			var newEdition = new Instance
			{
				InstanceID    = Guid.NewGuid().ToString(),
				DocTypeID     = source.DocTypeID,
				PrevEditionID = source.InstanceID,
				BuildFromID   = null,
				InclusionData = source.InclusionData,
				FillinData    = source.FillinData,
				NodeList      = source.NodeList,
			};

			Save(newEdition);
			return newEdition;
		}

		// ─────────────────────────────────────────────────────────────
		// UPDATE NODE LIST
		// Called when the user adds or removes nodes from an instance.
		// ─────────────────────────────────────────────────────────────
		public void UpdateNodeList(string instanceId, List<string> nodeIds)
		{
			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
                UPDATE Instance SET
                    NodeList = @nodeList
                WHERE InstanceID = @id";

			cmd.Parameters.AddWithValue("@id", instanceId);
			cmd.Parameters.AddWithValue("@nodeList",
				System.Text.Json.JsonSerializer.Serialize(nodeIds));

			cmd.ExecuteNonQuery();
		}

		// ─────────────────────────────────────────────────────────────
		// UPDATE FILL-IN DATA
		// Saves the user's fill-in variable responses as JSON.
		// ─────────────────────────────────────────────────────────────
		public void UpdateFillinData(string instanceId, Dictionary<string, string> fillinData)
		{
			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
                UPDATE Instance SET
                    FillinData = @fillinData
                WHERE InstanceID = @id";

			cmd.Parameters.AddWithValue("@id", instanceId);
			cmd.Parameters.AddWithValue("@fillinData",
				System.Text.Json.JsonSerializer.Serialize(fillinData));

			cmd.ExecuteNonQuery();
		}

		// ─────────────────────────────────────────────────────────────
		// GET NODE LIST (deserialized)
		// Returns the NodeList JSON as an actual List<string>.
		// ─────────────────────────────────────────────────────────────
		public List<string> GetNodeList(string instanceId)
		{
			var instance = GetById(instanceId);

			if (instance?.NodeList == null)
				return new List<string>();

			return System.Text.Json.JsonSerializer
				.Deserialize<List<string>>(instance.NodeList)
				?? new List<string>();
		}

		// ─────────────────────────────────────────────────────────────
		// DELETE
		// ─────────────────────────────────────────────────────────────
		public void Delete(string instanceId)
		{
			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = "DELETE FROM Instance WHERE InstanceID = @id";
			cmd.Parameters.AddWithValue("@id", instanceId);
			cmd.ExecuteNonQuery();
		}

		// ─────────────────────────────────────────────────────────────
		// SAVE (INSERT OR REPLACE)
		// ─────────────────────────────────────────────────────────────
		private void Save(Instance instance)
		{
			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
                INSERT OR REPLACE INTO Instance
                    (InstanceID, DocTypeID, PrevEditionID, BuildFromID,
                     InclusionData, FillinData, NodeList)
                VALUES
                    (@id, @docTypeId, @prevId, @buildFromId,
                     @inclusionData, @fillinData, @nodeList)";

			cmd.Parameters.AddWithValue("@id",            instance.InstanceID);
			cmd.Parameters.AddWithValue("@docTypeId",     instance.DocTypeID);
			cmd.Parameters.AddWithValue("@prevId",        (object?)instance.PrevEditionID ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@buildFromId",   (object?)instance.BuildFromID   ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@inclusionData", (object?)instance.InclusionData ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@fillinData",    (object?)instance.FillinData    ?? DBNull.Value);
			cmd.Parameters.AddWithValue("@nodeList",      (object?)instance.NodeList      ?? DBNull.Value);

			cmd.ExecuteNonQuery();
		}

		// ─────────────────────────────────────────────────────────────
		// MAP ROW → INSTANCE
		// ─────────────────────────────────────────────────────────────
		private static Instance MapRow(SqliteDataReader reader)
		{
			return new Instance
			{
				InstanceID    = reader.GetString(reader.GetOrdinal("InstanceID")),
				DocTypeID     = reader.GetString(reader.GetOrdinal("DocTypeID")),
				PrevEditionID = reader.IsDBNull(reader.GetOrdinal("PrevEditionID"))
								? null
								: reader.GetString(reader.GetOrdinal("PrevEditionID")),
				BuildFromID   = reader.IsDBNull(reader.GetOrdinal("BuildFromID"))
								? null
								: reader.GetString(reader.GetOrdinal("BuildFromID")),
				GeneratedDate = reader.IsDBNull(reader.GetOrdinal("GeneratedDate"))
								? string.Empty
								: reader.GetString(reader.GetOrdinal("GeneratedDate")),
				InclusionData = reader.IsDBNull(reader.GetOrdinal("InclusionData"))
								? null
								: reader.GetString(reader.GetOrdinal("InclusionData")),
				FillinData    = reader.IsDBNull(reader.GetOrdinal("FillinData"))
								? null
								: reader.GetString(reader.GetOrdinal("FillinData")),
				NodeList      = reader.IsDBNull(reader.GetOrdinal("NodeList"))
								? null
								: reader.GetString(reader.GetOrdinal("NodeList")),
			};
		}
	}
}
