using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MakeDoc.Core.Models
{
	public class LineItem
	{
		public string  LineItemID { get; set; } = string.Empty;

		/// <summary>Populated for canonical (doc-type-level) line items. Null for instance overrides.</summary>
		public string? DocTypeID  { get; set; }

		/// <summary>Populated for instance-level line item overrides. Null for canonical items.</summary>
		public string? InstanceID { get; set; }

		public int    LineNum     { get; set; }
		public string Description { get; set; } = string.Empty;
		public int    NAICS       { get; set; }
		public string Unit        { get; set; } = string.Empty;
		public double Quantity    { get; set; }
		public double UnitPrice   { get; set; }
	}
}
