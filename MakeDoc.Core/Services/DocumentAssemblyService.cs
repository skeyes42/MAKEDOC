using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace MakeDoc.Core.Services
{
	/// <summary>
	/// Stitches a sequence of DOCX byte arrays into a single assembled DOCX.
	///
	/// Algorithm:
	///   - The first blob is treated as the template. Its zip entries (styles,
	///     numbering, theme, headers/footers, rels, etc.) are copied verbatim
	///     into the output; only word/document.xml is replaced with the merged
	///     version.
	///   - For each subsequent blob, the body's child elements (minus any sectPr)
	///     are appended to the template body. No separators are injected -- any
	///     section/clause boundary formatting must live in the source DOCX.
	///   - The template's trailing sectPr is moved to the very end so the final
	///     document has well-formed section properties.
	///   - All ZIP entries except word/document.xml are copied verbatim from the
	///     template, including [Content_Types].xml. This preserves the exact bytes
	///     Word produced and avoids any XML round-trip artefacts (BOMs, declaration
	///     changes, namespace re-ordering) that cause Word's OPC parser to reject
	///     the file.
	///
	/// The entire operation is in-memory; no temporary files are written to disk.
	///
	/// Limitations (acceptable when all inputs share the same Word template):
	///   - styles.xml, numbering.xml, theme.xml are NOT merged across inputs.
	///   - word/_rels/document.xml.rels is NOT merged. Inputs with hyperlinks,
	///     images, footnotes, or embedded objects may produce dangling references.
	/// </summary>
	public class DocumentAssemblyService
	{
		private static readonly XNamespace W =
			"http://schemas.openxmlformats.org/wordprocessingml/2006/main";

		// XmlWriter settings that write UTF-8 WITHOUT a BOM.
		// Word-produced DOCX files have no BOM in any ZIP entry XML; adding one
		// causes Word's OPC parser to reject the file.
		private static readonly XmlWriterSettings NoBomXmlSettings = new()
		{
			Encoding           = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
			OmitXmlDeclaration = false,
			Indent             = false
		};

		/// <summary>
		/// Assembles the supplied DOCX blobs into a single DOCX byte array.
		/// Order of <paramref name="docxBlobs"/> determines the order of content
		/// in the assembled output.
		/// </summary>
		public byte[] Assemble(IList<byte[]> docxBlobs)
		{
			ArgumentNullException.ThrowIfNull(docxBlobs);
			if (docxBlobs.Count == 0)
				throw new ArgumentException(
					"At least one DOCX blob is required.", nameof(docxBlobs));

			// Build the merged document.xml from all blobs.
			XDocument mergedDoc = BuildMergedDocument(docxBlobs);

			using var templateMs = new MemoryStream(docxBlobs[0], writable: false);
			using var outMs      = new MemoryStream();

			using (var templateZip = new ZipArchive(templateMs, ZipArchiveMode.Read))
			using (var outZip      = new ZipArchive(outMs,      ZipArchiveMode.Create, leaveOpen: true))
			{
				foreach (var entry in templateZip.Entries)
				{
					var outEntry = outZip.CreateEntry(
						entry.FullName, CompressionLevel.Fastest);

					using var inStream  = entry.Open();
					using var outStream = outEntry.Open();

					if (entry.FullName == "word/document.xml")
						SaveNoBom(mergedDoc, outStream);
					else
						inStream.CopyTo(outStream);
				}
			}

			return outMs.ToArray();
		}

		// ─────────────────────────────────────────────────────────────
		// Private helpers
		// ─────────────────────────────────────────────────────────────

		private XDocument BuildMergedDocument(IList<byte[]> docxBlobs)
		{
			XDocument mainDoc = LoadDocumentXml(docxBlobs[0])
				?? throw new InvalidDataException(
					"word/document.xml not found in first DOCX blob; " +
					"input does not appear to be a valid Word file.");

			XElement body = mainDoc.Descendants(W + "body").First();

			XElement? trailingSectPr = body.Elements(W + "sectPr").LastOrDefault();
			trailingSectPr?.Remove();

			for (int i = 1; i < docxBlobs.Count; i++)
				AppendBodyFromBlob(body, docxBlobs[i]);

			SdtFixer.FixInlineTextSdts(body, W);
			SdtFixer.FixBlockLevelSdts(body, W);

			// LINQ to XML drops namespace declarations that aren't used in any
			// element or attribute during the load/save round-trip. If mc:Ignorable
			// still references those now-absent prefix names, Word 365 rejects the
			// file with "unreadable content". Removing mc:Ignorable is safe — the
			// extension namespaces it guards are either still present (and Word
			// knows them) or absent (and Word ignores unknown content by default).
			XNamespace MC = "http://schemas.openxmlformats.org/markup-compatibility/2006";
			mainDoc.Root?.Attribute(MC + "Ignorable")?.Remove();

			if (trailingSectPr != null)
				body.Add(trailingSectPr);

			return mainDoc;
		}

		private static void AppendBodyFromBlob(XElement targetBody, byte[] docxBlob)
		{
			XDocument? fragmentDoc = LoadDocumentXml(docxBlob);
			if (fragmentDoc is null) return;

			XElement fragmentBody = fragmentDoc.Descendants(W + "body").First();
			fragmentBody.Elements(W + "sectPr").Remove();

			foreach (XElement element in fragmentBody.Elements())
				targetBody.Add(new XElement(element));
		}

		/// <summary>
		/// Saves <paramref name="doc"/> to <paramref name="stream"/> using
		/// UTF-8 encoding WITHOUT a byte-order mark (BOM).
		/// </summary>
		private static void SaveNoBom(XDocument doc, Stream stream)
		{
			using var writer = XmlWriter.Create(stream, NoBomXmlSettings);
			doc.WriteTo(writer);
		}

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
	}
}
