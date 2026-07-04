namespace MakeDoc.Core.Models
{
	/// <summary>
	/// One entry in the mutable transient node list maintained by AssemblyForm
	/// during a canonical or build-from assembly session.
	///
	/// The list is never persisted directly; it drives the display ListView,
	/// the context-menu operations, and the Generate Document procedure.
	/// </summary>
	public class TransientNodeEntry
	{
		/// <summary>
		/// The node's identifier.
		/// <list type="bullet">
		///   <item>Canonical nodes: the persisted NodeID from the NodeHierarchy walk.</item>
		///   <item>User-inserted clauses: a temporary "TMP-{8 hex chars}" key assigned
		///     at insertion time. At Generate time the system writes a permanent UC- row
		///     and uses that ID in the Instance.NodeList; the TMP key is never stored.</item>
		///   <item>Special-clause (inclusion-tag) nodes: the persisted NodeID from the
		///     node group's NodeHierarchy walk.</item>
		/// </list>
		/// </summary>
		public string NodeID { get; set; } = string.Empty;

		public string Title { get; set; } = string.Empty;

		public string NodeType { get; set; } = string.Empty;

		/// <summary>
		/// True for clauses the user opened from a file during this session
		/// (Insert User Clause context-menu operation). These always produce a
		/// UC- row when the document is generated.
		/// </summary>
		public bool IsUserInsertion { get; set; }

		/// <summary>
		/// True for nodes sourced from an inclusion-tag node group
		/// (Insert Special Clauses). Special-clause nodes may not be edited —
		/// the Edit context-menu item is disabled for them.
		/// </summary>
		public bool IsSpecialClause { get; set; }

		/// <summary>
		/// True when the user has opened this clause in Word and saved a change
		/// back this session. At Generate time an IsEdited clause produces a new
		/// UC- row (UC- rows are never updated in place, per spec).
		/// </summary>
		public bool IsEdited { get; set; }

		/// <summary>
		/// The in-session DOCX blob.
		/// <list type="bullet">
		///   <item>User insertions: set at insertion time from the chosen file.</item>
		///   <item>Edited clauses: set after the user saves in Word.</item>
		///   <item>Canonical / unedited nodes: null — content is loaded from NodeService
		///     on demand (at Browse time and at Generate time).</item>
		/// </list>
		/// </summary>
		public byte[]? EditedContent { get; set; }

		/// <summary>
		/// Informational: the filesystem path the user-inserted clause was loaded from.
		/// Not persisted.
		/// </summary>
		public string? SourcePath { get; set; }

		/// <summary>
		/// Short status badge shown in the ListView Status column.
		/// </summary>
		public string Badge =>
			IsUserInsertion ? "[user]"    :
			IsSpecialClause ? "[special]" :
			IsEdited        ? "[edited]"  :
			string.Empty;
	}
}
