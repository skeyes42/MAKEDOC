using Microsoft.Data.Sqlite;
using MakeDoc.Core.Data;
using MakeDoc.Core.Models;

namespace MakeDoc.Core.Services
{
	public class NodeHierarchyService
	{
		private readonly MakDocDb _db;

		public NodeHierarchyService(MakDocDb db)
		{
			_db = db;
		}

		// ─────────────────────────────────────────────────────────────
		// GET ALL EDGES FOR A DOCTYPE
		// Returns the raw adjacency list rows for a given DocTypeID,
		// ordered by Sequence.
		// ─────────────────────────────────────────────────────────────
		public List<NodeHierarchy> GetByDocType(string docTypeId)
		{
			var rows = new List<NodeHierarchy>();

			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
                SELECT ParentNodeID, ChildNodeID, DocTypeID, Sequence
                FROM NodeHierarchy
                WHERE DocTypeID = @docTypeId
                ORDER BY Sequence";
			cmd.Parameters.AddWithValue("@docTypeId", docTypeId);

			using var reader = cmd.ExecuteReader();
			while (reader.Read())
				rows.Add(MapRow(reader));

			return rows;
		}

		// ─────────────────────────────────────────────────────────────
		// TRAVERSE — returns ordered flat node list for a DocType
		// Walks the linked list starting from the head node found in
		// DocType.HeaderNodeID, following ParentNodeID → ChildNodeID
		// edges in Sequence order.
		// ─────────────────────────────────────────────────────────────
		public List<string> GetOrderedNodeIds(string docTypeId, string headerNodeId)
		{
			var edges = GetByDocType(docTypeId);

			// Build a lookup: ParentNodeID -> ChildNodeID
			var nextNode = edges.ToDictionary(
				e => e.ParentNodeID,
				e => e.ChildNodeID);

			var orderedNodes = new List<string>();
			var visited = new HashSet<string>();

			string? current = headerNodeId;

			while (current != null
				   && !string.IsNullOrWhiteSpace(current)
				   && !visited.Contains(current))
			{
				orderedNodes.Add(current);
				visited.Add(current);

				nextNode.TryGetValue(current, out current);
			}

			return orderedNodes;
		}

		// ─────────────────────────────────────────────────────────────
		// GET CHILDREN OF A NODE (direct children only)
		// ─────────────────────────────────────────────────────────────
		public List<string> GetChildren(string parentNodeId, string docTypeId)
		{
			var children = new List<string>();

			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
                SELECT ChildNodeID
                FROM NodeHierarchy
                WHERE ParentNodeID = @parentId
                  AND DocTypeID    = @docTypeId
                ORDER BY Sequence";
			cmd.Parameters.AddWithValue("@parentId", parentNodeId);
			cmd.Parameters.AddWithValue("@docTypeId", docTypeId);

			using var reader = cmd.ExecuteReader();
			while (reader.Read())
				children.Add(reader.GetString(0));

			return children;
		}

		// ─────────────────────────────────────────────────────────────
		// GET PARENT OF A NODE
		// ─────────────────────────────────────────────────────────────
		public string? GetParent(string childNodeId, string docTypeId)
		{
			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
                SELECT ParentNodeID
                FROM NodeHierarchy
                WHERE ChildNodeID = @childId
                  AND DocTypeID   = @docTypeId
                LIMIT 1";
			cmd.Parameters.AddWithValue("@childId", childNodeId);
			cmd.Parameters.AddWithValue("@docTypeId", docTypeId);

			var result = cmd.ExecuteScalar();
			return result is DBNull || result is null ? null : (string)result;
		}

		// ─────────────────────────────────────────────────────────────
		// INSERT EDGE
		// ─────────────────────────────────────────────────────────────
		public void Insert(NodeHierarchy edge)
		{
			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
                INSERT INTO NodeHierarchy 
                    (ParentNodeID, ChildNodeID, DocTypeID, Sequence)
                VALUES 
                    (@parentId, @childId, @docTypeId, @seq)";
			cmd.Parameters.AddWithValue("@parentId", edge.ParentNodeID);
			cmd.Parameters.AddWithValue("@childId", edge.ChildNodeID);
			cmd.Parameters.AddWithValue("@docTypeId", edge.DocTypeID);
			cmd.Parameters.AddWithValue("@seq", edge.Sequence);

			cmd.ExecuteNonQuery();
		}

		// ─────────────────────────────────────────────────────────────
		// DELETE EDGE
		// ─────────────────────────────────────────────────────────────
		public void Delete(string parentNodeId, string childNodeId, string docTypeId)
		{
			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
                DELETE FROM NodeHierarchy
                WHERE ParentNodeID = @parentId
                  AND ChildNodeID  = @childId
                  AND DocTypeID    = @docTypeId";
			cmd.Parameters.AddWithValue("@parentId", parentNodeId);
			cmd.Parameters.AddWithValue("@childId", childNodeId);
			cmd.Parameters.AddWithValue("@docTypeId", docTypeId);

			cmd.ExecuteNonQuery();
		}

		// ─────────────────────────────────────────────────────────────
		// MAP ROW → NodeHierarchy
		// ─────────────────────────────────────────────────────────────
		private static NodeHierarchy MapRow(SqliteDataReader reader)
		{
			return new NodeHierarchy
			{
				ParentNodeID = reader.GetString(reader.GetOrdinal("ParentNodeID")),
				ChildNodeID = reader.GetString(reader.GetOrdinal("ChildNodeID")),
				DocTypeID = reader.GetString(reader.GetOrdinal("DocTypeID")),
				Sequence = reader.GetInt32(reader.GetOrdinal("Sequence"))
			};
		}
	}
}