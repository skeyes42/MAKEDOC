using System.Diagnostics;
using System.Text.Json;
using MakeDoc.App.Forms.Lineitem;
using MakeDoc.Core.Data;
using MakeDoc.Core.Models;
using MakeDoc.Core.Services;

namespace MakeDoc.App.Forms.Assembly
{
	public partial class AssemblyForm : Form
	{
		// ── Services ──────────────────────────────────────────────────
		private readonly MakDocDb                _db;
		private readonly NodeService             _nodeService;
		private readonly NodeHierarchyService    _hierarchyService;
		private readonly DocumentAssemblyService _assemblyService;
		private readonly FillinService           _fillinService;

		// ── State ─────────────────────────────────────────────────────
		private List<DocType>            _docTypes          = new();
		private DocType?                 _selectedDocType;
		private List<TransientNodeEntry> _transientNodeList = new();
		private List<string>             _canonicalNodeIds  = new();

		// ── Build-from state (null when in canonical mode) ─────────────
		private Instance?    _buildFromInstance    = null;

		// ── Build-to state (null unless this is a build-to session) ────
		// When set, the transient list is seeded from the target DocType's
		// canonical nodes (not the source's), and Reset restores that list.
		private DocType?     _buildToTargetDocType = null;

		// ── Dirty flag — true when the transient list has been modified
		//    since the last successful Generate (or since the form opened).
		private bool         _isDirty           = false;

		// ── Controls ──────────────────────────────────────────────────
		private TableLayoutPanel  _mainLayout   = null!;
		private ListBox           _lstDocTypes  = null!;
		private ListView          _lstNodes     = null!;
		private Label             _lblNodeCount = null!;
		private Button            _btnGenerate  = null!;
		private Label             _lblStatus    = null!;
		private ContextMenuStrip  _ctxMenu      = null!;

		private ToolStripMenuItem _miInsertUser    = null!;
		private ToolStripMenuItem _miDelete        = null!;
		private ToolStripMenuItem _miEdit          = null!;
		private ToolStripMenuItem _miMoveUp        = null!;
		private ToolStripMenuItem _miMoveDown      = null!;
		private ToolStripMenuItem _miBrowse        = null!;
		private ToolStripMenuItem _miReset         = null!;
		private ToolStripMenuItem _miInsertSpecial = null!;

		// ── ListView row colors ────────────────────────────────────────
		private static readonly Color ClrUserInsertion = Color.FromArgb(225, 240, 255);
		private static readonly Color ClrEdited        = Color.FromArgb(255, 251, 215);
		private static readonly Color ClrSpecial       = Color.FromArgb(242, 242, 242);

		// ─────────────────────────────────────────────────────────────
		// Constructor
		// ─────────────────────────────────────────────────────────────
		public AssemblyForm()
		{
			InitializeComponent();
			BuildUI();

			_fillinService = new FillinService();

			try
			{
				_db               = new MakDocDb();
				_nodeService      = new NodeService(_db);
				_hierarchyService = new NodeHierarchyService(_db);
				_assemblyService  = new DocumentAssemblyService();

				LoadDocTypes();
				SetStatus("Select a document type to begin.", success: true);
			}
			catch (Exception ex)
			{
				_db               = null!;
				_nodeService      = null!;
				_hierarchyService = null!;
				_assemblyService  = null!;
				SetStatus($"Database error: {ex.Message}", success: false);
			}
		}

		// ─────────────────────────────────────────────────────────────
		// Build-from constructor
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// Opens the Assembly form pre-loaded from a prior Instance.
		/// The doc type picker is locked; the transient list is seeded
		/// from the source instance's NodeList instead of the NodeHierarchy.
		/// </summary>
		public AssemblyForm(Instance sourceInstance) : this()
		{
			_buildFromInstance = sourceInstance;
			InitBuildFromMode(sourceInstance);
		}

		// ─────────────────────────────────────────────────────────────
		// Build-to constructor
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// Opens the Assembly form for a build-to operation.
		/// The transient list is seeded from the TARGET DocType's canonical
		/// nodes (not the source's). Fill-in values from the source instance
		/// are pre-populated in the Fill-in form via _buildFromInstance.
		/// </summary>
		public AssemblyForm(Instance sourceInstance, DocType targetDocType) : this()
		{
			_buildFromInstance    = sourceInstance;
			_buildToTargetDocType = targetDocType;
			InitBuildToMode(sourceInstance, targetDocType);
		}

		private void InitBuildToMode(Instance sourceInstance, DocType targetDocType)
		{
			this.Text = $"MAKEDOC — Build To: {targetDocType.Name}";

			// Lock the doc type picker; pre-select the TARGET doc type.
			_lstDocTypes.Enabled = false;
			for (int i = 0; i < _docTypes.Count; i++)
			{
				if (_docTypes[i].DocTypeID == targetDocType.DocTypeID)
				{
					_lstDocTypes.SelectedIndex = i;
					_selectedDocType = _docTypes[i];
					break;
				}
			}

			// Seed the transient list from the target DocType's canonical nodes.
			if (_selectedDocType is not null)
				LoadTransientList(_selectedDocType);

			string src = sourceInstance.InstanceID.Length >= 8
				? sourceInstance.InstanceID[..8] + "..."
				: sourceInstance.InstanceID;
			SetStatus(
				$"Build-to: {targetDocType.Name}  —  " +
				$"source {src}  —  right-click to manage clauses, then Generate.",
				success: true);
		}

