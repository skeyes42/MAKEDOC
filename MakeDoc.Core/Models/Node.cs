namespace MakeDoc.Core.Models
{
    public class Node
    {
        public string NodeID { get; set; } = string.Empty;
        public string NodeType { get; set; } = string.Empty;
        public int Sequence { get; set; } = 0;
        public string? Title { get; set; }
        public string? IsUserClause { get; set; }
        public string? IsSpecialClause { get; set; }
        public string? DerivedFrom { get; set; }
        public byte[]? Content { get; set; }  // seed DOCX binary for Template nodes
    }

    // Keeps NodeType values in one place — no magic strings scattered in code
    public static class NodeTypes
    {
        public const string Document = "Document";
        public const string Section = "Section";
        public const string Subsection = "Subsection";
        public const string Clause = "Clause";
        public const string HeaderNode = "HeaderNode";
    }
}
