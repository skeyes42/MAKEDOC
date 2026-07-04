using MakeDoc.App.Forms.Admin;
using MakeDoc.App.Forms.Analytics;
using MakeDoc.App.Forms.Assembly;
using MakeDoc.App.Forms.Lineitem;
using MakeDoc.Core.Data;
using MakeDoc.Core.Models;
using MakeDoc.Core.Services;
using System.Diagnostics;
using System.Text.Json;

namespace MakeDoc.App.Forms
{
    // ─────────────────────────────────────────────────────────────────────
    // MainDashboard
    //
    // Per ADR-007 / docs/features/build-from.md, the dashboard is centered
    // on the Instance table — the list of prior assembled documents. This
    // is the read-only slice: list rows, no actions yet. The "+ New Document"
    // button, the "Build from this" row action, and the build-from picker
    // come in subsequent slices.
    //
    // Archived rows are visible-but-de-emphasized (reduced opacity / grey
    // foreground) and not selectable — the Archive form is the place to
    // un-archive.
    //
    // Secondary entry points (Admin, Analytics, Archive, direct Assembly)
    // live in the top menu bar.
    // ─────────────────────────────────────────────────────────────────────
    public partial class MainDashboard : Form
    {
        private readonly MakDocDb _db;

        // ── Controls ──────────────────────────────────
        private MenuStrip        _menu        = null!;
        private TableLayoutPanel _mainLayout  = null!;
        private Panel            _headerPanel = null!;
        private ListView         _lvInstances = null!;
        private Label            _lblStatus   = null!;
        private ContextMenuStrip _ctxInstances = null!;

        // ── Dashboard context-menu items (stored for enable/visibility control) ──
        private ToolStripMenuItem _miLineItems   = null!;
        private ToolStripMenuItem _miBuildFrom   = null!;
        private ToolStripMenuItem _miBuildToSol  = null!;
        private ToolStripMenuItem _miBuildToAwd  = null!;

        public MainDashboard()
        {
            InitializeComponent();
            BuildUI();

            try
            {
                _db = new MakDocDb();
                _db.InitializeSchema();
                LoadInstances();
                SetStatus("MAKEDOC.db connected — ready", success: true);
            }
            catch (Exception ex)
            {
                _db = null!;
                SetStatus($"Database error: {ex.Message}", success: false);
            }
        }

        // ── UI Construction ───────────────────────────
        private void BuildUI()
        {
            this.Text = "MAKEDOC";
            this.Size = new Size(880, 560);
            this.MinimumSize = new Size(640, 420);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f);