		private void InitBuildFromMode(Instance sourceInstance)
		{
			this.Text = "MAKEDOC — Build From Document";

			// Lock the doc type picker — can't switch types in build-from mode.
			_lstDocTypes.Enabled = false;

			// Pre-select the matching doc type.
			for (int i = 0; i < _docTypes.Count; i++)
			{
				if (_docTypes[i].DocTypeID == sourceInstance.DocTypeID)
				{
					_lstDocTypes.SelectedIndex = i;
					_selectedDocType = _docTypes[i];
					break;
				}
			}

			LoadTransientListFromInstance(sourceInstance);

			string src = sourceInstance.InstanceID.Length >= 8
				? sourceInstance.InstanceID[..8] + "..."
				: sourceInstance.InstanceID;
			SetStatus(
				$"Build-from: {_selectedDocType?.Name ?? sourceInstance.DocTypeID}  —  " +
				$"source {src}  —  right-click to manage clauses, then Generate.",
				success: true);
		}

		private void LoadTransientListFromInstance(Instance sourceInstance)
		{
			_transientNodeList.Clear();
			_canonicalNodeIds.Clear();

			if (string.IsNullOrWhiteSpace(sourceInstance.NodeList))
			{
				SetStatus("Source instance has no node list.", success: false);
				_btnGenerate.Enabled = false;
				RefreshListView();
				return;
			}

			List<string>? nodeIds;
			try
			{
				nodeIds = JsonSerializer.Deserialize<List<string>>(sourceInstance.NodeList);
			}
			catch
			{
				SetStatus("Could not parse source instance node list.", success: false);
				_btnGenerate.Enabled = false;
				RefreshListView();
				return;
			}

			if (nodeIds is null || nodeIds.Count == 0)
			{
				SetStatus("Source instance node list is empty.", success: false);
				_btnGenerate.Enabled = false;
				RefreshListView();
				return;
			}

			var missingIds = new List<string>();

			foreach (var id in nodeIds)
			{
				var node = _nodeService.GetById(id);
				if (node is null)
				{
					missingIds.Add(id);
					continue;
				}
				_transientNodeList.Add(new TransientNodeEntry
				{
					NodeID   = id,
					Title    = node.Title    ?? string.Empty,
					NodeType = node.NodeType ?? NodeTypes.Clause
				});
				_canonicalNodeIds.Add(id);
			}

			if (missingIds.Count > 0)
			{
				string bullets = string.Join("\n  • ", missingIds);
				MessageBox.Show(
					$"The following {missingIds.Count} node(s) from the source document " +
					$"no longer exist in the database and were skipped:\n\n  • {bullets}",
					"Build From — Missing Nodes",
					MessageBoxButtons.OK,
					MessageBoxIcon.Warning);
			}

			RefreshListView();
			_btnGenerate.Enabled = _transientNodeList.Count > 0;
		}

		// ─────────────────────────────────────────────────────────────
		// UI Construction
		// ─────────────────────────────────────────────────────────────
		private void BuildUI()
		{
			this.Text          = "MAKEDOC — Document Assembly";
			this.Size          = new Size(960, 640);
			this.MinimumSize   = new Size(780, 520);
			this.StartPosition = FormStartPosition.CenterScreen;
			this.BackColor     = Color.White;
			this.Font          = new Font("Segoe UI", 9.5f);

			_mainLayout = new TableLayoutPanel
			{
				Dock        = DockStyle.Fill,
				ColumnCount = 1,
				RowCount    = 3,
				Padding     = new Padding(24)
			};
			_mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
			_mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
			_mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
			this.Controls.Add(_mainLayout);

			BuildHeader();
			BuildContent();
			BuildActionBar();
			BuildContextMenu();
		}

		private void BuildHeader()
		{
			var panel = new Panel { Dock = DockStyle.Fill };

			panel.Controls.Add(new Label
			{
				Text      = "Document Assembly",
				Font      = new Font("Segoe UI", 14f, FontStyle.Regular),
				ForeColor = Color.FromArgb(30, 30, 30),
				AutoSize  = true,
				Location  = new Point(0, 2)
			});
			panel.Controls.Add(new Label
			{
				Text      = "Select a document type, manage the clause list, then generate.",
				Font      = new Font("Segoe UI", 9f),
				ForeColor = Color.FromArgb(110, 110, 110),
				AutoSize  = true,
				Location  = new Point(0, 32)
			});

			_mainLayout.Controls.Add(panel, 0, 0);
		}

		private void BuildContent()
		{
			var content = new TableLayoutPanel
			{
				Dock        = DockStyle.Fill,
				ColumnCount = 2,
				RowCount    = 1,
				Padding     = new Padding(0, 8, 0, 8)
			};
			content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
			content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100));

