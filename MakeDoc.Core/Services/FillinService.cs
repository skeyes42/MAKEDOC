using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using MakeDoc.Core.Models;

namespace MakeDoc.Core.Services
{
	/// <summary>
	/// Scans DOCX clause blobs for fill-in variables and substitutes
	/// user-supplied values at assembly time.
	///
	/// Fill-in identity: a content control (w:sdt) is a fill-in iff its
	/// w:sdtPr contains a w:alias element with a non-empty w:val attribute.
	/// The w:alias value is the variable name (e.g. "contractor_name").
	/// This matches Word's own "Title" field for content controls and is how
	/// the MAKEDOC clause authoring tool (FindSDTTagsApp / UploadNodes.R)
	/// marks fill-in variables.
	///
	/// Two SDTs with the same alias represent the same variable and receive
	/// the same substitution value.
	/// </summary>
	public class FillinService
	{
		private static readonly XNamespace W =
			"http://schemas.openxmlformats.org/wordprocessingml/2006/main";

		// UTF-8 without BOM -- matches the encoding used by Word-produced DOCX files.
		private static readonly XmlWriterSettings NoBomXmlSettings = new()
		{
			Encoding           = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
			OmitXmlDeclaration = false,
			Indent             = false
		};

		// ─────────────────────────────────────────────────────────────
		// SCAN -- single blob
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// Returns every fill-in variable name found in <paramref name="docxBlob"/>,
		/// in document order. Duplicates are included; call Distinct() if needed.
		/// Returns an empty list when the blob is null/empty or cannot be parsed.
		/// </summary>
		public IReadOnlyList<string> ScanFillins(byte[] docxBlob)
		{
			if (docxBlob is null || docxBlob.Length == 0)
				return Array.Empty<string>();

			var doc = LoadDocumentXml(docxBlob);
			if (doc is null) return Array.Empty<string>();

			var names = new List<string>();
			foreach (var sdt in doc.Descendants(W + "sdt"))
			{
				var alias = GetSdtAlias(sdt);
				if (!string.IsNullOrWhiteSpace(alias))
					names.Add(alias);
			}

			return names;
		}

		// ─────────────────────────────────────────────────────────────
		// SCAN -- multiple blobs -> distinct ordered set
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// Scans all supplied blobs and returns the distinct set of fill-in
		/// variable names, preserving first-occurrence order across the sequence.
		/// </summary>
		public IReadOnlyList<string> ScanFillins(IEnumerable<byte[]> docxBlobs)
		{
			var seen    = new HashSet<string>(StringComparer.Ordinal);
			var ordered = new List<string>();

			foreach (var blob in docxBlobs)
			{
				foreach (var name in ScanFillins(blob))
				{
					if (seen.Add(name))
						ordered.Add(name);
				}
			}

			return ordered;
		}

		// ─────────────────────────────────────────────────────────────
		// SUBSTITUTE -- returns a new blob with values replaced
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// Returns a new DOCX blob with every SDT whose w:alias matches
		/// a key in <paramref name="values"/> replaced by the corresponding value.
		/// SDTs not present in <paramref name="values"/> are left untouched.
		/// The SDT wrapper element is preserved so the document stays
		/// Word-compatible and re-scannable.
		/// </summary>
		public byte[] SubstituteFillins(byte[] docxBlob,
			IReadOnlyDictionary<string, string> values)
		{
			if (docxBlob is null || docxBlob.Length == 0) return docxBlob;
			if (values   is null || values.Count == 0)   return docxBlob;

			using var inMs  = new MemoryStream(docxBlob, writable: false);
			using var outMs = new MemoryStream();

			using (var inZip  = new ZipArchive(inMs,  ZipArchiveMode.Read))
			using (var outZip = new ZipArchive(outMs, ZipArchiveMode.Create, leaveOpen: true))
			{
				foreach (var entry in inZip.Entries)
				{
					var outEntry = outZip.CreateEntry(
						entry.FullName, CompressionLevel.Fastest);

					using var inStream  = entry.Open();
					using var outStream = outEntry.Open();

					if (entry.FullName == "word/document.xml")
					{
						var doc = XDocument.Load(inStream);
						SubstituteInDocument(doc, values);
						using var writer = XmlWriter.Create(outStream, NoBomXmlSettings);
						doc.WriteTo(writer);
					}
					else
					{
						inStream.CopyTo(outStream);
					}
				}
			}

			return outMs.ToArray();
		}

