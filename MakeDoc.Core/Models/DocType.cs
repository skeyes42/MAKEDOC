namespace MakeDoc.Core.Models
{
    public class DocType
    {
        public string DocTypeID { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;

        // Stored as raw JSON string: ["DEI", "HAZMAT", "INTERNATIONAL"]
        public string? InclusionTags { get; set; }

        public string? HeaderNodeID { get; set; }  // FK → Node (NodeType = HeaderNode)

        public string? Tier { get; set; }  // e.g. "micro", "standard", "complex"

	}
}