// MakeDoc.Core/Services/NodeLoaderService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.VisualBasic.FileIO;
using MakeDoc.Core.Models;
using MakeDoc.Core.Services;

namespace MakeDoc.Core.Services
{
    public class NodeLoaderService
    {
        private readonly string _dbPath;
        private readonly string _docsRoot;

        public NodeLoaderService(string dbPath, string docsRoot)
        {
            _dbPath = dbPath;
            _docsRoot = docsRoot;
        }

        public NodeLoaderResult LoadFromCsv(string csvPath)
        {
            var nodesToLoad = ParseCsv(csvPath);
            var docxLookup = BuildDocxLookup();
            return LoadNodes(nodesToLoad, docxLookup);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static List<(string NodeID, string NodeType)> ParseCsv(string csvPath)
        {
            var result = new List<(string, string)>();
            //using var parser = new TextFieldParser(csvPath);
            using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(csvPath);
            parser.TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited;
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(",");

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields == null || fields.Length < 3) continue;
                if (fields[0].Trim().Equals("x", StringComparison.OrdinalIgnoreCase))
                    result.Add((fields[1].Trim(), fields[2].Trim()));
            }
            return result;
        }

        private Dictionary<string, string> BuildDocxLookup()
        {
            return Directory
                .GetFiles(_docsRoot, "*.docx", System.IO.SearchOption.AllDirectories)
                .Where(p => Path.GetFileName(p).StartsWith("NL-", StringComparison.OrdinalIgnoreCase))
                .GroupBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }

        private NodeLoaderResult LoadNodes(
            List<(string NodeID, string NodeType)> nodes,
            Dictionary<string, string> docxLookup)
        {
            var result = new NodeLoaderResult();

            using var con = new SqliteConnection($"Data Source={_dbPath}");
            con.Open();
            using var transaction = con.BeginTransaction();

            foreach (var (nodeId, nodeType) in nodes)
            {
                if (!docxLookup.TryGetValue(nodeId, out string? filePath))
                {
                    result.NotFound.Add(nodeId);
                    continue;
                }

                try
                {
                    byte[] blob = File.ReadAllBytes(filePath);

                    using var cmd = con.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO Node (NodeID, NodeType, Sequence, Title, Content)
                        VALUES (@NodeID, @NodeType, 0, NULL, @Content)
                        ON CONFLICT(NodeID) DO UPDATE SET
                            NodeType = excluded.NodeType,
                            Content  = excluded.Content";

                    cmd.Parameters.AddWithValue("@NodeID", nodeId);
                    cmd.Parameters.AddWithValue("@NodeType", nodeType);
                    cmd.Parameters.AddWithValue("@Content", blob);
                    cmd.ExecuteNonQuery();

                    result.Loaded.Add(nodeId);
                }
                catch (Exception ex)
                {
                    result.Errors.Add((nodeId, ex.Message));
                }
            }

            transaction.Commit();
            return result;
        }
    }
}