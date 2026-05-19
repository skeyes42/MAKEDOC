using Microsoft.Data.Sqlite;
using MakeDoc.Core.Data;
using MakeDoc.Core.Models;

namespace MakeDoc.Core.Services
{
	public class NodeService
	{
		private readonly MakDocDb _db;

		public NodeService(MakDocDb db)
		{
			_db = db;
		}

		// ─────────────────────────────────────────────
		// GET ALL
		// ─────────────────────────────────────────────
		public List<Node> GetAll()
		{
			var nodes = new List<Node>();

			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = "SELECT * FROM Node ORDER BY Sequence";

			using var reader = cmd.ExecuteReader();
			while (reader.Read())
				nodes.Add(MapRow(reader));

			return nodes;
		}

		// ─────────────────────────────────────────────
		// GET BY ID
		// ─────────────────────────────────────────────
		public Node? GetById(string nodeId)
		{
			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = "SELECT * FROM Node WHERE NodeID = @id";
			cmd.Parameters.AddWithValue("@id", nodeId);

			using var reader = cmd.ExecuteReader();
			return reader.Read() ? MapRow(reader) : null;
		}

		// ─────────────────────────────────────────────
		// GET BY NODE TYPE
		// ─────────────────────────────────────────────
		public List<Node> GetByType(string nodeType)
		{
			var nodes = new List<Node>();

			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"SELECT * FROM Node 
                                WHERE NodeType = @type 
                                ORDER BY Sequence";
			cmd.Parameters.AddWithValue("@type", nodeType);

			using var reader = cmd.ExecuteReader();
			while (reader.Read())
				nodes.Add(MapRow(reader));

			return nodes;
		}

		// ─────────────────────────────────────────────
		// INSERT
		// ─────────────────────────────────────────────
		public void Insert(Node node)
		{
			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
                INSERT INTO Node 
                    (NodeID, NodeType, Title, Content, Sequence, 
                     IsActive, CreatedDate, ModifiedDate)
                VALUES 
                    (@id, @type, @title, @content, @seq, 
                     @active, @created, @modified)";

			cmd.Parameters.AddWithValue("@id", node.NodeID);
			cmd.Parameters.AddWithValue("@type", node.NodeType);
			cmd.Parameters.AddWithValue("@title", node.Title ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@content", node.Content ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@seq", node.Sequence);
			/*cmd.Parameters.AddWithValue("@active", node.IsActive ? 1 : 0);
			cmd.Parameters.AddWithValue("@created", node.CreatedDate ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@modified", node.ModifiedDate ?? (object)DBNull.Value);*/

			cmd.ExecuteNonQuery();
		}

		// ─────────────────────────────────────────────
		// UPDATE
		// ─────────────────────────────────────────────
		public void Update(Node node)
		{
			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
                UPDATE Node SET
                    NodeType     = @type,
                    Title        = @title,
                    Content      = @content,
                    Sequence     = @seq,
                    IsActive     = @active,
                    ModifiedDate = @modified
                WHERE NodeID = @id";

			cmd.Parameters.AddWithValue("@id", node.NodeID);
			cmd.Parameters.AddWithValue("@type", node.NodeType);
			cmd.Parameters.AddWithValue("@title", node.Title ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@content", node.Content ?? (object)DBNull.Value);
			cmd.Parameters.AddWithValue("@seq", node.Sequence);
			//cmd.Parameters.AddWithValue("@active", node.IsActive ? 1 : 0);
			cmd.Parameters.AddWithValue("@modified",
				DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"));

			cmd.ExecuteNonQuery();
		}

		// ─────────────────────────────────────────────
		// DELETE (soft delete — sets IsActive = 0)
		// ─────────────────────────────────────────────
		public void Deactivate(string nodeId)
		{
			using var connection = _db.OpenConnection();
			using var cmd = connection.CreateCommand();
			cmd.CommandText = @"
                UPDATE Node SET 
                    IsActive     = 0,
                    ModifiedDate = @modified
                WHERE NodeID = @id";

			cmd.Parameters.AddWithValue("@id", nodeId);
			cmd.Parameters.AddWithValue("@modified",
				DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"));

			cmd.ExecuteNonQuery();
		}

		// ─────────────────────────────────────────────
		// MAP ROW → NODE
		// ─────────────────────────────────────────────
		private static Node MapRow(SqliteDataReader reader)
		{
			return new Node
			{
				NodeID = reader.GetString(reader.GetOrdinal("NodeID")),
				NodeType = reader.GetString(reader.GetOrdinal("NodeType")),
				Title = reader.IsDBNull(reader.GetOrdinal("Title"))
								   ? null
								   : reader.GetString(reader.GetOrdinal("Title")),
				Content = reader.IsDBNull(reader.GetOrdinal("Content"))
								   ? null
								   : (byte[])reader["Content"],
				Sequence = reader.GetInt32(reader.GetOrdinal("Sequence")),
			/*	IsActive = reader.GetInt32(reader.GetOrdinal("IsActive")) == 1,
				CreatedDate = reader.IsDBNull(reader.GetOrdinal("CreatedDate"))
								   ? null
								   : reader.GetString(reader.GetOrdinal("CreatedDate")),
				ModifiedDate = reader.IsDBNull(reader.GetOrdinal("ModifiedDate"))
								   ? null
								   : reader.GetString(reader.GetOrdinal("ModifiedDate"))*/
			};
		}
	}
}