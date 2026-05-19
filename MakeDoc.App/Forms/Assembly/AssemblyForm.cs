using MakeDoc.Core.Data;
using MakeDoc.Core.Models;
using MakeDoc.Core.Services;

namespace MakeDoc.App.Forms.Assembly
{
	public partial class AssemblyForm : Form
	{
		// ── Services ──────────────────────────────────────────────────
		private readonly MakDocDb _db;
		private readonly NodeService _nodeService;
		private readonly NodeHierarchyService _hierarchyService;
		private readonly TemplateService _templateService;
		private readonly DocumentAssemblyService _assemblyService;

		// ── State ─────────────────────────────────────────────────────
		private List<DocType> _docTypes = new();
		private DocType? _selectedDocType;

		// ── Controls ──────────────────────────────────────────────────
		private TableLayoutPanel _mainLayout = null!;
		private ListBox _lstDocTypes = null!;
		private ListView _lstNodes = null!;
		private Label _lblNodeCount = null!;
		private Button _btnGenerate = null!;
		private Label _lblStatus = null!;

		// ─────────────────────────────────────────────────────────────
		// Constructor
		// ─────────────────────────────────────────────────────────────
		public AssemblyForm()
		{
			InitializeComponent();
			BuildUI();

			try
			{
				_db               = new MakDocDb();
				_nodeService      = new NodeService(_db);
				_hierarchyService = new NodeHierarchyService(_db);
				_templateService  = new TemplateService(_db, _hierarchyService);
				_assemblyService  = new DocumentAssemblyService();

				LoadDocTypes();
				SetStatus("Select a document type to begin.", success: true);
			}
			catch (Exception ex)
			{
				_db            = null!;
				_nodeService   = null!;
				_hierarchyService = null!;
				_templateService  = null!;
				_assemblyService  = null!;
				SetStatus($"Database error: {ex.Message}", success: false);
			}
		}

		// ─────────────────────────────────────────────────────────────
		// UI Construction
		// ─────────────────────────────────────────────────────────────
		private void BuildUI()
		{
			this.Text            = "MAKEDOC — Document Assembly";
			this.Size            = new Size(900, 600);
			this.MinimumSize     = new Size(780, 500);
			this.StartPosition   = FormStartPosition.CenterScreen;
			this.BackColor       = Color.White;
			this.Font            = new Font("Segoe UI", 9.5f);

			// Outer layout: header | content | status
			_mainLayout = new TableLayoutPanel
			{
				Dock        = DockStyle.Fill,
				ColumnCount = 1,
				RowCount    = 3,
				Padding     = new Padding(24)
			};
			_mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // header
			_mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // content
			_mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));   // action bar
			this.Controls.Add(_mainLayout);