			var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0) };

			leftPanel.Controls.Add(new Label
			{
				Text      = "Document Type",
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
			leftPanel.Controls.Add(_lstDocTypes);

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
				Font          = new Font("Segoe UI", 9f),
				MultiSelect   = false
			};
			_lstNodes.Columns.Add("Node ID",  100);
			_lstNodes.Columns.Add("Type",      90);
			_lstNodes.Columns.Add("Status",    72);
			_lstNodes.Columns.Add("Title",     380);

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
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100));
			bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));

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
			_btnGenerate.FlatAppearance.BorderSize = 0;
			_btnGenerate.Click += OnGenerateClicked;

			bar.Controls.Add(_lblStatus,   0, 0);
			bar.Controls.Add(_btnGenerate, 1, 0);
			_mainLayout.Controls.Add(bar, 0, 2);
		}

		private void BuildContextMenu()
		{
			_ctxMenu = new ContextMenuStrip { Font = new Font("Segoe UI", 9.5f) };

			_miInsertUser    = new ToolStripMenuItem("Insert User Clause...");
			_miDelete        = new ToolStripMenuItem("Delete Clause");
			_miEdit          = new ToolStripMenuItem("Edit Clause");
			_miMoveUp        = new ToolStripMenuItem("Move Up");
			_miMoveDown      = new ToolStripMenuItem("Move Down");
			_miBrowse        = new ToolStripMenuItem("Browse Clause");
			_miReset         = new ToolStripMenuItem("Reset Clause List");
			_miInsertSpecial = new ToolStripMenuItem("Insert Special Clauses...");

			_miInsertUser.Click    += OnInsertUserClause;
			_miDelete.Click        += OnDeleteClause;
			_miEdit.Click          += OnEditClause;
			_miMoveUp.Click        += OnMoveUp;
			_miMoveDown.Click      += OnMoveDown;
			_miBrowse.Click        += OnBrowseClause;
			_miReset.Click         += OnResetClauseList;
			_miInsertSpecial.Click += OnInsertSpecialClauses;

			_ctxMenu.Items.AddRange(new ToolStripItem[]
			{
				_miInsertUser,
				_miDelete,
				_miEdit,
				_miMoveUp,
				_miMoveDown,
				_miBrowse,
				new ToolStripSeparator(),
				_miReset,
				_miInsertSpecial
			});

			_ctxMenu.Opening += OnContextMenuOpening;
			_lstNodes.ContextMenuStrip = _ctxMenu;
		}

		// ─────────────────────────────────────────────────────────────
		// Data loading
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

		private void LoadTransientList(DocType docType)
		{
			_transientNodeList.Clear();
			_canonicalNodeIds.Clear();

			if (string.IsNullOrWhiteSpace(docType.HeaderNodeID))
			{
				_lblNodeCount.Text   = "Clause list -- no header node configured";
				_btnGenerate.Enabled = false;
				RefreshListView();
				return;
			}

			var orderedIds = _hierarchyService.GetOrderedNodeIds(
				docType.DocTypeID, docType.HeaderNodeID);

			_canonicalNodeIds = new List<string>(orderedIds);

			foreach (var id in orderedIds)
			{
				var node = _nodeService.GetById(id);
				_transientNodeList.Add(new TransientNodeEntry
				{
					NodeID   = id,
					Title    = node?.Title    ?? string.Empty,
					NodeType = node?.NodeType ?? NodeTypes.Clause
				});
			}

			RefreshListView();
			_btnGenerate.Enabled = _transientNodeList.Count > 0;
		}

		// ─────────────────────────────────────────────────────────────
		// ListView refresh
		// ─────────────────────────────────────────────────────────────
		private void RefreshListView()
		{
			_lstNodes.BeginUpdate();
			_lstNodes.Items.Clear();
			foreach (var entry in _transientNodeList)
				_lstNodes.Items.Add(MakeListItem(entry));
			_lstNodes.EndUpdate();

			int total   = _transientNodeList.Count;
			int userIns = _transientNodeList.Count(e => e.IsUserInsertion);
			int edited  = _transientNodeList.Count(e => e.IsEdited);

			var parts = new List<string> { $"{total} clause(s)" };
			if (userIns > 0) parts.Add($"{userIns} user insertion(s)");
			if (edited  > 0) parts.Add($"{edited} edited");

			_lblNodeCount.Text = "Clause list -- " + string.Join(", ", parts);
		}

		private static ListViewItem MakeListItem(TransientNodeEntry entry)
		{
			var item = new ListViewItem(entry.NodeID);
			item.SubItems.Add(entry.NodeType);
			item.SubItems.Add(entry.Badge);
			item.SubItems.Add(entry.Title);

			item.BackColor =
				entry.IsUserInsertion ? ClrUserInsertion :
				entry.IsEdited        ? ClrEdited        :
				entry.IsSpecialClause ? ClrSpecial       :
				Color.White;

			return item;
		}

		// ─────────────────────────────────────────────────────────────
		// Selection helpers
		// ─────────────────────────────────────────────────────────────
		private int GetSelectedIndex()
		{
			return _lstNodes.SelectedIndices.Count == 0
				? -1
				: _lstNodes.SelectedIndices[0];
		}

		private TransientNodeEntry? GetSelectedEntry()
		{
			int idx = GetSelectedIndex();
			return idx < 0 ? null : _transientNodeList[idx];
		}

		// ─────────────────────────────────────────────────────────────
		// Event handlers -- DocType selection
		// ─────────────────────────────────────────────────────────────
		private void OnDocTypeSelected(object? sender, EventArgs e)
		{
			int idx = _lstDocTypes.SelectedIndex;
			if (idx < 0 || idx >= _docTypes.Count) return;

			_selectedDocType = _docTypes[idx];

			try
			{
				LoadTransientList(_selectedDocType);
				SetStatus(
					$"Selected: {_selectedDocType.Name}  --  " +
					"right-click any clause to manage it, then click Generate.",
					success: true);
			}
			catch (Exception ex)
			{
				SetStatus($"Error loading nodes: {ex.Message}", success: false);
			}
		}

		// ─────────────────────────────────────────────────────────────
		// Context menu -- enable/disable before opening
		// ─────────────────────────────────────────────────────────────
		private void OnContextMenuOpening(object? sender,
			System.ComponentModel.CancelEventArgs e)
		{
			if (_selectedDocType is null) { e.Cancel = true; return; }

			int  idx    = GetSelectedIndex();
			bool hasSel = idx >= 0;
			var  entry  = hasSel ? _transientNodeList[idx] : null;

			_miEdit.Enabled     = hasSel && entry?.IsSpecialClause == false;
			_miBrowse.Enabled   = hasSel;
			_miDelete.Enabled   = hasSel;
			_miMoveUp.Enabled   = hasSel && idx > 0;
			_miMoveDown.Enabled = hasSel && idx < _transientNodeList.Count - 1;

			_miInsertUser.Enabled    = true;
			_miReset.Enabled         = true;
			_miInsertSpecial.Enabled =
				!string.IsNullOrWhiteSpace(_selectedDocType.InclusionTags);
		}

		// ─────────────────────────────────────────────────────────────
		// Context menu handlers
		// ─────────────────────────────────────────────────────────────

		private void OnInsertUserClause(object? sender, EventArgs e)
		{
			using var dlg = new OpenFileDialog
			{
				Title  = "Select a clause DOCX file",
				Filter = "Word documents (*.docx)|*.docx"
			};
			if (dlg.ShowDialog(this) != DialogResult.OK) return;

			byte[] content;
			try { content = File.ReadAllBytes(dlg.FileName); }
			catch (Exception ex)
			{
				MessageBox.Show($"Could not read the file:\n{ex.Message}",
					"Insert User Clause", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			var entry = new TransientNodeEntry
			{
				NodeID          = "TMP-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
				Title           = Path.GetFileNameWithoutExtension(dlg.FileName),
				NodeType        = NodeTypes.Clause,
				IsUserInsertion = true,
				EditedContent   = content,
				SourcePath      = dlg.FileName
			};

			int insertAt = GetSelectedIndex() >= 0
				? GetSelectedIndex()
				: _transientNodeList.Count;

			_transientNodeList.Insert(insertAt, entry);
			_isDirty = true;
			RefreshListView();

			if (insertAt < _lstNodes.Items.Count)
			{
				_lstNodes.Items[insertAt].Selected = true;
				_lstNodes.Items[insertAt].EnsureVisible();
			}

			SetStatus($"Inserted user clause \"{Path.GetFileName(dlg.FileName)}\".", success: true);
		}

		private void OnDeleteClause(object? sender, EventArgs e)
		{
			int idx = GetSelectedIndex();
			if (idx < 0) return;

			_transientNodeList.RemoveAt(idx);
			_isDirty = true;
			RefreshListView();

			int newSel = Math.Min(idx, _transientNodeList.Count - 1);
			if (newSel >= 0)
			{
				_lstNodes.Items[newSel].Selected = true;
				_lstNodes.Items[newSel].EnsureVisible();
			}

			SetStatus("Clause removed from list.", success: true);
		}

		private void OnEditClause(object? sender, EventArgs e)
		{
			int idx = GetSelectedIndex();
			if (idx < 0) return;

			var entry = _transientNodeList[idx];

			if (entry.IsSpecialClause)
			{
				MessageBox.Show("Special-clause nodes cannot be edited.",
					"Edit Clause", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			byte[]? blob = ResolveBlobForEntry(entry);
			if (blob is null || blob.Length == 0)
			{
				MessageBox.Show("This clause has no DOCX content to edit.",
					"Edit Clause", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			byte[]? updated = OpenInWord(blob, readOnly: false);
			if (updated is null) return;

			entry.IsEdited      = true;
			entry.EditedContent = updated;
			_isDirty = true;
			RefreshListView();

			if (idx < _lstNodes.Items.Count)
			{
				_lstNodes.Items[idx].Selected = true;
				_lstNodes.Items[idx].EnsureVisible();
			}

			SetStatus($"Clause \"{entry.Title}\" saved as edited.", success: true);
		}

		private void OnMoveUp(object? sender, EventArgs e)
		{
			int idx = GetSelectedIndex();
			if (idx <= 0) return;

			(_transientNodeList[idx], _transientNodeList[idx - 1]) =
			(_transientNodeList[idx - 1], _transientNodeList[idx]);

			_isDirty = true;
			RefreshListView();
			_lstNodes.Items[idx - 1].Selected = true;
			_lstNodes.Items[idx - 1].EnsureVisible();
		}

		private void OnMoveDown(object? sender, EventArgs e)
		{
			int idx = GetSelectedIndex();
			if (idx < 0 || idx >= _transientNodeList.Count - 1) return;

			(_transientNodeList[idx], _transientNodeList[idx + 1]) =
			(_transientNodeList[idx + 1], _transientNodeList[idx]);

			_isDirty = true;
			RefreshListView();
			_lstNodes.Items[idx + 1].Selected = true;
			_lstNodes.Items[idx + 1].EnsureVisible();
		}

		private void OnBrowseClause(object? sender, EventArgs e)
		{
			var entry = GetSelectedEntry();
			if (entry is null) return;

			byte[]? blob = ResolveBlobForEntry(entry);
			if (blob is null || blob.Length == 0)
			{
				MessageBox.Show("This clause has no DOCX content to browse.",
					"Browse Clause", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			OpenInWord(blob, readOnly: true);
			}

		private void OnResetClauseList(object? sender, EventArgs e)
		{
			bool isDirty = _transientNodeList.Any(
				en => en.IsEdited || en.IsUserInsertion || en.IsSpecialClause);

			if (_buildToTargetDocType is not null)
			{
				// Build-to mode: reset to the target DocType's canonical list.
				if (isDirty)
				{
					var confirm = MessageBox.Show(
						"This will discard all edits, insertions, and deletions, and " +
						"restore the canonical clause list for the target document type.\n\nContinue?",
						"Reset Clause List",
						MessageBoxButtons.YesNo,
						MessageBoxIcon.Warning);
					if (confirm != DialogResult.Yes) return;
				}
				LoadTransientList(_buildToTargetDocType);
				_isDirty = false;
				SetStatus("Clause list reset to canonical (target document type).", success: true);
				return;
			}

			if (_buildFromInstance is not null)
			{
				// Build-from mode: reset to source instance's node list.
				if (isDirty)
				{
					var confirm = MessageBox.Show(
						"This will discard all edits, insertions, and deletions, and " +
						"restore the clause list from the source document.\n\nContinue?",
						"Reset Clause List",
						MessageBoxButtons.YesNo,
						MessageBoxIcon.Warning);
					if (confirm != DialogResult.Yes) return;
				}
				LoadTransientListFromInstance(_buildFromInstance);
				_isDirty = false;
				SetStatus("Clause list reset to source document.", success: true);
				return;
			}

			// Canonical mode: reset to NodeHierarchy.
			if (_selectedDocType is null) return;

			if (isDirty)
			{
				var confirm = MessageBox.Show(
					"This will discard all edits, insertions, and deletions, and " +
					"restore the original clause list for this document type.\n\nContinue?",
					"Reset Clause List",
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Warning);
				if (confirm != DialogResult.Yes) return;
			}

			LoadTransientList(_selectedDocType);
			_isDirty = false;
			SetStatus("Clause list reset to canonical.", success: true);
		}

		private void OnInsertSpecialClauses(object? sender, EventArgs e)
		{
			if (_selectedDocType is null) return;
			if (string.IsNullOrWhiteSpace(_selectedDocType.InclusionTags)) return;

			List<InclusionTag> tags;
			try
			{
				tags = JsonSerializer.Deserialize<List<InclusionTag>>(
					_selectedDocType.InclusionTags,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
					?? new List<InclusionTag>();

				// Discard any entries missing required fields.
				tags = tags
					.Where(t => !string.IsNullOrWhiteSpace(t.TagName)
					         && !string.IsNullOrWhiteSpace(t.DocTypeId))
					.ToList();
			}
			catch
			{
				// Fallback: legacy object format {"DisplayLabel": "docTypeId", ...}
				// stored before the array format was adopted.
				try
				{
					var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(
						_selectedDocType.InclusionTags);
					tags = dict?
						.Where(kv => !string.IsNullOrWhiteSpace(kv.Key)
						          && !string.IsNullOrWhiteSpace(kv.Value))
						.Select(kv => new InclusionTag(kv.Key, kv.Value))
						.ToList()
						?? new List<InclusionTag>();
				}
				catch
				{
					tags = new List<InclusionTag>();
				}
			}

			if (tags.Count == 0)
			{
				// Check whether the data is in the legacy string-array format
				// (e.g. ["DEI","HAZMAT"]) -- it can be parsed but has no docTypeId.
				bool isLegacy = false;
				try
				{
					var legacy = JsonSerializer.Deserialize<List<string>>(
						_selectedDocType.InclusionTags);
					isLegacy = legacy is not null && legacy.Count > 0;
				}
				catch { /* not legacy either */ }

				if (isLegacy)
				{
					MessageBox.Show(
						"The InclusionTags for this document type are in the old " +
						"string-array format (e.g. [\"DEI\",\"HAZMAT\"]).\n\n" +
						"Insert Special Clauses requires the updated format that " +
						"includes a DocType ID for each tag:\n\n" +
						"[{\"tagName\": \"DEI\", \"docTypeId\": \"<DocTypeID>\"}, ...]",
						"Insert Special Clauses -- Data Update Required",
						MessageBoxButtons.OK,
						MessageBoxIcon.Information);
				}
				else
				{
					MessageBox.Show(
						"No valid inclusion tags are defined for this document type.\n\n" +
						"InclusionTags must be a JSON array in the format:\n" +
						"[{\"tagName\": \"tag label\", \"docTypeId\": \"DocTypeID\"}]",
						"Insert Special Clauses",
						MessageBoxButtons.OK,
						MessageBoxIcon.Information);
				}
				return;
			}

			InclusionTag? selected = ShowTagPicker(tags);
			if (selected is null) return;

			var groupDocType = _db.GetDocType(selected.DocTypeId);
			if (groupDocType is null || string.IsNullOrWhiteSpace(groupDocType.HeaderNodeID))
			{
				MessageBox.Show(
					$"The node group for tag \"{selected.TagName}\" has no header node configured.",
					"Insert Special Clauses", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			var groupIds = _hierarchyService.GetOrderedNodeIds(
				selected.DocTypeId, groupDocType.HeaderNodeID);

			if (groupIds.Count == 0)
			{
				MessageBox.Show(
					$"The node group \"{selected.TagName}\" contains no nodes.",
					"Insert Special Clauses", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Special-clause nodes cannot be edited; no UC- rows are written for them.
			var newEntries = groupIds.Select(id =>
			{
				var node = _nodeService.GetById(id);
				return new TransientNodeEntry
				{
					NodeID          = id,
					Title           = node?.Title    ?? string.Empty,
					NodeType        = node?.NodeType ?? NodeTypes.Clause,
					IsSpecialClause = true
				};
			}).ToList();

			int insertAt = GetSelectedIndex() >= 0
				? GetSelectedIndex() + 1
				: _transientNodeList.Count;

			_transientNodeList.InsertRange(insertAt, newEntries);
			_isDirty = true;
			RefreshListView();

			SetStatus(
				$"Inserted {groupIds.Count} special clause(s) from \"{selected.TagName}\".",
				success: true);
		}

		// ─────────────────────────────────────────────────────────────
		// Generate Document -- shared procedure (create-new-document.md)
		// Steps:
		//   1. Collect clause blobs; scan for fill-in variable names
		//   2. Open FillinForm to collect values
		//   3. Write UC- nodes for edited/inserted clauses
		//   4. Persist Instance row with final node IDs and fill-in data
		//   5. Substitute fill-in values in memory; assemble DOCX
		//   6. Save dialog; write file
		// ─────────────────────────────────────────────────────────────
		private void OnGenerateClicked(object? sender, EventArgs e)
		{
			if (_selectedDocType is null || _transientNodeList.Count == 0) return;

			try
			{
				_btnGenerate.Enabled = false;

				// Step 1 -- collect clause and header-node blobs; scan both for fill-ins
				SetStatus("Scanning clauses for fill-in variables...", success: true);

				var scanBlobs = new List<byte[]>();
				foreach (var entry in _transientNodeList)
				{
					// Scan clause nodes AND header nodes (spec step 2).
					if (entry.NodeType != NodeTypes.Clause &&
					    entry.NodeType != NodeTypes.HeaderNode) continue;
					var b = ResolveBlobForEntry(entry);
					if (b is not null && b.Length > 0) scanBlobs.Add(b);
				}

				var fillinNames = _fillinService.ScanFillins(scanBlobs);

				// Step 2 & 3 -- always show FillinForm (user provides title + fill-ins).
				// In build-from mode, pre-populate with the source instance's values.
				SetStatus("Opening fill-in form...", success: true);

				IReadOnlyDictionary<string, string>? preFill = null;
				string? preTitle = null;

				if (_buildFromInstance is not null)
				{
					preTitle = _buildFromInstance.Title;

					if (!string.IsNullOrWhiteSpace(_buildFromInstance.FillinData))
					{
						try
						{
							preFill = JsonSerializer.Deserialize<Dictionary<string, string>>(
								_buildFromInstance.FillinData);
						}
						catch { /* best effort — leave null */ }
					}
				}

				string documentTitle;
				Dictionary<string, string> fillinValues;
				using (var fillinForm = new FillinForm(fillinNames, preFill, preTitle))
				{
					if (fillinForm.ShowDialog(this) != DialogResult.OK)
					{
						SetStatus("Generation cancelled.", success: true);
						return;
					}
					documentTitle = fillinForm.DocumentTitle;
					fillinValues  = fillinForm.Values
						.ToDictionary(kv => kv.Key, kv => kv.Value);
				}

				// Step 3 -- write UC- nodes for edited/inserted clauses
				SetStatus("Writing user-clause nodes...", success: true);

				var finalNodeIds = new List<string>();
				foreach (var entry in _transientNodeList)
				{
					bool needsUcRow =
						(entry.IsEdited || entry.IsUserInsertion)
						&& entry.EditedContent is not null
						&& entry.NodeType == NodeTypes.Clause;

					if (needsUcRow)
					{
						string ucId = "UC-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
						_nodeService.InsertUserClause(
							nodeId:      ucId,
							nodeType:    entry.NodeType,
							title:       entry.Title,
							content:     entry.EditedContent,
							derivedFrom: entry.IsEdited ? entry.NodeID : null);
						finalNodeIds.Add(ucId);
					}
					else
					{
						finalNodeIds.Add(entry.NodeID);
					}
				}

				// Step 4 -- persist Instance
				SetStatus("Persisting instance...", success: true);

				var instance = new Instance
				{
					InstanceID    = Guid.NewGuid().ToString(),
					DocTypeID     = _selectedDocType.DocTypeID,
					Title         = documentTitle,
					BuildFromID   = _buildFromInstance?.InstanceID,
					NodeList      = JsonSerializer.Serialize(finalNodeIds),
					FillinData    = JsonSerializer.Serialize(fillinValues),
					GeneratedDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")
				};
				_db.InsertInstance(instance);

				// Step 4a -- attach line items to the new instance.
				// Build-from/build-to: carry the source instance's line
				// items through. Fresh build: re-key the items staged
				// against the doctype in the Line Item form.
				if (_buildFromInstance is not null)
				{
					var sourceItems = _db.GetLineItemsForInstance(
						_buildFromInstance.InstanceID);

					if (sourceItems.Count > 0)
						_db.CopyLineItemsToInstance(
							_buildFromInstance.InstanceID, instance.InstanceID);
					else
						// Legacy: source built before instance-keying;
						// its items may still sit in doctype staging.
						_db.AssignLineItemsToInstance(
							_buildFromInstance.DocTypeID, instance.InstanceID);
				}
				else
				{
					_db.AssignLineItemsToInstance(
						_selectedDocType.DocTypeID, instance.InstanceID);
				}

				// Step 4b -- line item review modal (build-from / build-to only).
				// The new instance already owns its line items (copied in step 4a).
				// The user can view or adjust them; closing without saving keeps
				// the copied items unchanged. Either way, assembly proceeds.
				if (_buildFromInstance is not null)
				{
					SetStatus("Opening line item review...", success: true);
					using var liForm = new LineItemForm(instance.InstanceID);
					liForm.ShowDialog(this);
				}

				// Step 5 -- substitute fill-ins, assemble
				SetStatus($"Assembling {finalNodeIds.Count} node(s)...", success: true);

				// Load line items for {lineitem} tag substitution -- now
				// owned by the new instance.
				var lineItems = _db.GetLineItemsForInstance(instance.InstanceID)
				                   .AsReadOnly();

				var assemblyBlobs = new List<byte[]>();
				var missingNodes  = new List<string>();

				// Step 11 -- section number counter; increments once per clause node.
				int secNo = 0;

				foreach (var entry in _transientNodeList)
				{
					var blob = ResolveBlobForEntry(entry);
					if (blob is null || blob.Length == 0)
					{
						missingNodes.Add(entry.NodeID);
						continue;
					}

					if (entry.NodeType == NodeTypes.Clause)
					{
						secNo++;

						// Substitute SDT fill-ins.
						if (fillinValues.Count > 0)
							blob = _fillinService.SubstituteFillins(blob, fillinValues);

						// Substitute {SecNo} plain-text tag.
						blob = _fillinService.SubstitutePlainText(
							blob, "{SecNo}", secNo.ToString());

						// Substitute {lineitem} plain-text tag with generated table.
						blob = _fillinService.SubstituteLineItemTable(blob, lineItems);
					}
					else if (entry.NodeType == NodeTypes.HeaderNode && fillinValues.Count > 0)
					{
						// Header nodes participate in SDT fill-in substitution
						// but not in section numbering.
						blob = _fillinService.SubstituteFillins(blob, fillinValues);
					}

					assemblyBlobs.Add(blob);
				}

				if (missingNodes.Count > 0)
				{
					string preview = string.Join(", ", missingNodes.Take(5));
					if (missingNodes.Count > 5) preview += "...";
					SetStatus(
						$"Warning: {missingNodes.Count} node(s) had no content (skipped): {preview}",
						success: false);
				}

				if (assemblyBlobs.Count == 0)
				{
					SetStatus("Cannot generate -- no content blobs available.", success: false);
					return;
				}

				byte[] assembled = _assemblyService.Assemble(assemblyBlobs);

				// Step 6 -- save dialog
				string defaultName = SanitizeFilename(
					$"{_selectedDocType.Name}_{instance.InstanceID[..8]}_{DateTime.Now:yyyyMMdd-HHmm}.docx");

				using var saveDlg = new SaveFileDialog
				{
					Title            = "Save assembled document",
					Filter           = "Word document (*.docx)|*.docx",
					FileName         = defaultName,
					DefaultExt       = "docx",
					AddExtension     = true,
					InitialDirectory = ResolveAssembledDocumentsDirectory()
				};

				if (saveDlg.ShowDialog(this) != DialogResult.OK)
				{
					SetStatus("Document not saved -- instance was recorded.", success: true);
					return;
				}

				File.WriteAllBytes(saveDlg.FileName, assembled);
				_isDirty = false;

				SetStatus(
					$"Saved: {Path.GetFileName(saveDlg.FileName)}  " +
					$"(instance {instance.InstanceID[..8]}..., {finalNodeIds.Count} nodes).",
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

		// ─────────────────────────────────────────────────────────────
		// Form close -- dirty-state prompt
		// ─────────────────────────────────────────────────────────────
		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (_isDirty)
			{
				var result = MessageBox.Show(
					"Click Generate Document to keep changes.\n\n" +
					"Are you sure you want to close without generating?",
					"Close without generating?",
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Question,
					MessageBoxDefaultButton.Button2);

				if (result != DialogResult.Yes)
				{
					e.Cancel = true;
					return;
				}
			}
			base.OnFormClosing(e);
		}

		// ─────────────────────────────────────────────────────────────
		// Word integration
		// ─────────────────────────────────────────────────────────────
		private byte[]? OpenInWord(byte[] blob, bool readOnly)
		{
			string tempPath = Path.Combine(
				Path.GetTempPath(), $"makedoc_{Guid.NewGuid():N}.docx");

			File.WriteAllBytes(tempPath, blob);

			if (readOnly)
				File.SetAttributes(tempPath, FileAttributes.ReadOnly);

			// Start Word (or whatever app owns .docx) via ShellExecute.
			Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });

			if (readOnly)
			{
				// Show a modal prompt so the file is NOT deleted while Word still has it open.
				// The user dismisses this only after closing the document in Word.
				MessageBox.Show(
					"The clause is open in Word for review.\n\n" +
					"When you are finished, close the document in Word, then click OK.",
					"Browse Clause",
					MessageBoxButtons.OK,
					MessageBoxIcon.Information);

				TryDeleteTempFile(tempPath, markWritable: true);
				return null;
			}

			// Editable path: give Word a moment to fully load the document before
			// showing our dialog. Without this, a quick OK click can delete the file
			// before Word has finished opening it (race condition).
			System.Threading.Thread.Sleep(1500);

			var result = MessageBox.Show(
				"The clause is open in Word.\n\n" +
				"Edit and save the file in Word, then click OK to import your changes.\n" +
				"Click Cancel to discard any changes.",
				"Edit Clause",
				MessageBoxButtons.OKCancel,
				MessageBoxIcon.Information);

			if (result != DialogResult.OK)
			{
				TryDeleteTempFile(tempPath, markWritable: false);
				return null;
			}

			try
			{
				byte[] updated = File.ReadAllBytes(tempPath);
				TryDeleteTempFile(tempPath, markWritable: false);
				return updated;
			}
			catch (Exception ex)
			{
				MessageBox.Show(
					"Could not read the edited file.\n\n" +
					"Make sure you have saved and closed the Word document before clicking OK.\n\n" +
					$"Error: {ex.Message}",
					"Edit Clause",
					MessageBoxButtons.OK,
					MessageBoxIcon.Warning);
				TryDeleteTempFile(tempPath, markWritable: false);
				return null;
			}
		}

		private static void TryDeleteTempFile(string path, bool markWritable)
		{
			try
			{
				if (!File.Exists(path)) return;
				if (markWritable) File.SetAttributes(path, FileAttributes.Normal);
				File.Delete(path);
			}
			catch { /* best effort */ }
		}

		// ─────────────────────────────────────────────────────────────
		// Helpers
		// ─────────────────────────────────────────────────────────────
		private byte[]? ResolveBlobForEntry(TransientNodeEntry entry)
		{
			if (entry.EditedContent is not null && entry.EditedContent.Length > 0)
				return entry.EditedContent;
			return _nodeService.GetById(entry.NodeID)?.Content;
		}

		private static string SanitizeFilename(string name)
		{
			foreach (var c in Path.GetInvalidFileNameChars())
				name = name.Replace(c, '_');
			return name;
		}

		// ── Save-dialog default directory ─────────────────────────────
		// Reads assembledDocumentsDirectory from makedoc.config.json,
		// located at <MAKEDOC_DB>/../config/makedoc.config.json.
		// Returns "" on any failure — SaveFileDialog ignores an empty
		// InitialDirectory and falls back to the last-used folder.
		private static string ResolveAssembledDocumentsDirectory()
		{
			try
			{
				string dbDir = Environment.GetEnvironmentVariable("MAKEDOC_DB");
				if (string.IsNullOrWhiteSpace(dbDir))
					return "";

				string configPath = Path.GetFullPath(
					Path.Combine(dbDir, "..", "config", "makedoc.config.json"));
				if (!File.Exists(configPath))
					return "";

				using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
				if (!doc.RootElement.TryGetProperty(
						"assembledDocumentsDirectory", out var dirElement))
					return "";

				string dir = dirElement.GetString();
				if (string.IsNullOrWhiteSpace(dir))
					return "";

				dir = Path.GetFullPath(dir);
				return Directory.Exists(dir) ? dir : "";
			}
			catch
			{
				return "";
			}
		}

		private void SetStatus(string message, bool success)
		{
			_lblStatus.Text      = $"●  {message}";
			_lblStatus.ForeColor = success
				? Color.FromArgb(15, 110, 86)
				: Color.FromArgb(180, 40, 40);
		}

		// ── Inclusion-tag picker ──────────────────────────────────────
		private InclusionTag? ShowTagPicker(List<InclusionTag> tags)
		{
			using var dlg = new Form
			{
				Text            = "Insert Special Clauses",
				Size            = new Size(380, 240),
				StartPosition   = FormStartPosition.CenterParent,
				FormBorderStyle = FormBorderStyle.FixedDialog,
				MaximizeBox     = false,
				MinimizeBox     = false,
				BackColor       = Color.White,
				Font            = new Font("Segoe UI", 9.5f)
			};

			dlg.Controls.Add(new Label
			{
				Text     = "Select an inclusion tag to insert:",
				AutoSize = true,
				Location = new Point(16, 16)
			});

			var lst = new ListBox
			{
				Location       = new Point(16, 42),
				Size           = new Size(336, 100),
				BorderStyle    = BorderStyle.FixedSingle,
				IntegralHeight = false
			};
			foreach (var t in tags) lst.Items.Add(t.TagName);
			if (lst.Items.Count > 0) lst.SelectedIndex = 0;
			dlg.Controls.Add(lst);

			var btnOK = new Button
			{
				Text      = "Insert",
				Location  = new Point(172, 160),
				Size      = new Size(80, 30),
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(30, 30, 30),
				ForeColor = Color.White
			};
			btnOK.FlatAppearance.BorderSize = 0;
			btnOK.Click += (_, _) => dlg.DialogResult = DialogResult.OK;

			var btnCancel = new Button
			{
				Text      = "Cancel",
				Location  = new Point(264, 160),
				Size      = new Size(80, 30),
				FlatStyle = FlatStyle.Flat
			};
			btnCancel.Click += (_, _) => dlg.DialogResult = DialogResult.Cancel;

			dlg.Controls.Add(btnOK);
			dlg.Controls.Add(btnCancel);
			dlg.AcceptButton = btnOK;
			dlg.CancelButton = btnCancel;

			return dlg.ShowDialog(this) == DialogResult.OK && lst.SelectedIndex >= 0
				? tags[lst.SelectedIndex]
				: null;
		}
	}

	// ── InclusionTag record for JSON deserialization ──────────────────────
	// Shape per create-new-document.md:
	//   [{ "tagName": "<display label>", "docTypeId": "<DocTypeID>" }]
	internal record InclusionTag(
		[property: System.Text.Json.Serialization.JsonPropertyName("tagName")]   string TagName,
		[property: System.Text.Json.Serialization.JsonPropertyName("docTypeId")] string DocTypeId);
}
