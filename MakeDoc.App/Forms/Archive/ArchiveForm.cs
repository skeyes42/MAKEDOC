using MakeDoc.Core.Data;
using MakeDoc.Core.Models;
using System.Text.Json;

namespace MakeDoc.App.Forms.Archive
{
	public partial class ArchiveForm : Form
	{
		// ── Services ──────────────────────────────────────────────────
		private readonly MakDocDb _db;

		// ── State ─────────────────────────────────────────────────────
		private List<Instance> _activeInstances   = new();
		private List<Instance> _archivedInstances = new();

		// ── Controls — Active tab ─────────────────────────────────────
		private ListView _lvActive   = null!;
		private Button   _btnArchive = null!;

		// ── Controls — Archived tab ───────────────────────────────────
		private ListView _lvArchived   = null!;
		private Button   _btnUnarchive = null!;

		// ── Shared ────────────────────────────────────────────────────
		private Label _lblStatus = null!;

		// ─────────────────────────────────────────────────────────────
		// Constructor
		// ─────────────────────────────────────────────────────────────
		public ArchiveForm()
		{
			InitializeComponent();
			BuildUI();

			try
			{
				_db = new MakDocDb();
				RefreshAll();
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
			Text          = "MAKEDOC — Archive";
			Size          = new Size(900, 620);
			MinimumSize   = new Size(720, 480);
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
			outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));   // status
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
				Text      = "Archive",
				Font      = new Font("Segoe UI", 14f, FontStyle.Regular),
				ForeColor = Color.FromArgb(30, 30, 30),
				AutoSize  = true,
				Location  = new Point(0, 2)
			});
			panel.Controls.Add(new Label
			{
				Text      = "Browse instances, archive completed documents, and un-archive as needed.",
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

			tabs.TabPages.Add(BuildActiveTab());
			tabs.TabPages.Add(BuildArchivedTab());

			parent.Controls.Add(tabs, 0, 1);
		}

		// ── Active tab ────────────────────────────────────────────────
		private TabPage BuildActiveTab()
		{
			var page = new TabPage("Active") { BackColor = Color.White };
			page.Padding = new Padding(8);

			var layout = new TableLayoutPanel
			{
				Dock        = DockStyle.Fill,
				ColumnCount = 1,
				RowCount    = 2,
				Padding     = new Padding(0, 4, 0, 0)
			};
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // list
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));  // button bar
			page.Controls.Add(layout);

			_lvActive = MakeListView();
			layout.Controls.Add(_lvActive, 0, 0);

			var bar = new Panel { Dock = DockStyle.Fill };

			_btnArchive = MakeButton("Archive Selected", Color.FromArgb(30, 30, 30));
			_btnArchive.Location = new Point(0, 6);
			_btnArchive.Enabled  = false;
			_btnArchive.Click   += OnArchiveClicked;
			bar.Controls.Add(_btnArchive);

			var btnRefresh = MakeButton("Refresh", Color.FromArgb(80, 80, 80));
			btnRefresh.Location = new Point(200, 6);
			btnRefresh.Click   += (_, _) => RefreshAll();
			bar.Controls.Add(btnRefresh);

			layout.Controls.Add(bar, 0, 1);

			_lvActive.SelectedIndexChanged += (_, _) =>
				_btnArchive.Enabled = _lvActive.SelectedItems.Count > 0;

			return page;
		}

		// ── Archived tab ──────────────────────────────────────────────
		private TabPage BuildArchivedTab()
		{
			var page = new TabPage("Archived") { BackColor = Color.White };
			page.Padding = new Padding(8);

			var layout = new TableLayoutPanel
			{
				Dock        = DockStyle.Fill,
				ColumnCount = 1,
				RowCount    = 2,
				Padding     = new Padding(0, 4, 0, 0)
			};
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // list
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));  // button bar
			page.Controls.Add(layout);

			_lvArchived = MakeListView(includeArchiveDate: true);
			layout.Controls.Add(_lvArchived, 0, 0);

			var bar = new Panel { Dock = DockStyle.Fill };

			_btnUnarchive = MakeButton("Un-archive Selected", Color.FromArgb(15, 110, 86));
			_btnUnarchive.Width    = 160;
			_btnUnarchive.Location = new Point(0, 6);
			_btnUnarchive.Enabled  = false;
			_btnUnarchive.Click   += OnUnarchiveClicked;
			bar.Controls.Add(_btnUnarchive);

			var btnRefresh = MakeButton("Refresh", Color.FromArgb(80, 80, 80));
			btnRefresh.Location = new Point(170, 6);
			btnRefresh.Click   += (_, _) => RefreshAll();
			bar.Controls.Add(btnRefresh);

			layout.Controls.Add(bar, 0, 1);

			_lvArchived.SelectedIndexChanged += (_, _) =>
				_btnUnarchive.Enabled = _lvArchived.SelectedItems.Count > 0;

			return page;
		}

		private static ListView MakeListView(bool includeArchiveDate = false)
		{
			var lv = new ListView
			{
				Dock          = DockStyle.Fill,
				View          = View.Details,
				FullRowSelect = true,
				GridLines     = true,
				BorderStyle   = BorderStyle.FixedSingle,
				Font          = new Font("Segoe UI", 9f)
			};

			lv.Columns.Add("Instance",       100);
			lv.Columns.Add("Document Type",  200);
			lv.Columns.Add("Generated",      130);
			lv.Columns.Add("Nodes",           55);

			if (includeArchiveDate)
				lv.Columns.Add("Archived", 130);

			return lv;
		}

		private static Button MakeButton(string text, Color back)
		{
			var btn = new Button
			{
				Text      = text,
				Width     = 160,
				Height    = 32,
				FlatStyle = FlatStyle.Flat,
				BackColor = back,
				ForeColor = Color.White,
				Font      = new Font("Segoe UI", 9f),
				Cursor    = Cursors.Hand
			};
			btn.FlatAppearance.BorderSize = 0;
			return btn;
		}

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

		// ─────────────────────────────────────────────────────────────
		// Data Loading
		// ─────────────────────────────────────────────────────────────
		private void RefreshAll()
		{
			if (_db == null) return;

			_activeInstances   = _db.GetInstancesByArchiveStatus(archived: false);
			_archivedInstances = _db.GetInstancesByArchiveStatus(archived: true);

			PopulateListView(_lvActive,   _activeInstances,   includeArchiveDate: false);
			PopulateListView(_lvArchived, _archivedInstances, includeArchiveDate: true);

			SetStatus(
				$"Active: {_activeInstances.Count}    Archived: {_archivedInstances.Count}",
				success: true);
		}

		private static void PopulateListView(
			ListView lv, List<Instance> instances, bool includeArchiveDate)
		{
			lv.BeginUpdate();
			lv.Items.Clear();

			foreach (var inst in instances)
			{
				// Show first 8 chars of the GUID — enough to identify it
				string shortId = inst.InstanceID.Length >= 8
					? inst.InstanceID[..8] + "…"
					: inst.InstanceID;

				// Count nodes from the JSON array in NodeList
				int nodeCount = 0;
				if (!string.IsNullOrWhiteSpace(inst.NodeList))
				{
					try
					{
						var ids = JsonSerializer.Deserialize<List<string>>(inst.NodeList);
						nodeCount = ids?.Count ?? 0;
					}
					catch { /* malformed JSON — leave count at 0 */ }
				}

				// Format the ISO date string for display
				string generated = FormatDate(inst.GeneratedDate);

				var item = new ListViewItem(shortId);
				item.SubItems.Add(inst.DocTypeName ?? inst.DocTypeID);
				item.SubItems.Add(generated);
				item.SubItems.Add(nodeCount.ToString());

				if (includeArchiveDate)
					item.SubItems.Add(FormatDate(inst.ArchiveDate));

				// Store the full InstanceID in the Tag for use in event handlers
				item.Tag = inst.InstanceID;

				lv.Items.Add(item);
			}

			lv.EndUpdate();
		}

		// ─────────────────────────────────────────────────────────────
		// Event Handlers
		// ─────────────────────────────────────────────────────────────
		private void OnArchiveClicked(object? sender, EventArgs e)
		{
			if (_lvActive.SelectedItems.Count == 0) return;

			var item       = _lvActive.SelectedItems[0];
			var instanceId = item.Tag as string ?? "";
			var docType    = item.SubItems[1].Text;

			var confirm = MessageBox.Show(
				$"Archive instance for \"{docType}\"?\n\n" +
				$"It will no longer appear in active lists or be available as a build-from source.",
				"Confirm Archive",
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Question);

			if (confirm != DialogResult.Yes) return;

			try
			{
				_db.SetArchiveStatus(instanceId, archived: true);
				RefreshAll();
				SetStatus($"Instance archived.", success: true);
			}
			catch (Exception ex)
			{
				SetStatus($"Archive failed: {ex.Message}", success: false);
			}
		}

		private void OnUnarchiveClicked(object? sender, EventArgs e)
		{
			if (_lvArchived.SelectedItems.Count == 0) return;

			var item       = _lvArchived.SelectedItems[0];
			var instanceId = item.Tag as string ?? "";
			var docType    = item.SubItems[1].Text;

			var confirm = MessageBox.Show(
				$"Un-archive instance for \"{docType}\"?\n\n" +
				$"It will return to the active list and be available as a build-from source.",
				"Confirm Un-archive",
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Question);

			if (confirm != DialogResult.Yes) return;

			try
			{
				_db.SetArchiveStatus(instanceId, archived: false);
				RefreshAll();
				SetStatus($"Instance un-archived.", success: true);
			}
			catch (Exception ex)
			{
				SetStatus($"Un-archive failed: {ex.Message}", success: false);
			}
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

		private static string FormatDate(string? isoDate)
		{
			if (string.IsNullOrWhiteSpace(isoDate)) return "—";
			return DateTime.TryParse(isoDate, out var dt)
				? dt.ToString("yyyy-MM-dd  HH:mm")
				: isoDate;
		}
	}
}
