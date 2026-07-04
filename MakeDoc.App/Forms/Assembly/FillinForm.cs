namespace MakeDoc.App.Forms.Assembly
{
	/// <summary>
	/// Modal dialog that collects fill-in values from the user at assembly time.
	///
	/// Always displays a "Document Title" field first, then one labeled TextBox
	/// per variable name found by FillinService. When the user clicks Generate
	/// Document, the values are exposed via DocumentTitle and Values.
	/// An optional preFill dictionary seeds the variable inputs (used by the
	/// build-from flow to carry forward prior fill-in data).
	/// </summary>
	public class FillinForm : Form
	{
		// ── State ──────────────────────────────────────────────────────
		private readonly IReadOnlyList<string>           _variableNames;
		private readonly Dictionary<string, TextBox>     _inputs = new();
		private TextBox                                  _titleInput = null!;

		// ── Result ─────────────────────────────────────────────────────
		/// <summary>
		/// The document title supplied by the user.
		/// Populated only after DialogResult.OK; empty string otherwise.
		/// </summary>
		public string DocumentTitle { get; private set; } = string.Empty;

		/// <summary>
		/// The variable-name → value map the user supplied.
		/// Populated only after DialogResult.OK; empty otherwise.
		/// </summary>
		public IReadOnlyDictionary<string, string> Values { get; private set; } =
			new Dictionary<string, string>();

		// ── Constructor ────────────────────────────────────────────────
		public FillinForm(
			IReadOnlyList<string>                        variableNames,
			IReadOnlyDictionary<string, string>?         preFill  = null,
			string?                                      preTitle = null)
		{
			_variableNames = variableNames;
			InitializeComponent();
			BuildUI(preFill, preTitle);
		}

		// ── Minimal InitializeComponent ────────────────────────────────
		private void InitializeComponent()
		{
			this.Text            = "Fill-in Values";
			this.Font            = new Font("Segoe UI", 9.5f);
			this.BackColor       = Color.White;
			this.MinimumSize     = new Size(500, 280);
			this.StartPosition   = FormStartPosition.CenterParent;
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.MaximizeBox     = false;
			this.MinimizeBox     = false;
		}

		// ── UI construction ────────────────────────────────────────────
		private void BuildUI(IReadOnlyDictionary<string, string>? preFill, string? preTitle = null)
		{
			// Outer layout: header | scrollable inputs | button bar
			var outer = new TableLayoutPanel
			{
				Dock        = DockStyle.Fill,
				ColumnCount = 1,
				RowCount    = 3,
				Padding     = new Padding(20)
			};
			outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));   // header
			outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // inputs
			outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));   // buttons

			// ── Header ────────────────────────────────────────────────
			var headerPanel = new Panel { Dock = DockStyle.Fill };

			headerPanel.Controls.Add(new Label
			{
				Text      = "Fill-in Values",
				Font      = new Font("Segoe UI", 12f),
				ForeColor = Color.FromArgb(30, 30, 30),
				AutoSize  = true,
				Location  = new Point(0, 0)
			});
			headerPanel.Controls.Add(new Label
			{
				Text      = "Enter a value for each variable found in the clause list.",
				Font      = new Font("Segoe UI", 8.5f),
				ForeColor = Color.FromArgb(110, 110, 110),
				AutoSize  = true,
				Location  = new Point(0, 28)
			});

			outer.Controls.Add(headerPanel, 0, 0);

			// ── Scrollable input area ──────────────────────────────────
			var scroll = new Panel
			{
				Dock        = DockStyle.Fill,
				AutoScroll  = true,
				BorderStyle = BorderStyle.FixedSingle,
				Padding     = new Padding(4)
			};

			const int rowHeight = 54;
			int y = 8;

			// ── Document Title (always shown) ──────────────────────────
			scroll.Controls.Add(new Label
			{
				Text      = "Document Title",
				Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
				ForeColor = Color.FromArgb(60, 60, 60),
				AutoSize  = true,
				Location  = new Point(8, y)
			});

			_titleInput = new TextBox
			{
				Font     = new Font("Segoe UI", 9.5f),
				Location = new Point(8, y + 20),
				Width    = 420,
				Text     = preTitle ?? string.Empty
			};
			scroll.Controls.Add(_titleInput);
			y += rowHeight;

			// ── Variable fill-ins ──────────────────────────────────────
			foreach (var name in _variableNames)
			{
				scroll.Controls.Add(new Label
				{
					Text      = name,
					Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
					ForeColor = Color.FromArgb(60, 60, 60),
					AutoSize  = true,
					Location  = new Point(8, y)
				});

				var txt = new TextBox
				{
					Font     = new Font("Segoe UI", 9.5f),
					Location = new Point(8, y + 20),
					Width    = 420,
					Text     = preFill is not null && preFill.TryGetValue(name, out var v) ? v : string.Empty
				};

				_inputs[name] = txt;
				scroll.Controls.Add(txt);
				y += rowHeight;
			}

			outer.Controls.Add(scroll, 0, 1);

			// Fit the form height to the content (capped)
			int inputHeight = Math.Min(y + 12, 380);
			this.ClientSize = new Size(480, 52 + inputHeight + 52 + 40);

			// ── Button bar ────────────────────────────────────────────
			var bar = new FlowLayoutPanel
			{
				Dock          = DockStyle.Fill,
				FlowDirection = FlowDirection.RightToLeft,
				WrapContents  = false,
				Padding       = new Padding(0, 10, 0, 0)
			};

			var btnOK = new Button
			{
				Text      = "Generate Document",
				Size      = new Size(168, 34),
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(30, 30, 30),
				ForeColor = Color.White,
				Font      = new Font("Segoe UI", 9.5f),
				Cursor    = Cursors.Hand
			};
			btnOK.FlatAppearance.BorderSize = 0;
			btnOK.Click += (_, _) =>
			{
				DocumentTitle = _titleInput.Text.Trim();
				Values = _inputs.ToDictionary(
					kv => kv.Key,
					kv => kv.Value.Text.Trim());
				DialogResult = DialogResult.OK;
				Close();
			};

			var btnCancel = new Button
			{
				Text      = "Cancel",
				Size      = new Size(80, 34),
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.White,
				ForeColor = Color.FromArgb(30, 30, 30),
				Font      = new Font("Segoe UI", 9.5f),
				Cursor    = Cursors.Hand
			};
			btnCancel.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
			btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

			bar.Controls.Add(btnOK);
			bar.Controls.Add(btnCancel);
			outer.Controls.Add(bar, 0, 2);

			this.Controls.Add(outer);
			this.AcceptButton = btnOK;
			this.CancelButton = btnCancel;
		}
	}
}
