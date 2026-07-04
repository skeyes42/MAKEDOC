using MakeDoc.Core.Data;
using MakeDoc.Core.Models;

namespace MakeDoc.App.Forms.Admin
{
	public partial class AdminForm : Form
	{
		// ── Services ──────────────────────────────────────────────────
		private readonly MakDocDb _db;

		// ── State ─────────────────────────────────────────────────────
		private List<DocType> _docTypes    = new();
		private List<Node>    _allNodes    = new();
		private bool          _isNewDocType = false;

		// ── Controls — DocTypes tab ───────────────────────────────────
		private ListBox  _lstDocTypes      = null!;
		private TextBox  _txtDocTypeID     = null!;
		private TextBox  _txtName          = null!;
		private ComboBox _cboHeaderNode    = null!;
		private ComboBox _cboTemplateBlob  = null!;
		private TextBox  _txtInclusionTags = null!;
		private Button   _btnNew           = null!;
		private Button   _btnSave          = null!;
		private Button   _btnDelete        = null!;

		// ── Controls — Nodes tab ──────────────────────────────────────
		private ComboBox _cboNodeTypeFilter = null!;
		private TextBox  _txtNodeSearch     = null!;
		private ListView _lvNodes           = null!;

		// ── Shared ────────────────────────────────────────────────────
		private Label _lblStatus = null!;

		// ─────────────────────────────────────────────────────────────
		// Constructor
		// ─────────────────────────────────────────────────────────────
		public AdminForm()
		{
			InitializeComponent();
			BuildUI();

			try
			{
				_db = new MakDocDb();
				RefreshDocTypes();
				RefreshNodes();
				SetStatus("Ready.", success: true);
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
			Text          = "MAKEDOC — Administration";
			Size          = new Size(960, 660);
			MinimumSize   = new Size(800, 540);
			StartPosition = FormStartPosition.CenterScreen;
			BackColor     = Color.White;
			Font          = new Font("Segoe UI", 9.5f);

			var outer = new TableLayoutPanel
			{
				Dock        = DockStyle.Fill,
				ColumnCount = 1,
				RowCount    = 3,
				Padding     = new Padding(24)
			};
			outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // header
			outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // tabs
			outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));   // status bar
			Controls.Add(outer);

			BuildHeader(outer);
			BuildTabs(outer);
			BuildStatusBar(outer);
		}

		private void BuildHeader(TableLayoutPanel parent)
		{
			var panel = new Panel { Dock = DockStyle.Fill };

			panel.Controls.Add(new Label
			{
				Text      = "Administration",
				Font      = new Font("Segoe UI", 14f, FontStyle.Regular),
				ForeColor = Color.FromArgb(30, 30, 30),
				AutoSize  = true,
				Location  = new Point(0, 2)
			});
			panel.Controls.Add(new Label
			{
				Text      = "Manage document types and browse nodes.",
				Font      = new Font("Segoe UI", 9f),
				ForeColor = Color.FromArgb(110, 110, 110),
				AutoSize  = true,
				Location  = new Point(0, 32)
			});

			parent.Controls.Add(panel, 0, 0);
		}

		private void BuildTabs(TableLayoutPanel parent)
		{
			var tabs = new TabControl
			{
				Dock = DockStyle.Fill,
				Font = new Font("Segoe UI", 9f)
			};

			tabs.TabPages.Add(BuildDocTypesTab());
			tabs.TabPages.Add(BuildNodesTab());

			parent.Controls.Add(tabs, 0, 1);
		}

		// ── Document Types Tab ────────────────────────────────────────
		private TabPage BuildDocTypesTab()
		{
			var page = new TabPage("Document Types") { BackColor = Color.White };
			page.Padding = new Padding(8);

			var layout = new TableLayoutPanel
			{
				Dock        = DockStyle.Fill,
				ColumnCount = 2,
				RowCount    = 1,
				Padding     = new Padding(0, 4, 0, 0)
			};
			layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
			layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			page.Controls.Add(layout);

			layout.Controls.Add(BuildDocTypeList(), 0, 0);
			layout.Controls.Add(BuildDocTypeForm(), 1, 0);

			return page;
		}

