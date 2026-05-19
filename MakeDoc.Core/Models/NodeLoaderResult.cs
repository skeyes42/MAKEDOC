using System;
using System.Collections.Generic;

namespace MakeDoc.Core.Models
{
    public class NodeLoaderResult
    {
        public List<string> Loaded { get; } = new();
        public List<string> NotFound { get; } = new();
        public List<(string ID, string Message)> Errors { get; } = new();

        public void PrintSummary()
        {
            Console.WriteLine("\n── Summary ──────────────────────────────────────");
            Console.WriteLine($"  Loaded:    {Loaded.Count}");
            Console.WriteLine($"  Not found: {NotFound.Count}");
            Console.WriteLine($"  Errors:    {Errors.Count}");

            if (NotFound.Count > 0)
            {
                Console.WriteLine("\nMissing files:");
                foreach (var id in NotFound)
                    Console.WriteLine($"  {id}");
            }

            if (Errors.Count > 0)
            {
                Console.WriteLine("\nErrors:");
                foreach (var (id, msg) in Errors)
                    Console.WriteLine($"  {id}: {msg}");
            }
        }
    }
}