			BuildHeader();
			BuildContent();
			BuildActionBar();
		}

		private void BuildHeader()
		{
			var panel = new Panel { Dock = DockStyle.Fill };

			var lblTitle = new Label
			{
				Text      = "Document Assembly",
				Font      = new Font("Segoe UI", 14f, FontStyle.Regular),
				ForeColor = Color.FromArgb(30, 30, 30),
				AutoSize  = true,
				Location  = new Point(0, 2)
			};

			var lblSub = new Label
			{
				Text      = "Select a document type, review the clause list, then generate.",
				Font      = new Font("Segoe UI", 9f),
				ForeColor = Color.FromArgb(110, 110, 110),
				AutoSize  = true,
				Location  = new Point(0, 32)
			};

			panel.Controls.Add(lblTitle);
			panel.Controls.Add(lblSub);
			_mainLayout.Controls.Add(panel, 0, 0);
		}

		private void BuildContent()
		{
			// Two-column split: DocType list | Node list
			var content = new TableLayoutPanel
			{
				Dock        = DockStyle.Fill,
				ColumnCount = 2,
				RowCount    = 1,
				Padding     = new Padding(0, 8, 0, 8)
			};
			content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
			content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

			// ── Left panel — DocType list ──────────────────────────
			var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0) };

			var lblDocTypes = new Label
			{
				Text      = "Document Type",
				Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
				ForeColor = Color.FromArgb(80, 80, 80),
				Dock      = DockStyle.Top,
				Height    = 22
			};

			_lstDocTypes = new ListBox
			{
				Dock              = DockStyle.Fill,
				BorderStyle       = BorderStyle.FixedSingle,
				Font              = new Font("Segoe UI", 9.5f),
				IntegralHeight    = false,
				ItemHeight        = 22
			};
			_lstDocTypes.SelectedIndexChanged += OnDocTypeSelected;

			leftPanel.Controls.Add(_lstDocTypes);
			leftPanel.Controls.Add(lblDocTypes);

			// ── Right panel — Node list ────────────────────────────
			var rightPanel = new Panel { Dock = DockStyle.Fill };

			_lblNodeCount = new Label
			{
				Text      = "Clause list",
				Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
				ForeColor = Color.FromArgb(80, 80, 80),
				Dock      = DockStyle.Top,
				Height    = 22
			};

			_lstNodes = new ListView
			{
				Dock          = DockStyle.Fill,
				View          = View.Details,
				FullRowSelect = true,
				GridLines     = true,
				BorderStyle   = BorderStyle.FixedSingle,
				Font          = new Font("Segoe UI", 9f)
			};
			_lstNodes.Columns.Add("Node ID",  90);
			_lstNodes.Columns.Add("Type",     90);
			_lstNodes.Columns.Add("Title",    400);

			rightPanel.Controls.Add(_lstNodes);
			rightPanel.Controls.Add(_lblNodeCount);

			content.Controls.Add(leftPanel,  0, 0);
			content.Controls.Add(rightPanel, 1, 0);
			_mainLayout.Controls.Add(content, 0, 1);
		}

		private void BuildActionBar()
		{
			var bar = new TableLayoutPanel
			{
				Dock        = DockStyle.Fill,
				ColumnCount = 2,
				RowCount    = 1
			};
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));

			_lblStatus = new Label
			{
				Text      = "Initializing...",
				Dock      = DockStyle.Fill,
				Font      = new Font("Segoe UI", 8.5f),
				ForeColor = Color.FromArgb(130, 130, 130),
				TextAlign = ContentAlignment.MiddleLeft
			};

			_btnGenerate = new Button
			{
				Text      = "Generate Document",
				Dock      = DockStyle.Fill,
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(30, 30, 30),
				ForeColor = Color.White,
				Font      = new Font("Segoe UI", 9.5f),
				Cursor    = Cursors.Hand,
				Enabled   = false
			};
			_btnGenerate.FlatAppearance.BorderSize  = 0;
			_btnGenerate.Click += OnGenerateClicked;

			bar.Controls.Add(_lblStatus,   0, 0);
			bar.Controls.Add(_btnGenerate, 1, 0);
			_mainLayout.Controls.Add(bar, 0, 2);
		}

		// ─────────────────────────────────────────────────────────────
		// Data Loading
		// ─────────────────────────────────────────────────────────────
		private void LoadDocTypes()
		{
			_docTypes = _db.GetAllDocTypes();

			_lstDocTypes.BeginUpdate();
			_lstDocTypes.Items.Clear();

			foreach (var dt in _docTypes)
				_lstDocTypes.Items.Add(dt.Name);

			_lstDocTypes.EndUpdate();
		}

		private void LoadNodeList(DocType docType)
		{
			_lstNodes.Items.Clear();
			_lblNodeCount.Text = "Clause list — loading...";

			if (string.IsNullOrWhiteSpace(docType.HeaderNodeID))
			{
				_lblNodeCount.Text = "Clause list — no header node configured";
				return;
			}

			var orderedIds = _hierarchyService.GetOrderedNodeIds(
				docType.DocTypeID,
				docType.HeaderNodeID);

			_lstNodes.BeginUpdate();

			foreach (var nodeId in orderedIds)
			{
				var node = _nodeService.GetById(nodeId);

				var item = new ListViewItem(nodeId);
				item.SubItems.Add(node?.NodeType ?? "—");
				item.SubItems.Add(node?.Title    ?? "");
				_lstNodes.Items.Add(item);
			}

			_lstNodes.EndUpdate();

			_lblNodeCount.Text = $"Clause list — {orderedIds.Count} node(s)";
		}

		// ─────────────────────────────────────────────────────────────
		// Event Handlers
		// ─────────────────────────────────────────────────────────────
		private void OnDocTypeSelected(object? sender, EventArgs e)
		{
			int idx = _lstDocTypes.SelectedIndex;
			if (idx < 0 || idx >= _docTypes.Count) return;

			_selectedDocType    = _docTypes[idx];
			_btnGenerate.Enabled = true;

			try
			{
				LoadNodeList(_selectedDocType);
				SetStatus($"Selected: {_selectedDocType.Name}", success: true);
			}
			catch (Exception ex)
			{
				SetStatus($"Error loading nodes: {ex.Message}", success: false);
			}
		}

		private void OnGenerateClicked(object? sender, EventArgs e)
		{
			if (_selectedDocType == null) return;

			if (string.IsNullOrWhiteSpace(_selectedDocType.HeaderNodeID))
			{
				SetStatus("Cannot generate — no header node configured for this document type.", success: false);
				return;
			}

			try
			{
				_btnGenerate.Enabled = false;
				SetStatus("Creating instance...", success: true);

				// 1. Persist a fresh Instance row with the default NodeList
				//    derived from the DocType's hierarchy.
				var instance = _templateService.CreateFromDocType(
					_selectedDocType.DocTypeID,
					_selectedDocType.HeaderNodeID);

				// 2. Pull the ordered NodeIDs and fetch each clause's blob.
				var nodeIds = _templateService.GetNodeList(instance.InstanceID);
				if (nodeIds.Count == 0)
				{
					SetStatus("Cannot generate — instance has no nodes.", success: false);
					return;
				}

				SetStatus($"Loading {nodeIds.Count} clause blob(s)...", success: true);

				var blobs   = new List<byte[]>(nodeIds.Count);
				var missing = new List<string>();
				foreach (var nodeId in nodeIds)
				{
					var node = _nodeService.GetById(nodeId);
					if (node?.Content == null || node.Content.Length == 0)
						missing.Add(nodeId);
					else
						blobs.Add(node.Content);
				}

				if (missing.Count > 0)
				{
					string preview = string.Join(", ", missing.Take(5));
					if (missing.Count > 5) preview += "...";
					SetStatus(
						$"Cannot generate — {missing.Count} node(s) have no content: {preview}",
						success: false);
					return;
				}

				// 3. Assemble in-memory.
				SetStatus("Assembling document...", success: true);
				byte[] assembledBytes = _assemblyService.Assemble(blobs);

				// 4. Let the user choose where to save.
				string defaultName = SanitizeFilename(
					$"{_selectedDocType.Name}_{instance.InstanceID[..8]}_{DateTime.Now:yyyyMMdd-HHmm}.docx");

				using var dlg = new SaveFileDialog
				{
					Title        = "Save assembled document",
					Filter       = "Word document (*.docx)|*.docx",
					FileName     = defaultName,
					DefaultExt   = "docx",
					AddExtension = true
				};

				if (dlg.ShowDialog(this) != DialogResult.OK)
				{
					SetStatus("Generation cancelled — file not saved.", success: true);
					return;
				}

				File.WriteAllBytes(dlg.FileName, assembledBytes);

				SetStatus(
					$"Saved: {Path.GetFileName(dlg.FileName)}  " +
					$"(instance {instance.InstanceID[..8]}…, {nodeIds.Count} nodes).",
					success: true);
			}
			catch (Exception ex)
			{
				SetStatus($"Error: {ex.Message}", success: false);
			}
			finally
			{
				_btnGenerate.Enabled = true;
			}
		}

		// Strips characters Windows won't accept in a filename.
		private static string SanitizeFilename(string name)
		{
			foreach (var c in Path.GetInvalidFileNameChars())
				name = name.Replace(c, '_');
			return name;
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
	}
}