		// ─────────────────────────────────────────────────────────────
		// PLAIN TEXT SUBSTITUTE -- returns a new blob with a literal
		// pattern replaced inside w:t text elements
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// Substitutes all occurrences of <paramref name="pattern"/> (e.g. "{SecNo}")
		/// inside word/document.xml with <paramref name="replacement"/>. Returns a
		/// new DOCX blob; the original is not modified.
		///
		/// Works at the paragraph level so patterns that Word has split across
		/// multiple w:t elements (a common Word behaviour) are still matched and
		/// replaced correctly.
		/// </summary>
		public byte[] SubstitutePlainText(byte[] docxBlob, string pattern, string replacement)
		{
			if (docxBlob  is null || docxBlob.Length == 0) return docxBlob;
			if (string.IsNullOrEmpty(pattern))              return docxBlob;

			using var inMs  = new MemoryStream(docxBlob, writable: false);
			using var outMs = new MemoryStream();

			using (var inZip  = new ZipArchive(inMs,  ZipArchiveMode.Read))
			using (var outZip = new ZipArchive(outMs, ZipArchiveMode.Create, leaveOpen: true))
			{
				foreach (var entry in inZip.Entries)
				{
					var outEntry = outZip.CreateEntry(
						entry.FullName, CompressionLevel.Fastest);

					using var inStream  = entry.Open();
					using var outStream = outEntry.Open();

					if (entry.FullName == "word/document.xml")
					{
						var doc = XDocument.Load(inStream);
						SubstitutePlainTextInDocument(doc, pattern, replacement);
						using var writer = XmlWriter.Create(outStream, NoBomXmlSettings);
						doc.WriteTo(writer);
					}
					else
					{
						inStream.CopyTo(outStream);
					}
				}
			}

			return outMs.ToArray();
		}

		/// <summary>
		/// Scans every w:p in <paramref name="doc"/> for <paramref name="pattern"/>.
		/// The scan concatenates the text of all w:t descendants within each paragraph
		/// so that patterns split across multiple w:t elements are still found.
		/// When a match is found the replacement is written into the first w:t that
		/// contributes to the match; any remaining w:t elements whose text was part
		/// of the split pattern are cleared (text after the match end is preserved).
		/// </summary>
		private static void SubstitutePlainTextInDocument(
			XDocument doc, string pattern, string replacement)
		{
			foreach (var para in doc.Descendants(W + "p").ToList())
			{
				var wts = para.Descendants(W + "t").ToList();
				if (wts.Count == 0) continue;

				// Build a position map: (startPos, endPos, w:t element)
				var segments = new List<(int Start, int End, XElement Wt)>();
				int pos = 0;
				foreach (var wt in wts)
				{
					string v = wt.Value;
					segments.Add((pos, pos + v.Length, wt));
					pos += v.Length;
				}

				string combined = string.Concat(wts.Select(t => t.Value));

				// Replace every occurrence (handles repeated tags in the same paragraph).
				int searchFrom = 0;
				while (true)
				{
					int matchStart = combined.IndexOf(
						pattern, searchFrom, StringComparison.Ordinal);
					if (matchStart < 0) break;

					int matchEnd         = matchStart + pattern.Length;
					bool replacementDone = false;

					foreach (var (segStart, segEnd, wt) in segments)
					{
						if (segEnd <= matchStart || segStart >= matchEnd) continue;

						int prefixLen   = Math.Max(0, matchStart - segStart);
						int suffixStart = Math.Min(segEnd, matchEnd) - segStart;

						string prefix = wt.Value[..prefixLen];
						string suffix = suffixStart < wt.Value.Length
							? wt.Value[suffixStart..]
							: string.Empty;

						wt.Value = replacementDone
							? suffix                          // clear matched portion
							: prefix + replacement + suffix;  // place replacement

						replacementDone = true;
					}

					// Advance past this match (using replacement length in combined).
					// Rebuild combined from updated w:t values for the next search.
					combined   = string.Concat(wts.Select(t => t.Value));
					searchFrom = matchStart + replacement.Length;
				}
			}
		}

