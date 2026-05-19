using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxSdtScanner
{
    class Program
    {
        static void Main(string[] args)
        {
            string folderPath = @"C:\Users\skeye\BOOK2\MAKEDOC\docs\Clauses and assembled documents\clauses\micro\req";

            if (args.Length > 0)
            {
                folderPath = args[0];
            }

            Console.WriteLine(folderPath);

            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine("Directory not found.");
                return;
            }

            var docxFiles = Directory.GetFiles(folderPath, "*.docx", SearchOption.TopDirectoryOnly);

            if (docxFiles.Length == 0)
            {
                Console.WriteLine("No DOCX files found.");
                return;
            }

            foreach (var file in docxFiles)
            {
                Console.WriteLine($"\n=== {Path.GetFileName(file)} ===");

                try
                {
                    using (WordprocessingDocument doc = WordprocessingDocument.Open(file, false))
                    {
                        var body = doc.MainDocumentPart.Document.Body;

                        // All SDT elements in the document
                        var sdts = body.Descendants<SdtElement>().ToList();

                        // Keep only those with a non-empty Alias (= fill-ins)
                        var fillins = sdts
                            .Select(sdt => new
                            {
                            Title = sdt.SdtProperties?.GetFirstChild<SdtAlias>()?.Val?.Value,
                            Tag = sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value
                            })
                            .Where(x => !string.IsNullOrWhiteSpace(x.Title))
                            .ToList();

                        if (!fillins.Any())
                        {
                            // Console.WriteLine("No fill-ins found.");
                            continue;
                        }

                        foreach (var f in fillins)
                        {
                            Console.WriteLine($"Fill-in → Title: {f.Title}, Tag: {f.Tag ?? "(none)"}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading {file}: {ex.Message}");
                }
            }

            Console.WriteLine("\nDone.");
        }
    }
}