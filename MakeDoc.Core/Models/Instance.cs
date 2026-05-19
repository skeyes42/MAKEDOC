namespace MakeDoc.Core.Models
{
    public class Instance
    {
        public string  InstanceID    { get; set; } = string.Empty;
        public string  DocTypeID     { get; set; } = string.Empty;  // FK → DocType
        public string? PrevEditionID { get; set; }                  // FK → Instance (prior edition)
        public string? BuildFromID   { get; set; }                  // FK → Instance (cloned from)
        public string  GeneratedDate { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty ;

        public string OutputFile { get; set; } = string.Empty;

        // ── Archive fields ────────────────────────────────────────────
        public bool    IsArchived  { get; set; } = false;
        public string? ArchiveDate { get; set; }                    // set when archived, cleared on un-archive

        // ── JSON columns — stored as raw strings, parsed when needed ──
        public string? InclusionData { get; set; }  // {"tags": ["DEI"], "tier": "standard"}
        public string? FillinData    { get; set; }  // {"delivery_days": "30", ...}
        public string? NodeList      { get; set; }  // ["NL-0001", "NL-0003", "NL-0005", ...]

        // ── Display-only — populated by JOIN queries, not stored ──────
        public string? DocTypeName { get; set; }
        public string? DocTypeTier { get; set; }
    }
}