		// ─────────────────────────────────────────────────────────────
		// LINE ITEM TABLE SUBSTITUTE -- replaces {lineitem} paragraphs
		// with an OpenXML table and a Grand Total paragraph
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// Returns a new DOCX blob with every paragraph whose plain text
		/// contains "{lineitem}" replaced by a formatted line-item table
		/// followed by a bold Grand Total paragraph.
		/// If the tag is not present the original blob is returned unchanged.
		/// </summary>
		public byte[] SubstituteLineItemTable(byte[] docxBlob,
			IReadOnlyList<LineItem> items)
		{
			if (docxBlob is null || docxBlob.Length == 0) return docxBlob;
			items ??= Array.Empty<LineItem>();

			using var inMs  = new MemoryStream(docxBlob, writable: false);
			using var outMs = new MemoryStream();

			using (var inZip  = new ZipArchive(inMs,  ZipArchiveMode.Read))
			using (var outZip = new ZipArchive(outMs, ZipArchiveMode.Create,
			                                         leaveOpen: true))
			{
				foreach (var entry in inZip.Entries)
				{
					var outEntry = outZip.CreateEntry(
						entry.FullName, CompressionLevel.Fastest);

					using var inStream  = entry.Open();
					using var outStream = outEntry.Open();

					if (entry.FullName == "word/document.xml")
					{
						var doc = XDocument.Load(inStream);
						SubstituteLineItemTableInDocument(doc, items);
						using var writer = XmlWriter.Create(outStream, NoBomXmlSettings);
						doc.WriteTo(writer);
					}
					else
					{
						inStream.CopyTo(outStream);
					}
				}
			}

			return outMs.ToArray();
		}

		private static void SubstituteLineItemTableInDocument(
			XDocument doc, IReadOnlyList<LineItem> items)
		{
			const string Tag = "{lineitem}";

			foreach (var para in doc.Descendants(W + "p").ToList())
			{
				string combined = string.Concat(
					para.Descendants(W + "t").Select(t => t.Value));

				if (!combined.Contains(Tag, StringComparison.OrdinalIgnoreCase))
					continue;

				// Insert table then grand-total paragraph in place of this paragraph.
				para.AddBeforeSelf(BuildLineItemTable(items));
				para.AddBeforeSelf(BuildGrandTotalParagraph(items));
				para.Remove();
			}
		}

		private static XElement BuildLineItemTable(IReadOnlyList<LineItem> items)
		{
			static XElement MakeCell(string text, bool bold = false)
			{
				var run = new XElement(W + "r");
				if (bold)
					run.Add(new XElement(W + "rPr", new XElement(W + "b")));
				run.Add(new XElement(W + "t",
					new XAttribute(XNamespace.Xml + "space", "preserve"),
					text));
				return new XElement(W + "tc",
					new XElement(W + "p", run));
			}

			var tblPr = new XElement(W + "tblPr",
				new XElement(W + "tblBorders",
					new XElement(W + "top",
						new XAttribute(W + "val", "single"),
						new XAttribute(W + "sz",  "4"),
						new XAttribute(W + "space", "0"),
						new XAttribute(W + "color", "auto")),
					new XElement(W + "left",
						new XAttribute(W + "val", "single"),
						new XAttribute(W + "sz",  "4"),
						new XAttribute(W + "space", "0"),
						new XAttribute(W + "color", "auto")),
					new XElement(W + "bottom",
						new XAttribute(W + "val", "single"),
						new XAttribute(W + "sz",  "4"),
						new XAttribute(W + "space", "0"),
						new XAttribute(W + "color", "auto")),
					new XElement(W + "right",
						new XAttribute(W + "val", "single"),
						new XAttribute(W + "sz",  "4"),
						new XAttribute(W + "space", "0"),
						new XAttribute(W + "color", "auto")),
					new XElement(W + "insideH",
						new XAttribute(W + "val", "single"),
						new XAttribute(W + "sz",  "4"),
						new XAttribute(W + "space", "0"),
						new XAttribute(W + "color", "auto")),
					new XElement(W + "insideV",
						new XAttribute(W + "val", "single"),
						new XAttribute(W + "sz",  "4"),
						new XAttribute(W + "space", "0"),
						new XAttribute(W + "color", "auto"))
				)
			);

			var headerRow = new XElement(W + "tr",
				MakeCell("Line #",      bold: true),
				MakeCell("Description", bold: true),
				MakeCell("NAICS",       bold: true),
				MakeCell("Qty",         bold: true),
				MakeCell("Unit",        bold: true),
				MakeCell("Unit Price",  bold: true),
				MakeCell("Extended",    bold: true)
			);

			var tbl = new XElement(W + "tbl", tblPr, headerRow);

			foreach (var li in items)
			{
				decimal extended = (decimal)li.Quantity * (decimal)li.UnitPrice;
				tbl.Add(new XElement(W + "tr",
					MakeCell(li.LineNum.ToString()),
					MakeCell(li.Description),
					MakeCell(li.NAICS == 0 ? "" : li.NAICS.ToString()),
					MakeCell(((decimal)li.Quantity).ToString("G29")),
					MakeCell(li.Unit),
					MakeCell(((decimal)li.UnitPrice).ToString("C")),
					MakeCell(extended.ToString("C"))
				));
			}

			return tbl;
		}

