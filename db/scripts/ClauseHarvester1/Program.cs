using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

// ClauseHarvester
// ----------------
// Walks a directory tree of clause/header-node .docx files and reports:
//   NodeID    -- parsed from the file name (NL-<id>.docx | DEI-<id>.docx | HAZ-<id>.docx)
//   Title     -- text after "SECTION {SecNo} — " in the first non-empty paragraph
//                (blank for header nodes, which have no SECTION line)
//   Directory -- the containing directory
//
// Usage:
//   ClauseHarvester [<root-directory>]            > report.txt
//   ClauseHarvester                               > report.txt   (uses the default root)
//
// Output is tab-delimited so you can paste it straight into a spreadsheet.

internal static class Program
{
	private const string DefaultRoot =
		@"C:\Users\skeye\BOOK2\MAKEDOC\docs\Clauses and assembled documents\clauses";

	// Matches NL-<id>.docx, DEI-<id>.docx, HAZ-<id>.docx (case-insensitive).
	private static readonly Regex FileNameRx =
		new(@"^(NL|DEI|HAZ)-(.+)\.docx$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	// Matches "SECTION <secNo> — <title>" with em-dash, en-dash, or plain hyphen.
	private static readonly Regex SectionRx =
		new(@"^\s*SECTION\s+(\S+)\s*[—–\-]\s*(.+?)\s*$",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

	private static int Main(string[] args)
	{
		string root = args.Length > 0 ? args[0] : DefaultRoot;

		if (!Directory.Exists(root))
		{
			Console.Error.WriteLine($"ERROR: directory not found: {root}");
			return 1;
		}

		// Use UTF-8 so em-dashes and other punctuation in titles round-trip cleanly.
		Console.OutputEncoding = Encoding.UTF8;

		Console.WriteLine("NodeID\tTitle\tDirectory");

		int total = 0, clauses = 0, headers = 0, errors = 0;

		foreach (var file in Directory.EnumerateFiles(root, "*.docx", SearchOption.AllDirectories))
		{
			var name = Path.GetFileName(file);
			var m = FileNameRx.Match(name);
			if (!m.Success) continue;

			string nodeId = m.Groups[2].Value;
			string dir = Path.GetDirectoryName(file) ?? "";
			string title = "";

			try
			{
				title = ExtractTitle(file);
			}
			catch (Exception ex)
			{
				errors++;
				Console.Error.WriteLine($"WARN: failed to read {file}: {ex.Message}");
			}

			if (title.Length > 0) clauses++; else headers++;
			total++;

			// Defensive: keep the report cleanly tab-delimited.
			title = Sanitize(title);
			Console.WriteLine($"{nodeId}\t{title}\t{dir}");
		}

		Console.Error.WriteLine(
			$"-- Processed {total} file(s): {clauses} clause(s), {headers} header node(s), {errors} error(s).");

		return 0;
	}

	/// <summary>
	/// Opens the .docx, reads word/document.xml, finds the first non-empty paragraph,
	/// and returns the title text if it matches the SECTION pattern. Otherwise returns "".
	/// </summary>
	private static string ExtractTitle(string docxPath)
	{
		using var zip = ZipFile.OpenRead(docxPath);
		var entry = zip.GetEntry("word/document.xml");
		if (entry == null) return "";

		using var stream = entry.Open();
		var settings = new XmlReaderSettings { IgnoreWhitespace = false };
		using var reader = XmlReader.Create(stream, settings);

		var paraText = new StringBuilder();
		bool inPara = false;

		while (reader.Read())
		{
			if (reader.NodeType == XmlNodeType.Element
				&& reader.LocalName == "p" && reader.NamespaceURI == W)
			{
				paraText.Clear();
				inPara = true;
			}
			else if (reader.NodeType == XmlNodeType.EndElement
					 && reader.LocalName == "p" && reader.NamespaceURI == W)
			{
				inPara = false;
				var text = paraText.ToString().Trim();
				if (text.Length > 0)
				{
					var sm = SectionRx.Match(text);
					return sm.Success ? sm.Groups[2].Value.Trim() : "";
				}
			}
			else if (inPara
					 && reader.NodeType == XmlNodeType.Element
					 && reader.NamespaceURI == W)
			{
				switch (reader.LocalName)
				{
					case "t":
						// <w:t> may be empty; ReadElementContentAsString handles that.
						paraText.Append(reader.ReadElementContentAsString());
						break;
					case "tab":
						paraText.Append('\t');
						break;
					case "br":
						paraText.Append(' ');
						break;
				}
			}
		}

		return "";
	}

	private static string Sanitize(string s)
		=> s.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
