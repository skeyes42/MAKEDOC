// SdtFixer.cs  — drop this into your DocxAssembler project
// Call SdtFixer.FixInlineTextSdts(body, w) after all body content has been merged,
// just before you add the final sectPr back.

using System.Xml.Linq;
using System.Linq;

namespace MakeDoc.Core
{
    public static class SdtFixer
    {
        /// <summary>
        /// Word requires that text-type inline SDTs (w:sdt with w:text in w:sdtPr)
        /// contain only run-level content inside w:sdtContent — NOT paragraphs.
        /// When clauses are assembled the content controls sometimes get a spurious
        /// w:p wrapper.  This method removes that wrapper and lifts the runs up.
        /// </summary>
        public static void FixInlineTextSdts(XElement body, XNamespace w)
        {
            // Find every sdt anywhere in the body tree
            var sdts = body.Descendants(w + "sdt").ToList();

            foreach (var sdt in sdts)
            {
                var sdtPr      = sdt.Element(w + "sdtPr");
                var sdtContent = sdt.Element(w + "sdtContent");

                if (sdtPr == null || sdtContent == null) continue;

                // Is it a text-type (inline) SDT?
                bool isTextType = sdtPr.Element(w + "text") != null;
                if (!isTextType) continue;

                // Does sdtContent contain any w:p children?
                var paragraphs = sdtContent.Elements(w + "p").ToList();
                if (!paragraphs.Any()) continue;

                // Lift run-level content out of the paragraphs
                var runs = paragraphs
                    .SelectMany(p => p.Elements())
                    .Where(e => e.Name != w + "pPr")  // discard paragraph formatting
                    .Select(e => new XElement(e))       // deep-copy
                    .ToList();

                // Replace sdtContent children with the extracted runs
                sdtContent.RemoveNodes();
                foreach (var run in runs)
                    sdtContent.Add(run);
            }
        }

        /// <summary>
        /// Word requires block-level SDTs (direct children of w:body) to contain
        /// only block-level children (w:p, w:tbl) inside w:sdtContent — NOT bare
        /// runs. LINQ to XML round-trips can leave the placeholder run unwrapped.
        /// This method wraps any bare runs in a w:p so Word accepts the file.
        /// </summary>
        public static void FixBlockLevelSdts(XElement body, XNamespace w)
        {
            foreach (var sdt in body.Elements(w + "sdt").ToList())
            {
                var sdtContent = sdt.Element(w + "sdtContent");
                if (sdtContent == null) continue;

                bool hasBareRuns = sdtContent.Elements()
                    .Any(e => e.Name == w + "r" || e.Name == w + "hyperlink");
                if (!hasBareRuns) continue;

                var children = sdtContent.Elements().ToList();
                sdtContent.RemoveNodes();
                var p = new XElement(w + "p");
                foreach (var child in children)
                    p.Add(new XElement(child));
                sdtContent.Add(p);
            }
        }
    }
}