		private Panel BuildDocTypeList()
		{
			var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0) };

			panel.Controls.Add(new Label
			{
				Text      = "Document Types",
				Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
				ForeColor = Color.FromArgb(80, 80, 80),
				Dock      = DockStyle.Top,
				Height    = 22
			});

			_lstDocTypes = new ListBox
			{
				Dock           = DockStyle.Fill,
				BorderStyle    = BorderStyle.FixedSingle,
				Font           = new Font("Segoe UI", 9.5f),
				IntegralHeight = false,
				ItemHeight     = 22
			};
			_lstDocTypes.SelectedIndexChanged += OnDocTypeSelected;
			panel.Controls.Add(_lstDocTypes);

			return panel;
		}

		private Panel BuildDocTypeForm()
		{
			var panel = new Panel { Dock = DockStyle.Fill };

			int y          = 4;
			const int LH   = 18;   // label height
			const int FH   = 26;   // field height
			const int Gap  = 10;
			const int FW   = 420;  // field width

			// ── Local helper ─────────────────────────────────
			Label Lbl(string text)
			{
				var l = new Label
				{
					Text      = text,
					Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
					ForeColor = Color.FromArgb(80, 80, 80),
					Location  = new Point(0, y),
					AutoSize  = false,
					Width     = FW,
					Height    = LH
				};
				panel.Controls.Add(l);
				y += LH;
				return l;
			}

			TextBox Txt(bool enabled = true)
			{
				var t = new TextBox
				{
					Location = new Point(0, y),
					Width    = FW,
					Height   = FH,
					Font     = new Font("Segoe UI", 9.5f),
					Enabled  = enabled
				};
				panel.Controls.Add(t);
				y += FH + Gap;
				return t;
			}

			ComboBox Cbo()
			{
				var c = new ComboBox
				{
					Location      = new Point(0, y),
					Width         = FW,
					Height        = FH,
					Font          = new Font("Segoe UI", 9.5f),
					DropDownStyle = ComboBoxStyle.DropDownList
				};
				panel.Controls.Add(c);
				y += FH + Gap;
				return c;
			}

			// ── Fields ───────────────────────────────────────
			Lbl("Document Type ID");
			_txtDocTypeID = Txt(enabled: false);

			Lbl("Name");
			_txtName = Txt();

			Lbl("Header Node");
			_cboHeaderNode = Cbo();

			Lbl("Template Blob");
			_cboTemplateBlob = Cbo();

			Lbl("Inclusion Tags  (JSON array, e.g. [\"DEI\",\"HAZMAT\"])");
			_txtInclusionTags = Txt();

			y += 4;

			// ── Buttons ──────────────────────────────────────
			_btnNew = MakeButton("New", Color.FromArgb(60, 60, 60));
			_btnNew.Location = new Point(0, y);
			_btnNew.Click   += OnNewClicked;
			panel.Controls.Add(_btnNew);

			_btnSave = MakeButton("Save", Color.FromArgb(30, 30, 30));
			_btnSave.Location = new Point(100, y);
			_btnSave.Click   += OnSaveClicked;
			panel.Controls.Add(_btnSave);

			_btnDelete = MakeButton("Delete", Color.FromArgb(180, 40, 40));
			_btnDelete.Location = new Point(200, y);
			_btnDelete.Enabled  = false;
			_btnDelete.Click   += OnDeleteClicked;
			panel.Controls.Add(_btnDelete);

			return panel;
		}

		// ── Nodes Tab ─────────────────────────────────────────────────
		private TabPage BuildNodesTab()
		{
			var page = new TabPage("Nodes") { BackColor = Color.White };
			page.Padding = new Padding(8);

			var layout = new TableLayoutPanel
			{
				Dock        = DockStyle.Fill,
				ColumnCount = 1,
				RowCount    = 2,
				Padding     = new Padding(0, 4, 0, 0)
			};
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
			page.Controls.Add(layout);

			// ── Filter bar ───────────────────────────────────
			var bar = new Panel { Dock = DockStyle.Fill };

			bar.Controls.Add(new Label
			{
				Text      = "Type:",
				Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
				ForeColor = Color.FromArgb(80, 80, 80),
				AutoSize  = true,
				Location  = new Point(0, 9)
			});

			_cboNodeTypeFilter = new ComboBox
			{
				Width         = 150,
				Location      = new Point(42, 5),
				Font          = new Font("Segoe UI", 9.5f),
				DropDownStyle = ComboBoxStyle.DropDownList
			};
			_cboNodeTypeFilter.Items.Add("All");
			foreach (var t in new[] {
				NodeTypes.HeaderNode, NodeTypes.Section,   NodeTypes.Subsection })
				//NodeTypes.Clause,     NodeTypes.Template,  NodeTypes.Document })
				_cboNodeTypeFilter.Items.Add(t);
			_cboNodeTypeFilter.SelectedIndex = 0;
			_cboNodeTypeFilter.SelectedIndexChanged += (_, _) => ApplyNodeFilter();
			bar.Controls.Add(_cboNodeTypeFilter);

			bar.Controls.Add(new Label
			{
				Text      = "Search:",
				Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
				ForeColor = Color.FromArgb(80, 80, 80),
				AutoSize  = true,
				Location  = new Point(204, 9)
			});

			_txtNodeSearch = new TextBox
			{
				Width    = 260,
				Location = new Point(262, 5),
				Font     = new Font("Segoe UI", 9.5f)
			};
			_txtNodeSearch.TextChanged += (_, _) => ApplyNodeFilter();
			bar.Controls.Add(_txtNodeSearch);

			layout.Controls.Add(bar, 0, 0);

			// ── Node list ─────────────────────────────────────
			_lvNodes = new ListView
			{
				Dock          = DockStyle.Fill,
				View          = View.Details,
				FullRowSelect = true,
				GridLines     = true,
				BorderStyle   = BorderStyle.FixedSingle,
				Font          = new Font("Segoe UI", 9f)
			};
			_lvNodes.Columns.Add("Node ID",  120);
			_lvNodes.Columns.Add("Type",      100);
			_lvNodes.Columns.Add("Title",     420);
			_lvNodes.Columns.Add("Content",    80);

			layout.Controls.Add(_lvNodes, 0, 1);

			return page;
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
			parent.Controls.Add(_lblStatus, 0, 2);
		}

		// ── Shared button factory ─────────────────────────────────────
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

			_lstDocTypes.BeginUpdate();
			_lstDocTypes.Items.Clear();
			foreach (var dt in _docTypes)
				_lstDocTypes.Items.Add(dt.Name);
			_lstDocTypes.EndUpdate();

			PopulateNodeCombos();
		}

		private void PopulateNodeCombos()
		{
			var headerNodes   = _db.GetNodesByType(NodeTypes.HeaderNode);
			//var templateNodes = _db.GetNodesByType(NodeTypes.Template);

			FillNodeCombo(_cboHeaderNode,   headerNodes);
			//FillNodeCombo(_cboTemplateBlob, templateNodes);
		}

		private static void FillNodeCombo(ComboBox cbo, List<Node> nodes)
		{
			cbo.Items.Clear();
			cbo.Items.Add(new NodeItem("", "(none)"));
			foreach (var n in nodes)
				cbo.Items.Add(new NodeItem(
					n.NodeID,
					string.IsNullOrWhiteSpace(n.Title)
						? n.NodeID
						: $"{n.NodeID} — {n.Title}"));
			if (cbo.Items.Count > 0)
				cbo.SelectedIndex = 0;
		}

		private void RefreshNodes()
		{
			_allNodes = _db.GetAllNodes();
			ApplyNodeFilter();
		}

		private void ApplyNodeFilter()
		{
			var typeFilter = _cboNodeTypeFilter.SelectedItem?.ToString() ?? "All";
			var term       = _txtNodeSearch.Text.Trim().ToLowerInvariant();

			var filtered = _allNodes
				.Where(n => typeFilter == "All" || n.NodeType == typeFilter)
				.Where(n => string.IsNullOrEmpty(term)
				            || n.NodeID.ToLowerInvariant().Contains(term)
				            || (n.Title?.ToLowerInvariant().Contains(term) ?? false))
				.ToList();

			_lvNodes.BeginUpdate();
			_lvNodes.Items.Clear();
			foreach (var n in filtered)
			{
				var item = new ListViewItem(n.NodeID);
				item.SubItems.Add(n.NodeType);
				item.SubItems.Add(n.Title ?? "");
				item.SubItems.Add(n.Content is { Length: > 0 }
					? $"{n.Content.Length / 1024} KB"
					: "—");
				_lvNodes.Items.Add(item);
			}
			_lvNodes.EndUpdate();

			SetStatus($"Nodes: {filtered.Count} shown of {_allNodes.Count} total.", success: true);
		}

		// ─────────────────────────────────────────────────────────────
		// Event Handlers
		// ─────────────────────────────────────────────────────────────
		private void OnDocTypeSelected(object? sender, EventArgs e)
		{
			int idx = _lstDocTypes.SelectedIndex;
			if (idx < 0 || idx >= _docTypes.Count) return;

			_isNewDocType = false;
			var dt = _docTypes[idx];

			_txtDocTypeID.Text     = dt.DocTypeID;
			_txtDocTypeID.Enabled  = false;
			_txtName.Text          = dt.Name;
			_txtInclusionTags.Text = dt.InclusionTags ?? "";

			SelectNodeCombo(_cboHeaderNode,   dt.HeaderNodeID);
			//SelectNodeCombo(_cboTemplateBlob, dt.TemplateBlobID);

			_btnDelete.Enabled = true;
			SetStatus($"Editing: {dt.Name}", success: true);
		}

		private static void SelectNodeCombo(ComboBox cbo, string? nodeId)
		{
			for (int i = 0; i < cbo.Items.Count; i++)
			{
				if (cbo.Items[i] is NodeItem ni &&
				    (ni.NodeID == (nodeId ?? "")))
				{
					cbo.SelectedIndex = i;
					return;
				}
			}
			if (cbo.Items.Count > 0) cbo.SelectedIndex = 0;
		}

		private void OnNewClicked(object? sender, EventArgs e)
		{
			_isNewDocType = true;
			_lstDocTypes.ClearSelected();

			_txtDocTypeID.Text     = "";
			_txtDocTypeID.Enabled  = true;
			_txtName.Text          = "";
			_txtInclusionTags.Text = "";
			if (_cboHeaderNode.Items.Count   > 0) _cboHeaderNode.SelectedIndex   = 0;
			if (_cboTemplateBlob.Items.Count > 0) _cboTemplateBlob.SelectedIndex = 0;
			_btnDelete.Enabled = false;

			_txtDocTypeID.Focus();
			SetStatus("Enter details for the new document type, then click Save.", success: true);
		}

		private void OnSaveClicked(object? sender, EventArgs e)
		{
			var id   = _txtDocTypeID.Text.Trim();
			var name = _txtName.Text.Trim();

			if (string.IsNullOrWhiteSpace(id))
			{
				SetStatus("Document Type ID is required.", success: false);
				return;
			}
			if (string.IsNullOrWhiteSpace(name))
			{
				SetStatus("Name is required.", success: false);
				return;
			}

			var hdr  = (_cboHeaderNode.SelectedItem   as NodeItem)?.NodeID;
			var tmpl = (_cboTemplateBlob.SelectedItem as NodeItem)?.NodeID;

			var dt = new DocType
			{
				DocTypeID      = id,
				Name           = name,
				HeaderNodeID   = string.IsNullOrWhiteSpace(hdr)  ? null : hdr,
				//TemplateBlobID = string.IsNullOrWhiteSpace(tmpl) ? null : tmpl,
				InclusionTags  = string.IsNullOrWhiteSpace(_txtInclusionTags.Text)
				                     ? null
				                     : _txtInclusionTags.Text.Trim()
			};

			try
			{
				if (_isNewDocType)
					_db.InsertDocType(dt);
				else
					_db.UpdateDocType(dt);

				RefreshDocTypes();

				int newIdx = _docTypes.FindIndex(d => d.DocTypeID == id);
				if (newIdx >= 0) _lstDocTypes.SelectedIndex = newIdx;

				SetStatus($"Saved: {dt.Name}", success: true);
			}
			catch (Exception ex)
			{
				SetStatus($"Save failed: {ex.Message}", success: false);
			}
		}

		private void OnDeleteClicked(object? sender, EventArgs e)
		{
			int idx = _lstDocTypes.SelectedIndex;
			if (idx < 0 || idx >= _docTypes.Count) return;

			var dt = _docTypes[idx];
			var confirm = MessageBox.Show(
				$"Delete document type \"{dt.Name}\"?\n\nThis cannot be undone.",
				"Confirm Delete",
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Warning);

			if (confirm != DialogResult.Yes) return;

			try
			{
				_db.DeleteDocType(dt.DocTypeID);
				RefreshDocTypes();
				ClearDocTypeForm();
				SetStatus($"Deleted: {dt.Name}", success: true);
			}
			catch (Exception ex)
			{
				SetStatus($"Delete failed: {ex.Message}", success: false);
			}
		}

		// ─────────────────────────────────────────────────────────────
		// Helpers
		// ─────────────────────────────────────────────────────────────
		private void ClearDocTypeForm()
		{
			_isNewDocType          = false;
			_txtDocTypeID.Text     = "";
			_txtDocTypeID.Enabled  = false;
			_txtName.Text          = "";
			_txtInclusionTags.Text = "";
			if (_cboHeaderNode.Items.Count   > 0) _cboHeaderNode.SelectedIndex   = 0;
			if (_cboTemplateBlob.Items.Count > 0) _cboTemplateBlob.SelectedIndex = 0;
			_btnDelete.Enabled = false;
		}

		private void SetStatus(string message, bool success)
		{
			_lblStatus.Text      = $"●  {message}";
			_lblStatus.ForeColor = success
				? Color.FromArgb(15, 110, 86)
				: Color.FromArgb(180, 40, 40);
		}

		// ── NodeItem wrapper for ComboBox ─────────────────────────────
		private sealed class NodeItem
		{
			public string NodeID  { get; }
			public string Display { get; }

			public NodeItem(string nodeId, string display)
			{
				NodeID  = nodeId;
				Display = display;
			}

			public override string ToString() => Display;
		}
	}
}
