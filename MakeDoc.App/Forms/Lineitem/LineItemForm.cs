using MakeDoc.Core.Data;
using MakeDoc.Core.Models;

namespace MakeDoc.App.Forms.Lineitem
{
	public partial class LineItemForm : Form
	{
		// ── Services ──────────────────────────────────────────────────
		private readonly MakDocDb _db;

		// ── State ─────────────────────────────────────────────────────
		private List<DocType> _docTypes  = new();

		/// <summary>
		/// When non-null the form is in instance mode: line items are loaded
		/// from and saved to this InstanceID rather than a DocTypeID.
		/// </summary>
		private string? _instanceId = null;

		// ── Controls ──────────────────────────────────────────────────
		private ComboBox     _cboInstance = null!;
		private DataGridView _grid        = null!;
		private TextBox      _txtTotal    = null!;
		private Label        _lblStatus   = null!;

		// ─────────────────────────────────────────────────────────────
		// Constructor — doc-type mode (Tools > Line Items)
		// ─────────────────────────────────────────────────────────────
		public LineItemForm()
		{
			InitializeComponent();
			BuildUI();

			try
			{
				_db = new MakDocDb();
				RefreshDocTypes();
				SetStatus("Ready.", success: true);
			}
			catch (Exception ex)
			{
				_db = null!;
				SetStatus($"Database error: {ex.Message}", success: false);
			}
		}

		// ─────────────────────────────────────────────────────────────
		// Constructor — instance mode (build-from review modal)
		//
		// Pre-loads the line items that were just copied to the new
		// instance. The user can view/edit them and Save All, or close
		// without saving to keep the items exactly as copied.
		// ─────────────────────────────────────────────────────────────
		public LineItemForm(string instanceId)
		{
			_instanceId = instanceId;
			InitializeComponent();
			BuildUI();

			try
			{
				_db = new MakDocDb();
				LoadLineItemsForInstance(instanceId);
				SetStatus(
					"Line items copied from source. Edit if needed, then Save All — or close to keep as-is.",
					success: true);
			}
			catch (Exception ex)
			{
				_db = null!;
				SetStatus($"Database error: {ex.Message}", success: false);
			}
		}