		private static XElement BuildGrandTotalParagraph(IReadOnlyList<LineItem> items)
		{
			decimal grandTotal = items.Sum(
				li => (decimal)li.Quantity * (decimal)li.UnitPrice);

			return new XElement(W + "p",
				new XElement(W + "r",
					new XElement(W + "rPr", new XElement(W + "b")),
					new XElement(W + "t",
						new XAttribute(XNamespace.Xml + "space", "preserve"),
						$"Grand Total: {grandTotal:C}")
				)
			);
		}

		// ─────────────────────────────────────────────────────────────
		// Private helpers
		// ─────────────────────────────────────────────────────────────

		private static XDocument? LoadDocumentXml(byte[] docxBlob)
		{
			try
			{
				using var ms      = new MemoryStream(docxBlob, writable: false);
				using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
				var entry = archive.GetEntry("word/document.xml");
				if (entry is null) return null;
				using var stream = entry.Open();
				return XDocument.Load(stream);
			}
			catch { return null; }
		}

		/// <summary>
		/// Returns the w:alias/@w:val from the SDT's w:sdtPr, or null when absent.
		/// This is the content control's "Title" as shown in Word's Developer tab,
		/// and is the canonical fill-in variable name used by MAKEDOC.
		/// </summary>
		private static string? GetSdtAlias(XElement sdt)
		{
			var sdtPr = sdt.Element(W + "sdtPr");
			if (sdtPr is null) return null;
			var alias = sdtPr.Element(W + "alias");
			var val   = alias?.Attribute(W + "val")?.Value?.Trim();
			return string.IsNullOrWhiteSpace(val) ? null : val;
		}

		/// <summary>
		/// Walks every SDT in <paramref name="doc"/> that has a w:alias; when the
		/// alias matches a key in <paramref name="values"/>, replaces the SDT content
		/// with a single run containing the substitution text. The SDT element itself
		/// (and its w:sdtPr) are preserved.
		/// </summary>
		private static void SubstituteInDocument(XDocument doc,
			IReadOnlyDictionary<string, string> values)
		{
			foreach (var sdt in doc.Descendants(W + "sdt").ToList())
			{
				var varName = GetSdtAlias(sdt);
				if (varName is null) continue;
				if (!values.TryGetValue(varName, out var replacement)) continue;

				var content = sdt.Element(W + "sdtContent");
				if (content is null) continue;

				// Carry run properties from the first existing run, if present,
				// so the substituted text inherits the original font/size/bold.
				var rPr = content.Descendants(W + "r")
				                 .FirstOrDefault()
				                 ?.Element(W + "rPr");

				content.RemoveNodes();

				var run = new XElement(W + "r");
				if (rPr is not null) run.Add(new XElement(rPr));
				run.Add(new XElement(W + "t",
					new XAttribute(XNamespace.Xml + "space", "preserve"),
					replacement));

				content.Add(new XElement(W + "p", run));

				// Remove the "showing placeholder" flag. If w:showingPlcHdr is
				// present in w:sdtPr, Word displays the placeholder text instead
				// of the actual w:sdtContent -- the substitution would be invisible.
				sdt.Element(W + "sdtPr")?.Element(W + "showingPlcHdr")?.Remove();
			}
		}
	}
}
