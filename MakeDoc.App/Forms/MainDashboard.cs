using MakeDoc.App.Forms.Admin;
using MakeDoc.App.Forms.Analytics;
using MakeDoc.App.Forms.Archive;
using MakeDoc.App.Forms.Assembly;
using MakeDoc.Core.Data;
using MakeDoc.Core.Models;

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
        private MenuStrip _menu = null!;
        private TableLayoutPanel _mainLayout = null!;
        private Panel _headerPanel = null!;
        private ListView _lvInstances = null!;
        private Label _lblStatus = null!;

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

            var miAssembly = new ToolStripMenuItem("&Assembly...");
            miAssembly.Click += (s, e) =>
            {
                using var form = new AssemblyForm();
                form.ShowDialog(this);
                LoadInstances(); // refresh after possible new instance
            };

            var miArchive = new ToolStripMenuItem("Ar&chive...");
            miArchive.Click += (s, e) =>
            {
                using var form = new ArchiveForm();
                form.ShowDialog(this);
                LoadInstances(); // refresh in case archive status changed
            };

            var miAnalytics = new ToolStripMenuItem("A&nalytics...");
            miAnalytics.Click += (s, e) =>
            {
                using var form = new AnalyticsForm();
                form.ShowDialog(this);
            };

            toolsMenu.DropDownItems.Add(miAssembly);
            toolsMenu.DropDownItems.Add(miArchive);
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

            _lvInstances.Columns.Add("Generated",       170);
            _lvInstances.Columns.Add("Document type",   320);
            _lvInstances.Columns.Add("Tier",            100);
            _lvInstances.Columns.Add("Archived",         90);

            // Block selection of archived rows — they're context-only in this slice.
            _lvInstances.ItemSelectionChanged += (s, e) =>
            {
                if (e.IsSelected && e.Item?.Tag is Instance inst && inst.IsArchived)
                {
                    e.Item.Selected = false;
                }
            };

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