            // Outer layout: header | instance table | status
            // Add the Fill-docked layout BEFORE the menu — WinForms dock
            // processes controls in reverse-add order, so the last-added
            // control takes its slice first. We want the menu (Top) to claim
            // its space first, then the layout to fill what's left.
            _mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(24, 16, 24, 12)
            };
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));   // header
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // instance table
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));   // status
            this.Controls.Add(_mainLayout);

            BuildHeader();
            BuildInstanceTable();
            BuildStatusBar();

            // Menu added last so it takes the Top dock slice ahead of _mainLayout.
            BuildMenu();
        }

        private void BuildMenu()
        {
            _menu = new MenuStrip
            {
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9.5f)
            };

            var fileMenu = new ToolStripMenuItem("&File");
            var miExit = new ToolStripMenuItem("E&xit");
            miExit.Click += (s, e) => this.Close();
            fileMenu.DropDownItems.Add(miExit);

            var toolsMenu = new ToolStripMenuItem("&Tools");

			var miLineItem = new ToolStripMenuItem("&Line Items...");
			miLineItem.Click += (s, e) =>
			{
				using var form = new LineItemForm();
				form.ShowDialog(this);
				LoadInstances(); // refresh after possible new instance
			};


			var miAssembly = new ToolStripMenuItem("&Assembly...");
            miAssembly.Click += (s, e) =>
            {
                using var form = new AssemblyForm();
                form.ShowDialog(this);
                LoadInstances(); // refresh after possible new instance
            };

            var miAnalytics = new ToolStripMenuItem("A&nalytics...");
            miAnalytics.Click += (s, e) =>
            {
                using var form = new AnalyticsForm();
                form.ShowDialog(this);
            };

            toolsMenu.DropDownItems.Add(miLineItem);
			toolsMenu.DropDownItems.Add(miAssembly);
            toolsMenu.DropDownItems.Add(miAnalytics);
            toolsMenu.DropDownItems.Add(new ToolStripSeparator());

            var miAdmin = new ToolStripMenuItem("&Maintenance && Configuration...");
            miAdmin.Click += (s, e) =>
            {
                using var form = new AdminForm();
                form.ShowDialog(this);
                LoadInstances(); // doctype changes may affect display
            };
            toolsMenu.DropDownItems.Add(miAdmin);

            _menu.Items.Add(fileMenu);
            _menu.Items.Add(toolsMenu);

            this.MainMenuStrip = _menu;
            this.Controls.Add(_menu);
        }

        private void BuildHeader()
        {
            _headerPanel = new Panel { Dock = DockStyle.Fill };

            var lblTitle = new Label
            {
                Text = "MAKEDOC",
                Font = new Font("Segoe UI", 16f, FontStyle.Regular),
                ForeColor = Color.FromArgb(30, 30, 30),
                AutoSize = true,
                Location = new Point(0, 4)
            };

            var lblSub = new Label
            {
                Text = "Document instances — assembled documents and their state",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(110, 110, 110),
                AutoSize = true,
                Location = new Point(0, 36)
            };

            _headerPanel.Controls.Add(lblTitle);
            _headerPanel.Controls.Add(lblSub);
            _mainLayout.Controls.Add(_headerPanel, 0, 0);
        }

        private void BuildInstanceTable()
        {
            _lvInstances = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                MultiSelect = false,
                HideSelection = false,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            _lvInstances.Columns.Add("Generated",       155);
            _lvInstances.Columns.Add("Title",           200);
            _lvInstances.Columns.Add("Document type",   240);
            _lvInstances.Columns.Add("Tier",             80);
            _lvInstances.Columns.Add("Archived",         80);

            BuildInstanceContextMenu();
            _lvInstances.ContextMenuStrip = _ctxInstances;

            _mainLayout.Controls.Add(_lvInstances, 0, 1);
        }

        private void BuildStatusBar()
        {
            _lblStatus = new Label
            {
                Text = "Initializing...",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(130, 130, 130),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _mainLayout.Controls.Add(_lblStatus, 0, 2);
        }

        // ── Data Loading ──────────────────────────────
        private void LoadInstances()
        {
            if (_db == null) return;

            try
            {
                var instances = _db.GetAllInstancesWithDocType();
                RenderInstances(instances);
                SetStatus(
                    $"MAKEDOC.db connected — {instances.Count} instance(s) loaded",
                    success: true);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load instances: {ex.Message}", success: false);
            }
        }

        private void RenderInstances(List<Instance> instances)
        {
            _lvInstances.BeginUpdate();
            try
            {
                _lvInstances.Items.Clear();

                foreach (var inst in instances)
                {
                    var item = new ListViewItem(FormatGeneratedDate(inst.GeneratedDate))
                    {
                        Tag = inst
                    };
                    item.SubItems.Add(inst.Title ?? "");
                    item.SubItems.Add(inst.DocTypeName ?? inst.DocTypeID);
                    item.SubItems.Add(inst.DocTypeTier ?? "");
                    item.SubItems.Add(inst.IsArchived ? "Archived" : "");

                    if (inst.IsArchived)
                    {
                        // De-emphasize archived rows (per build-from.md UI section).
                        item.ForeColor = Color.FromArgb(160, 160, 160);
                        item.Font = new Font(_lvInstances.Font, FontStyle.Italic);
                    }

                    _lvInstances.Items.Add(item);
                }
            }
            finally
            {
                _lvInstances.EndUpdate();
            }
        }

        private static string FormatGeneratedDate(string raw)
        {
            // GeneratedDate is stored as a SQLite datetime string; show it as
            // a friendly local-time string when we can parse it.
            if (DateTime.TryParse(raw, out var dt))
                return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            return raw;
        }

        // ── Instance context menu ─────────────────────
        private void BuildInstanceContextMenu()
        {
            _ctxInstances = new ContextMenuStrip { Font = new Font("Segoe UI", 9.5f) };

            _miBuildFrom  = new ToolStripMenuItem("Build from selected document");
            _miBuildToSol = new ToolStripMenuItem("Build to solicitation...");
            _miBuildToAwd = new ToolStripMenuItem("Build to award...");
            var miView      = new ToolStripMenuItem("View document");
            var miSep       = new ToolStripSeparator();
            var miArchive   = new ToolStripMenuItem("Archive document");
            var miUnarchive = new ToolStripMenuItem("Un-archive document");

            _miBuildFrom.Click  += OnBuildFromSelected;
            _miBuildToSol.Click += OnBuildToSolicitation;
            _miBuildToAwd.Click += OnBuildToAward;
            miView.Click        += OnViewSelected;
            miArchive.Click     += OnArchiveSelected;
            miUnarchive.Click   += OnUnarchiveSelected;

            _ctxInstances.Items.Add(_miBuildFrom);
            _ctxInstances.Items.Add(_miBuildToSol);
            _ctxInstances.Items.Add(_miBuildToAwd);
            _ctxInstances.Items.Add(miView);
            _ctxInstances.Items.Add(miSep);
            _ctxInstances.Items.Add(miArchive);
            _ctxInstances.Items.Add(miUnarchive);

            // Show/hide items based on archive state and document type.
            _ctxInstances.Opening += (s, e) =>
            {
                var inst = GetSelectedInstance();
                if (inst is null) { e.Cancel = true; return; }

                bool active = !inst.IsArchived;
                bool isReq  = inst.DocTypeID.StartsWith("req-", StringComparison.OrdinalIgnoreCase);
                bool isSol  = inst.DocTypeID.StartsWith("sol-", StringComparison.OrdinalIgnoreCase);

                _miBuildFrom.Visible  = active;
                _miBuildToSol.Visible = active && isReq;
                _miBuildToAwd.Visible = active && isSol;
                miView.Visible        = active;
                miSep.Visible         = active;
                miArchive.Visible     = active;
                miUnarchive.Visible   = !active;
            };
        }

        private void OnArchiveSelected(object? sender, EventArgs e)
        {
            var inst = GetSelectedInstance();
            if (inst is null || inst.IsArchived) return;

            _db.SetArchiveStatus(inst.InstanceID, archived: true);
            LoadInstances();
            SetStatus($"Document archived.", success: true);
        }

        private void OnUnarchiveSelected(object? sender, EventArgs e)
        {
            var inst = GetSelectedInstance();
            if (inst is null || !inst.IsArchived) return;

            _db.SetArchiveStatus(inst.InstanceID, archived: false);
            LoadInstances();
            SetStatus($"Document un-archived.", success: true);
        }

        private Instance? GetSelectedInstance()
        {
            if (_lvInstances.SelectedItems.Count == 0) return null;
            return _lvInstances.SelectedItems[0].Tag as Instance;
        }

        private void OnBuildFromSelected(object? sender, EventArgs e)
        {
            var inst = GetSelectedInstance();
            if (inst is null) return;

            using var form = new AssemblyForm(inst);
            form.ShowDialog(this);
            LoadInstances();
        }

        private void OnBuildToSolicitation(object? sender, EventArgs e)
        {
            var inst = GetSelectedInstance();
            if (inst is null || inst.IsArchived) return;

            var solTypes = _db.GetAllDocTypes()
                .Where(dt => dt.DocTypeID.StartsWith("sol-", StringComparison.OrdinalIgnoreCase))
                .OrderBy(dt => dt.Name)
                .ToList();

            if (solTypes.Count == 0)
            {
                MessageBox.Show(
                    "No solicitation document types are defined in the database.",
                    "Build To Solicitation",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var target = ShowDocTypePicker("Select target solicitation type:", solTypes);
            if (target is null) return;

            using var form = new AssemblyForm(inst, target);
            form.ShowDialog(this);
            LoadInstances();
        }

        private void OnBuildToAward(object? sender, EventArgs e)
        {
            var inst = GetSelectedInstance();
            if (inst is null || inst.IsArchived) return;

            var awdTypes = _db.GetAllDocTypes()
                .Where(dt => dt.DocTypeID.StartsWith("awd-", StringComparison.OrdinalIgnoreCase))
                .OrderBy(dt => dt.Name)
                .ToList();

            if (awdTypes.Count == 0)
            {
                MessageBox.Show(
                    "No award document types are defined in the database.",
                    "Build To Award",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var target = ShowDocTypePicker("Select target award type:", awdTypes);
            if (target is null) return;

            using var form = new AssemblyForm(inst, target);
            form.ShowDialog(this);
            LoadInstances();
        }

        /// <summary>
        /// Shows a modal picker listing the supplied DocTypes and returns the
        /// one the user selected, or null if the user cancelled.
        /// </summary>
        private DocType? ShowDocTypePicker(string prompt, List<DocType> docTypes)
        {
            using var dlg = new Form
            {
                Text            = "Select Document Type",
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
                Text     = prompt,
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
            foreach (var dt in docTypes) lst.Items.Add(dt.Name);
            if (lst.Items.Count > 0) lst.SelectedIndex = 0;
            dlg.Controls.Add(lst);

            var btnOK = new Button
            {
                Text      = "Select",
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
                ? docTypes[lst.SelectedIndex]
                : null;
        }

        private void OnViewSelected(object? sender, EventArgs e)
        {
            var inst = GetSelectedInstance();
            if (inst is null) return;

            if (string.IsNullOrWhiteSpace(inst.NodeList))
            {
                MessageBox.Show(
                    "This document has no node list and cannot be re-assembled.",
                    "View Document", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                SetStatus("Re-assembling document for viewing...", success: true);

                var nodeIds = JsonSerializer.Deserialize<List<string>>(inst.NodeList);
                if (nodeIds is null || nodeIds.Count == 0)
                {
                    SetStatus("Node list is empty — nothing to assemble.", success: false);
                    return;
                }

                var nodeService     = new NodeService(_db);
                var assemblyService = new DocumentAssemblyService();
                var fillinService   = new FillinService();

                // Deserialize stored fill-in values (may be null/empty for old instances).
                IReadOnlyDictionary<string, string> fillinValues =
                    new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(inst.FillinData))
                {
                    try
                    {
                        fillinValues = JsonSerializer.Deserialize<Dictionary<string, string>>(
                            inst.FillinData)
                            ?? new Dictionary<string, string>();
                    }
                    catch { /* best effort — leave empty */ }
                }

                var blobs   = new List<byte[]>();
                var missing = new List<string>();
                int secNo   = 0;

                foreach (var id in nodeIds)
                {
                    var node = nodeService.GetById(id);
                    if (node?.Content is null || node.Content.Length == 0)
                    {
                        missing.Add(id);
                        continue;
                    }

                    byte[] blob = node.Content;

                    if (node.NodeType == NodeTypes.Clause)
                    {
                        secNo++;

                        // Apply stored SDT fill-ins.
                        if (fillinValues.Count > 0)
                            blob = fillinService.SubstituteFillins(blob, fillinValues);

                        // Apply {SecNo} plain-text fill-in.
                        blob = fillinService.SubstitutePlainText(
                            blob, "{SecNo}", secNo.ToString());
                    }

                    blobs.Add(blob);
                }

                if (missing.Count > 0)
                {
                    string preview = string.Join(", ", missing.Take(5));
                    if (missing.Count > 5) preview += "...";
                    SetStatus(
                        $"Warning: {missing.Count} node(s) had no content (skipped): {preview}",
                        success: false);
                }

                if (blobs.Count == 0)
                {
                    SetStatus("Cannot view — no content blobs available.", success: false);
                    return;
                }

                byte[] assembled = assemblyService.Assemble(blobs);

                string src = inst.InstanceID.Length >= 8 ? inst.InstanceID[..8] : inst.InstanceID;
                string tmpPath = Path.Combine(
                    Path.GetTempPath(),
                    $"MAKEDOC_view_{src}_{DateTime.Now:yyyyMMddHHmmss}.docx");

                File.WriteAllBytes(tmpPath, assembled);
                Process.Start(new ProcessStartInfo(tmpPath) { UseShellExecute = true });

                SetStatus($"Opened for viewing: {Path.GetFileName(tmpPath)}", success: true);
            }
            catch (Exception ex)
            {
                SetStatus($"Error viewing document: {ex.Message}", success: false);
            }
        }

        // ── Status Bar ────────────────────────────────
        private void SetStatus(string message, bool success)
        {
            _lblStatus.Text = $"●  {message}";
            _lblStatus.ForeColor = success
                ? Color.FromArgb(15, 110, 86)   // green
                : Color.FromArgb(180, 40, 40);   // red
        }
    }
}