		// ─────────────────────────────────────────────────────────────
		// UI Construction
		// ─────────────────────────────────────────────────────────────
		private void BuildUI()
		{
			Text          = _instanceId is not null
				? "MAKEDOC — Review Line Items"
				: "MAKEDOC — Line Item Manager";
			Size          = new Size(1060, 580);
			MinimumSize   = new Size(800, 440);
			StartPosition = FormStartPosition.CenterScreen;
			BackColor     = Color.White;
			Font          = new Font("Segoe UI", 9.5f);

			var outer = new TableLayoutPanel
			{
				Dock        = DockStyle.Fill,
				ColumnCount = 1,
				RowCount    = 5,
				Padding     = new Padding(24)
			};
			outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // header
			outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));   // instance picker
			outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // grid
			outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));   // bottom bar
			outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // status
			Controls.Add(outer);

			BuildHeader(outer);
			BuildInstancePicker(outer);
			BuildGrid(outer);
			BuildBottomBar(outer);
			BuildStatusBar(outer);
		}

		private void BuildHeader(TableLayoutPanel parent)
		{
			var panel = new Panel { Dock = DockStyle.Fill };
			panel.Controls.Add(new Label
			{
				Text      = "Line Item Manager",
				Font      = new Font("Segoe UI", 14f, FontStyle.Regular),
				ForeColor = Color.FromArgb(30, 30, 30),
				AutoSize  = true,
				Location  = new Point(0, 2)
			});
			panel.Controls.Add(new Label
			{
				Text      = _instanceId is not null
					? "Review and optionally edit line items for the newly created document."
					: "Manage line items for assembled document instances.",
				Font      = new Font("Segoe UI", 9f),
				ForeColor = Color.FromArgb(110, 110, 110),
				AutoSize  = true,
				Location  = new Point(0, 32)
			});
			parent.Controls.Add(panel, 0, 0);
		}

		private void BuildInstancePicker(TableLayoutPanel parent)
		{
			var panel = new Panel { Dock = DockStyle.Fill };

			if (_instanceId is not null)
			{
				// Instance mode: no doc-type picker needed; show a descriptive label.
				panel.Controls.Add(new Label
				{
					Text      = "Line items copied from the source document. Edit as needed, then Save All.",
					Font      = new Font("Segoe UI", 9f),
					ForeColor = Color.FromArgb(80, 80, 80),
					AutoSize  = true,
					Location  = new Point(0, 11)
				});
			}
			else
			{
				// Doc-type mode: combo picks which document type to manage.
				panel.Controls.Add(new Label
				{
					Text      = "Document Type:",
					Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
					ForeColor = Color.FromArgb(80, 80, 80),
					AutoSize  = true,
					Location  = new Point(0, 11)
				});

				_cboInstance = new ComboBox
				{
					Width         = 520,
					Location      = new Point(112, 7),
					Font          = new Font("Segoe UI", 9.5f),
					DropDownStyle = ComboBoxStyle.DropDownList
				};
				_cboInstance.SelectedIndexChanged += OnInstanceSelected;
				panel.Controls.Add(_cboInstance);
			}

			parent.Controls.Add(panel, 0, 1);
		}

		private void BuildGrid(TableLayoutPanel parent)
		{
			_grid = new DataGridView
			{
				Dock                        = DockStyle.Fill,
				AllowUserToAddRows          = false,
				AllowUserToDeleteRows       = false,
				RowHeadersWidth             = 30,
				ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
				SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
				BorderStyle                 = BorderStyle.FixedSingle,
				BackgroundColor             = Color.White,
				GridColor                   = Color.FromArgb(220, 220, 220),
				Font                        = new Font("Segoe UI", 9.5f),
				AutoSizeRowsMode            = DataGridViewAutoSizeRowsMode.AllCells
			};

			_grid.Columns.Add(new DataGridViewTextBoxColumn
				{ Name = "LineItemID",    HeaderText = "ID",          ReadOnly = true, Visible = false });
			_grid.Columns.Add(new DataGridViewTextBoxColumn
				{ Name = "LineNum",       HeaderText = "Line #",                       Width   = 60   });
			_grid.Columns.Add(new DataGridViewTextBoxColumn
				{ Name = "Description",  HeaderText = "Description",                  Width   = 320  });
			_grid.Columns.Add(new DataGridViewTextBoxColumn
				{ Name = "NAICS",        HeaderText = "NAICS",                        Width   = 80   });
			_grid.Columns.Add(new DataGridViewTextBoxColumn
				{ Name = "Unit",         HeaderText = "Unit",                         Width   = 70   });
			_grid.Columns.Add(new DataGridViewTextBoxColumn
				{ Name = "Quantity",     HeaderText = "Qty",                          Width   = 65   });
			_grid.Columns.Add(new DataGridViewTextBoxColumn
				{ Name = "UnitPrice",    HeaderText = "Unit Price",                   Width   = 90   });
			_grid.Columns.Add(new DataGridViewTextBoxColumn
				{ Name = "ExtendedPrice", HeaderText = "Extended",   ReadOnly = true, Width   = 100  });

			_grid.CellEndEdit += OnCellEndEdit;

			parent.Controls.Add(_grid, 0, 2);
		}

		private void BuildBottomBar(TableLayoutPanel parent)
		{
			var panel = new Panel { Dock = DockStyle.Fill };

			var btnAddRow = MakeButton("Add Row",    Color.FromArgb(60, 60, 60));
			btnAddRow.Location = new Point(0, 4);
			btnAddRow.Click   += OnAddRowClicked;
			panel.Controls.Add(btnAddRow);

			var btnSaveAll = MakeButton("Save All",  Color.FromArgb(30, 30, 30));
			btnSaveAll.Location = new Point(100, 4);
			btnSaveAll.Click   += OnSaveAllClicked;
			panel.Controls.Add(btnSaveAll);

			var btnDeleteRow = MakeButton("Delete Row", Color.FromArgb(180, 40, 40));
			btnDeleteRow.Location = new Point(200, 4);
			btnDeleteRow.Click   += OnDeleteRowClicked;
			panel.Controls.Add(btnDeleteRow);

			panel.Controls.Add(new Label
			{
				Text      = "Total:",
				Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
				ForeColor = Color.FromArgb(80, 80, 80),
				AutoSize  = true,
				Location  = new Point(340, 12)
			});

			_txtTotal = new TextBox
			{
				Location  = new Point(388, 7),
				Width     = 130,
				Font      = new Font("Segoe UI", 9.5f),
				ReadOnly  = true,
				BackColor = Color.FromArgb(245, 245, 245),
				Text      = "$0.00"
			};
			panel.Controls.Add(_txtTotal);

			parent.Controls.Add(panel, 0, 3);
		}

		// ── Status Bar ────────────────────────────────────────────────
		private void BuildStatusBar(TableLayoutPanel parent)
		{
			_lblStatus = new Label
			{
				Text      = "Initializing...",
				Dock      = DockStyle.Fill,
				Font      = new Font("Segoe UI", 8.5f),
				ForeColor = Color.FromArgb(130, 130, 130),
				TextAlign = ContentAlignment.MiddleLeft
			};
			parent.Controls.Add(_lblStatus, 0, 4);
		}

		private static Button MakeButton(string text, Color back)
		{
			var btn = new Button
			{
				Text      = text,
				Size      = new Size(90, 32),
				FlatStyle = FlatStyle.Flat,
				BackColor = back,
				ForeColor = Color.White,
				Font      = new Font("Segoe UI", 9f),
				Cursor    = Cursors.Hand
			};
			btn.FlatAppearance.BorderSize = 0;
			return btn;
		}

		// ─────────────────────────────────────────────────────────────
		// Data Loading
		// ─────────────────────────────────────────────────────────────
		private void RefreshDocTypes()
		{
			_docTypes = _db.GetAllDocTypes();

			_cboInstance.Items.Clear();
			_cboInstance.Items.Add(new DocTypeItem(null, "(select a document type)"));
			foreach (var dt in _docTypes)
			{
				string tier  = dt.Tier is not null ? $" [{dt.Tier}]" : "";
				string label = $"{dt.Name}{tier}";
				_cboInstance.Items.Add(new DocTypeItem(dt.DocTypeID, label));
			}
			_cboInstance.SelectedIndex = 0;
		}

		private void LoadLineItemsForDocType(string docTypeId)
		{
			_grid.Rows.Clear();

			var items = _db.GetLineItemsForDocType(docTypeId);
			foreach (var li in items)
				_grid.Rows.Add(
					li.LineItemID,
					li.LineNum,
					li.Description,
					li.NAICS,
					li.Unit,
					li.Quantity,
					li.UnitPrice,
					(li.Quantity * li.UnitPrice).ToString("F2"));

			ComputeTotals();
			SetStatus($"{items.Count} line item(s) loaded.", success: true);
		}

		private void LoadLineItemsForInstance(string instanceId)
		{
			_grid.Rows.Clear();

			var items = _db.GetLineItemsForInstance(instanceId);
			foreach (var li in items)
				_grid.Rows.Add(
					li.LineItemID,
					li.LineNum,
					li.Description,
					li.NAICS,
					li.Unit,
					li.Quantity,
					li.UnitPrice,
					(li.Quantity * li.UnitPrice).ToString("F2"));

			ComputeTotals();
			SetStatus($"{items.Count} line item(s) loaded.", success: true);
		}

		// ─────────────────────────────────────────────────────────────
		// Totals
		// ─────────────────────────────────────────────────────────────
		private void ComputeTotals()
		{
			decimal total = 0;

			foreach (DataGridViewRow row in _grid.Rows)
			{
				if (row.IsNewRow) continue;

				decimal.TryParse(Convert.ToString(row.Cells["Quantity"].Value),  out decimal qty);
				decimal.TryParse(Convert.ToString(row.Cells["UnitPrice"].Value), out decimal price);

				decimal extended = qty * price;
				row.Cells["ExtendedPrice"].Value = extended.ToString("F2");
				total += extended;
			}

			_txtTotal.Text = total.ToString("C");
		}

		// ─────────────────────────────────────────────────────────────
		// Event Handlers
		// ─────────────────────────────────────────────────────────────
		private void OnInstanceSelected(object? sender, EventArgs e)
		{
			var item = _cboInstance.SelectedItem as DocTypeItem;
			if (item?.DocTypeID is null)
			{
				_grid.Rows.Clear();
				_txtTotal.Text = "$0.00";
				SetStatus("Select a document type to manage its line items.", success: true);
				return;
			}

			LoadLineItemsForDocType(item.DocTypeID);
		}

		private void OnCellEndEdit(object? sender, DataGridViewCellEventArgs e)
		{
			string colName = _grid.Columns[e.ColumnIndex].Name;
			if (colName is "Quantity" or "UnitPrice")
				ComputeTotals();
		}

		private void OnAddRowClicked(object? sender, EventArgs e)
		{
			if (_instanceId is null)
			{
				// Doc-type mode: a document type must be selected first.
				var dt = _cboInstance?.SelectedItem as DocTypeItem;
				if (dt?.DocTypeID is null)
				{
					SetStatus("Select a document type before adding a row.", success: false);
					return;
				}
			}

			int nextLineNum = _grid.Rows.Count + 1;
			_grid.Rows.Add(Guid.NewGuid().ToString(), nextLineNum, "", 0, "", 0, 0, "0.00");

			int newIdx = _grid.Rows.Count - 1;
			_grid.CurrentCell = _grid.Rows[newIdx].Cells["Description"];
			_grid.BeginEdit(true);
		}

		private void OnSaveAllClicked(object? sender, EventArgs e)
		{
			if (_instanceId is not null)
			{
				SaveLineItemsForInstance(_instanceId);
				return;
			}

			// Doc-type mode
			var dt = _cboInstance?.SelectedItem as DocTypeItem;
			if (dt?.DocTypeID is null)
			{
				SetStatus("Select a document type before saving.", success: false);
				return;
			}

			ComputeTotals();

			var existing    = _db.GetLineItemsForDocType(dt.DocTypeID);
			var existingIds = new HashSet<string>(existing.Select(x => x.LineItemID));

			int saved = 0, errors = 0;

			foreach (DataGridViewRow row in _grid.Rows)
			{
				if (row.IsNewRow) continue;

				int.TryParse(   Convert.ToString(row.Cells["LineNum"].Value),    out int    lineNum);
				int.TryParse(   Convert.ToString(row.Cells["NAICS"].Value),      out int    naics);
				double.TryParse(Convert.ToString(row.Cells["Quantity"].Value),   out double qty);
				double.TryParse(Convert.ToString(row.Cells["UnitPrice"].Value),  out double price);

				string id = Convert.ToString(row.Cells["LineItemID"].Value) ?? Guid.NewGuid().ToString();

				var li = new LineItem
				{
					LineItemID  = id,
					DocTypeID   = dt.DocTypeID,
					InstanceID  = null,
					LineNum     = lineNum,
					Description = Convert.ToString(row.Cells["Description"].Value) ?? "",
					NAICS       = naics,
					Unit        = Convert.ToString(row.Cells["Unit"].Value) ?? "",
					Quantity    = qty,
					UnitPrice   = price
				};

				try
				{
					if (existingIds.Contains(id))
						_db.UpdateLineItem(li);
					else
						_db.InsertLineItem(li);
					saved++;
				}
				catch (Exception ex)
				{
					errors++;
					SetStatus($"Error saving row {lineNum}: {ex.Message}", success: false);
				}
			}

			if (errors == 0)
				SetStatus($"Saved {saved} line item(s).", success: true);
		}

		private void SaveLineItemsForInstance(string instanceId)
		{
			ComputeTotals();

			var existing    = _db.GetLineItemsForInstance(instanceId);
			var existingIds = new HashSet<string>(existing.Select(x => x.LineItemID));

			int saved = 0, errors = 0;

			foreach (DataGridViewRow row in _grid.Rows)
			{
				if (row.IsNewRow) continue;

				int.TryParse(   Convert.ToString(row.Cells["LineNum"].Value),    out int    lineNum);
				int.TryParse(   Convert.ToString(row.Cells["NAICS"].Value),      out int    naics);
				double.TryParse(Convert.ToString(row.Cells["Quantity"].Value),   out double qty);
				double.TryParse(Convert.ToString(row.Cells["UnitPrice"].Value),  out double price);

				string id = Convert.ToString(row.Cells["LineItemID"].Value) ?? Guid.NewGuid().ToString();

				var li = new LineItem
				{
					LineItemID  = id,
					DocTypeID   = null,
					InstanceID  = instanceId,
					LineNum     = lineNum,
					Description = Convert.ToString(row.Cells["Description"].Value) ?? "",
					NAICS       = naics,
					Unit        = Convert.ToString(row.Cells["Unit"].Value) ?? "",
					Quantity    = qty,
					UnitPrice   = price
				};

				try
				{
					if (existingIds.Contains(id))
						_db.UpdateLineItem(li);
					else
						_db.InsertLineItem(li);
					saved++;
				}
				catch (Exception ex)
				{
					errors++;
					SetStatus($"Error saving row {lineNum}: {ex.Message}", success: false);
				}
			}

			if (errors == 0)
				SetStatus($"Saved {saved} line item(s).", success: true);
		}

		private void OnDeleteRowClicked(object? sender, EventArgs e)
		{
			if (_grid.SelectedRows.Count == 0)
			{
				SetStatus("Select a row to delete.", success: false);
				return;
			}

			var    row  = _grid.SelectedRows[0];
			string desc = Convert.ToString(row.Cells["Description"].Value) ?? "(empty)";
			string id   = Convert.ToString(row.Cells["LineItemID"].Value)  ?? "";

			var confirm = MessageBox.Show(
				$"Delete \"{desc}\"?\n\nThis cannot be undone.",
				"Confirm Delete",
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Warning);

			if (confirm != DialogResult.Yes) return;

			if (!string.IsNullOrEmpty(id))
			{
				try   { _db.DeleteLineItem(id); }
				catch (Exception ex)
				{
					SetStatus($"Delete failed: {ex.Message}", success: false);
					return;
				}
			}

			_grid.Rows.Remove(row);
			ComputeTotals();
			SetStatus($"Deleted \"{desc}\".", success: true);
		}

		// ─────────────────────────────────────────────────────────────
		// Helpers
		// ─────────────────────────────────────────────────────────────
		private void SetStatus(string message, bool success)
		{
			_lblStatus.Text      = $"●  {message}";
			_lblStatus.ForeColor = success
				? Color.FromArgb(15, 110, 86)
				: Color.FromArgb(180, 40, 40);
		}

		private sealed class DocTypeItem
		{
			public string? DocTypeID { get; }
			public string  Display   { get; }

			public DocTypeItem(string? docTypeId, string display)
			{
				DocTypeID = docTypeId;
				Display   = display;
			}

			public override string ToString() => Display;
		}
	}
}
