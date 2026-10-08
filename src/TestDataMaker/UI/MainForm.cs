using System.Diagnostics;
using System.Globalization;
using TestDataMaker.Models;
using TestDataMaker.Services;

namespace TestDataMaker.UI;

internal sealed class MainForm : Form
{
    public const string AppTitle = "TestDataMaker Ver.1.0";

    private const int ColName = 0;
    private const int ColType = 1;
    private const int ColRule = 2;
    private const int ColNull = 3;
    private const int ColUnique = 4;

    private readonly List<FieldDefinition> _fields = new();
    private readonly CsvService _csv = new();
    private readonly ExcelService _excel = new();
    private readonly ConfigService _config = new();
    private readonly ValidationService _validation = new();
    private readonly DataGenerationService _generation = new();

    private readonly MenuStrip _menu = new();
    private readonly DataGridView _grid = new();
    private readonly FieldDetailPanel _detail = new() { Dock = DockStyle.Fill };
    private readonly TextBox _count = new() { Width = 110, TextAlign = HorizontalAlignment.Right, Text = "1,000" };
    private readonly CheckBox _bom = new() { Text = "CSVをBOM付きUTF-8で出力（Excelで文字化けしない）", Checked = true, AutoSize = true };
    private readonly Panel _progressPanel = new() { Dock = DockStyle.Fill, Visible = false, Height = 36 };
    private readonly ProgressBar _progress = new() { Width = 360, Height = 20 };
    private readonly Label _progressLabel = new() { AutoSize = true };
    private readonly Button _cancel = new() { Text = "キャンセル", AutoSize = true };
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly List<Control> _lockWhileBusy = new();
    private readonly List<ToolStripItem> _menuLockWhileBusy = new();

    private CancellationTokenSource? _cts;
    private bool _busy;
    private bool _closeAfterCancel;
    private bool _dirty;
    private bool _loadingGrid;
    private string? _configPath;
    private string _lastFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public MainForm()
    {
        Text = AppTitle;
        Size = new Size(1200, 760);
        MinimumSize = new Size(940, 600);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;
        KeyPreview = true;
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch (Exception)
        {
            // アイコンが取れなくても動作に影響しない
        }

        BuildMenu();
        BuildLayout();
        SetupGrid();
        SetupDragDrop(this);

        _detail.FieldChanged += (_, _) =>
        {
            if (_grid.CurrentRow is { } row) RefreshRow(row.Index);
            MarkDirty();
        };
        _count.Leave += (_, _) =>
        {
            if (ValidationService.TryParseRecordCount(_count.Text, out int n, out _)) _count.Text = n.ToString("N0");
        };
        _count.TextChanged += (_, _) => MarkDirty();
        _cancel.Click += (_, _) => CancelGeneration();
        FormClosing += OnFormClosing;

        LoadDefinition(DefaultDefinition(), null);
        SetStatus("Ready");
    }

    // ---- 画面構築 ----

    private void BuildMenu()
    {
        var file = new ToolStripMenuItem("ファイル(&F)");
        file.DropDownItems.Add(MenuItem("新規作成(&N)", Keys.Control | Keys.N, (_, _) => NewDefinition()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(MenuItem("CSVを読み込む(&I)...", Keys.Control | Keys.I, (_, _) => OpenCsv()));
        file.DropDownItems.Add(MenuItem("設定を読み込む(&O)...", Keys.Control | Keys.O, (_, _) => OpenConfig()));
        file.DropDownItems.Add(MenuItem("設定を保存(&S)", Keys.Control | Keys.S, (_, _) => SaveConfig(false)));
        file.DropDownItems.Add(MenuItem("名前を付けて設定を保存(&A)...", Keys.None, (_, _) => SaveConfig(true)));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(MenuItem("終了(&X)", Keys.Alt | Keys.F4, (_, _) => Close(), lockWhileBusy: false));

        var run = new ToolStripMenuItem("生成(&G)");
        run.DropDownItems.Add(MenuItem("プレビュー(&P)", Keys.F5, (_, _) => ShowPreview()));
        run.DropDownItems.Add(MenuItem("CSV生成(&C)...", Keys.Control | Keys.Shift | Keys.C, async (_, _) => await ExportAsync(_csv)));
        run.DropDownItems.Add(MenuItem("Excel生成(&E)...", Keys.Control | Keys.Shift | Keys.E, async (_, _) => await ExportAsync(_excel)));

        var help = new ToolStripMenuItem("ヘルプ(&H)");
        help.DropDownItems.Add(MenuItem("使い方(&U)", Keys.F1, (_, _) => ShowUsage(), lockWhileBusy: false));
        help.DropDownItems.Add(MenuItem("ログフォルダを開く(&L)", Keys.None, (_, _) => OpenLogFolder(), lockWhileBusy: false));
        help.DropDownItems.Add(new ToolStripSeparator());
        help.DropDownItems.Add(MenuItem("バージョン情報(&A)", Keys.None, (_, _) => ShowAbout(), lockWhileBusy: false));

        _menu.Items.AddRange(new ToolStripItem[] { file, run, help });
        MainMenuStrip = _menu;
    }

    private ToolStripMenuItem MenuItem(string text, Keys keys, EventHandler onClick, bool lockWhileBusy = true)
    {
        var item = new ToolStripMenuItem(text, null, onClick);
        if (keys != Keys.None) item.ShortcutKeys = keys;
        if (keys == (Keys.Alt | Keys.F4)) item.ShortcutKeyDisplayString = "Alt+F4";
        if (lockWhileBusy) _menuLockWhileBusy.Add(item);
        return item;
    }

    private Button Button(string text, EventHandler onClick, bool lockWhileBusy = true)
    {
        var b = new Button { Text = text, AutoSize = true, Padding = new Padding(6, 2, 6, 2), Margin = new Padding(0, 0, 6, 0) };
        b.Click += onClick;
        if (lockWhileBusy) _lockWhileBusy.Add(b);
        return b;
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12, 8, 12, 4) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 見出し
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // ボタン
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 定義一覧
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // ヒント
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 生成
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 進捗

        root.Controls.Add(new Label
        {
            Text = "データ定義",
            AutoSize = true,
            Font = new Font("Yu Gothic UI", 12f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6),
        });

        // ファイル操作・項目操作ボタン
        var tools = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0, 0, 0, 6) };
        tools.Controls.Add(Button("CSVを読み込む", (_, _) => OpenCsv()));
        tools.Controls.Add(Button("設定を読み込む", (_, _) => OpenConfig()));
        tools.Controls.Add(Button("設定を保存", (_, _) => SaveConfig(false)));
        tools.Controls.Add(new Label { Width = 24 });
        tools.Controls.Add(Button("項目を追加", (_, _) => AddField()));
        tools.Controls.Add(Button("項目を削除", (_, _) => RemoveField()));
        tools.Controls.Add(Button("▲ 上へ", (_, _) => MoveField(-1)));
        tools.Controls.Add(Button("▼ 下へ", (_, _) => MoveField(1)));
        root.Controls.Add(tools);

        // 項目一覧（左）と生成ルール詳細（右）
        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2, BorderStyle = BorderStyle.FixedSingle };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(_detail);
        _lockWhileBusy.Add(split);
        root.Controls.Add(split);
        Load += (_, _) =>
        {
            split.Panel2MinSize = 320;
            split.SplitterDistance = Math.Max(300, split.Width - 420);
        };

        root.Controls.Add(new Label
        {
            Text = "ヒント：CSVファイルをこの画面にドラッグ＆ドロップすると、列名を読み込んで項目を自動作成します。",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 4, 0, 8),
        });

        // 生成件数・生成ボタン
        var gen = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        gen.Controls.Add(new Label { Text = "生成件数：", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 4, 0) });
        _count.Margin = new Padding(0, 3, 4, 0);
        gen.Controls.Add(_count);
        _lockWhileBusy.Add(_count);
        gen.Controls.Add(new Label { Text = "件（1～1,000,000）", AutoSize = true, Margin = new Padding(0, 7, 24, 0) });
        var preview = Button("プレビュー", (_, _) => ShowPreview());
        var csv = Button("CSV生成", async (_, _) => await ExportAsync(_csv));
        var excel = Button("Excel生成", async (_, _) => await ExportAsync(_excel));
        foreach (var b in new[] { preview, csv, excel })
        {
            b.MinimumSize = new Size(110, 34);
            b.Margin = new Padding(0, 0, 8, 0);
        }
        csv.Font = excel.Font = new Font(Font, FontStyle.Bold);
        gen.Controls.Add(preview);
        gen.Controls.Add(new Label { Width = 16 });
        gen.Controls.Add(csv);
        gen.Controls.Add(excel);
        _bom.Margin = new Padding(16, 8, 0, 0);
        gen.Controls.Add(_bom);
        _lockWhileBusy.Add(_bom);
        root.Controls.Add(gen);

        // 進捗
        var progressFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
        progressFlow.Controls.Add(new Label { Text = "生成中...", AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
        _progress.Margin = new Padding(0, 4, 8, 0);
        progressFlow.Controls.Add(_progress);
        _progressLabel.Margin = new Padding(0, 6, 16, 0);
        progressFlow.Controls.Add(_progressLabel);
        progressFlow.Controls.Add(_cancel);
        _progressPanel.Controls.Add(progressFlow);
        _progressPanel.AutoSize = true;
        root.Controls.Add(_progressPanel);

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(new ToolStripStatusLabel("ステータス："));
        statusStrip.Items.Add(_status);

        Controls.Add(root);
        Controls.Add(statusStrip);
        Controls.Add(_menu);
    }

    private void SetupGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BorderStyle = BorderStyle.None;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        _grid.RowHeadersWidth = 48;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _grid.RowTemplate.Height = 28;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "項目名", Width = 170, MaxInputLength = ValidationService.MaxFieldNameLength, SortMode = DataGridViewColumnSortMode.NotSortable });
        var type = new DataGridViewComboBoxColumn
        {
            HeaderText = "データ型",
            Width = 130,
            DisplayMember = nameof(ComboItem<DataType>.Text),
            ValueMember = nameof(ComboItem<DataType>.Value),
            DataSource = Enum.GetValues<DataType>().Select(t => new ComboItem<DataType>(t, DisplayNames.Of(t))).ToList(),
            FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
        };
        _grid.Columns.Add(type);
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "生成ルール（右側で設定）",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 150,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = { ForeColor = SystemColors.GrayText },
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "NULL率",
            Width = 80,
            ValueType = typeof(double),
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = { Format = @"0.##\%", Alignment = DataGridViewContentAlignment.MiddleRight },
        });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "重複禁止", Width = 84, SortMode = DataGridViewColumnSortMode.NotSortable });

        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            // コンボボックス・チェックボックスは選択した時点で反映する
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell?.ColumnIndex is ColType or ColUnique)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += OnCellValueChanged;
        _grid.CellParsing += (_, e) =>
        {
            if (e.ColumnIndex != ColNull) return;
            if (TryParseNullRate(e.Value?.ToString(), out double rate))
            {
                e.Value = rate;
                e.ParsingApplied = true;
            }
        };
        _grid.CellValidating += (_, e) =>
        {
            if (e.ColumnIndex != ColNull || !_grid.IsCurrentCellInEditMode) return;
            if (!TryParseNullRate(e.FormattedValue?.ToString(), out double _))
            {
                e.Cancel = true;
                ShowWarning("NULL率は0～100の数値で入力してください。（例：5 または 5%）");
            }
        };
        _grid.DataError += (_, e) =>
        {
            e.ThrowException = false;
            e.Cancel = true;
        };
        _grid.SelectionChanged += (_, _) =>
        {
            if (_loadingGrid) return;
            var f = CurrentField();
            if (!ReferenceEquals(_detail.Field, f)) _detail.Field = f;
        };
        _grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete && !_grid.IsCurrentCellInEditMode && _grid.CurrentCell?.ColumnIndex == ColName)
            {
                RemoveField();
                e.Handled = true;
            }
        };
    }

    private static bool TryParseNullRate(string? text, out double rate)
    {
        string s = (text ?? "").Trim().TrimEnd('%', '％').Trim();
        if (s.Length == 0)
        {
            rate = 0;
            return true;
        }
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out rate) && rate is >= 0 and <= 100;
    }

    // ---- 項目一覧 ----

    private FieldDefinition? CurrentField()
    {
        int i = _grid.CurrentCell?.RowIndex ?? -1;
        return i >= 0 && i < _fields.Count ? _fields[i] : null;
    }

    private void LoadGrid(int selectIndex = 0)
    {
        _loadingGrid = true;
        _grid.Rows.Clear();
        foreach (var f in _fields)
        {
            int i = _grid.Rows.Add();
            WriteRow(i, f);
        }
        _loadingGrid = false;

        if (_fields.Count > 0)
        {
            int index = Math.Clamp(selectIndex, 0, _fields.Count - 1);
            _grid.CurrentCell = _grid.Rows[index].Cells[ColName];
        }
        _detail.Field = CurrentField();
    }

    private void WriteRow(int i, FieldDefinition f)
    {
        bool loading = _loadingGrid;
        _loadingGrid = true;
        var row = _grid.Rows[i];
        row.HeaderCell.Value = (i + 1).ToString();
        row.Cells[ColName].Value = f.Name;
        row.Cells[ColType].Value = f.DataType;
        row.Cells[ColRule].Value = f.RuleSummary;
        row.Cells[ColNull].Value = f.NullRate;
        row.Cells[ColUnique].Value = f.Unique;
        _loadingGrid = loading;
    }

    private void RefreshRow(int i)
    {
        if (i < 0 || i >= _fields.Count) return;
        WriteRow(i, _fields[i]);
    }

    private void OnCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_loadingGrid || e.RowIndex < 0 || e.RowIndex >= _fields.Count) return;
        var f = _fields[e.RowIndex];
        var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];

        switch (e.ColumnIndex)
        {
            case ColName:
                f.Name = (cell.Value as string ?? "").Trim();
                break;
            case ColType:
                if (cell.Value is DataType t && t != f.DataType)
                {
                    var defaults = FieldDefinition.Create(f.Name, t);
                    f.DataType = t;
                    if (t == DataType.Sequence) f.Unique = true;
                    if (t == DataType.Date && f.StartDate.Date == f.EndDate.Date)
                    {
                        f.StartDate = defaults.StartDate;
                        f.EndDate = defaults.EndDate;
                    }
                }
                break;
            case ColNull:
                f.NullRate = cell.Value is double d ? d : 0;
                break;
            case ColUnique:
                f.Unique = cell.Value is true;
                break;
        }
        RefreshRow(e.RowIndex);
        if (e.ColumnIndex is ColName or ColType) _detail.Field = f;
        MarkDirty();
    }

    private void AddField()
    {
        CommitGridEdit();
        int n = _fields.Count + 1;
        string name;
        do name = $"COLUMN{n++}"; while (_fields.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)));

        int insertAt = _grid.CurrentCell is null ? _fields.Count : _grid.CurrentCell.RowIndex + 1;
        _fields.Insert(insertAt, FieldDefinition.Create(name, DataType.String));
        LoadGrid(insertAt);
        _grid.CurrentCell = _grid.Rows[insertAt].Cells[ColName];
        _grid.BeginEdit(true);
        MarkDirty();
    }

    private void RemoveField()
    {
        CommitGridEdit();
        var f = CurrentField();
        if (f is null) return;
        if (MessageBox.Show(this, $"項目「{f.Name}」を削除しますか？", AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;
        int i = _fields.IndexOf(f);
        _fields.RemoveAt(i);
        LoadGrid(i);
        MarkDirty();
    }

    private void MoveField(int delta)
    {
        CommitGridEdit();
        var f = CurrentField();
        if (f is null) return;
        int i = _fields.IndexOf(f);
        int j = i + delta;
        if (j < 0 || j >= _fields.Count) return;
        (_fields[i], _fields[j]) = (_fields[j], _fields[i]);
        int col = _grid.CurrentCell?.ColumnIndex ?? ColName;
        LoadGrid(j);
        _grid.CurrentCell = _grid.Rows[j].Cells[col];
        MarkDirty();
    }

    private void CommitGridEdit()
    {
        if (_grid.IsCurrentCellInEditMode) _grid.EndEdit();
    }

    // ---- 定義の読み込み・保存 ----

    private static TestDataDefinition DefaultDefinition()
    {
        var id = FieldDefinition.Create("ID", DataType.Sequence);
        var name = FieldDefinition.Create("NAME", DataType.JapaneseName);
        name.NullRate = 5;
        var age = FieldDefinition.Create("AGE", DataType.Number);
        age.MinValue = 18;
        age.MaxValue = 80;
        var birthday = FieldDefinition.Create("BIRTHDAY", DataType.Date);
        birthday.NullRate = 2;
        var address = FieldDefinition.Create("ADDRESS", DataType.Address);
        address.NullRate = 10;
        var status = FieldDefinition.Create("STATUS", DataType.Fixed);
        status.FixedValue = "ACTIVE";
        return new TestDataDefinition { RecordCount = 1000, Fields = { id, name, age, birthday, address, status } };
    }

    private void LoadDefinition(TestDataDefinition def, string? configPath)
    {
        _fields.Clear();
        _fields.AddRange(def.Fields);
        LoadGrid();
        _count.Text = Math.Clamp(def.RecordCount, ValidationService.MinRecordCount, ValidationService.MaxRecordCount).ToString("N0");
        _configPath = configPath;
        _dirty = false;
        UpdateTitle();
    }

    private TestDataDefinition CurrentDefinition()
    {
        ValidationService.TryParseRecordCount(_count.Text, out int count, out _);
        return new TestDataDefinition
        {
            RecordCount = count > 0 ? count : 1000,
            Fields = _fields.Select(f => f.Clone()).ToList(),
        };
    }

    private void NewDefinition()
    {
        if (!ConfirmDiscardChanges()) return;
        LoadDefinition(new TestDataDefinition { Fields = { FieldDefinition.Create("COLUMN1", DataType.String) } }, null);
        SetStatus("新しい定義を作成しました。");
    }

    private void OpenCsv()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "列名を読み込むCSVファイルを選択",
            Filter = "CSV ファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*",
            InitialDirectory = _lastFolder,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        ImportCsv(dlg.FileName);
    }

    private void ImportCsv(string path)
    {
        if (!ConfirmDiscardChanges()) return;
        try
        {
            var result = _csv.ReadHeader(path);
            var fields = FieldTypeInferrer.Infer(result);
            _lastFolder = Path.GetDirectoryName(path) ?? _lastFolder;
            LoadDefinition(new TestDataDefinition { RecordCount = CurrentDefinition().RecordCount, Fields = fields }, null);
            _dirty = true;
            UpdateTitle();
            string enc = result.Encoding.CodePage == 932 ? "Shift-JIS" : "UTF-8";
            SetStatus($"CSVを読み込みました：{Path.GetFileName(path)}（{fields.Count}列、文字コード：{enc}）。データ型は列名から推測しています。必要に応じて変更してください。");
        }
        catch (TestDataException ex)
        {
            AppLogger.Error("CSV読み込みエラー", ex);
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            AppLogger.Error("CSV読み込みエラー", ex);
            ShowError(CsvService.ReadErrorMessage);
        }
    }

    private void OpenConfig()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "設定ファイルを開く",
            Filter = ConfigService.FileFilter + "|すべてのファイル (*.*)|*.*",
            InitialDirectory = _lastFolder,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        ImportConfig(dlg.FileName);
    }

    private void ImportConfig(string path)
    {
        if (!ConfirmDiscardChanges()) return;
        try
        {
            var def = _config.Load(path);
            _lastFolder = Path.GetDirectoryName(path) ?? _lastFolder;
            LoadDefinition(def, path);
            SetStatus($"設定を読み込みました：{Path.GetFileName(path)}（{def.Fields.Count}項目）");
        }
        catch (TestDataException ex)
        {
            AppLogger.Error("設定読み込みエラー", ex);
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            AppLogger.Error("設定読み込みエラー", ex);
            ShowError(ConfigService.LoadErrorMessage);
        }
    }

    private bool SaveConfig(bool saveAs)
    {
        CommitGridEdit();
        string? path = _configPath;
        if (saveAs || path is null)
        {
            using var dlg = new SaveFileDialog
            {
                Title = "設定を保存",
                Filter = ConfigService.FileFilter,
                DefaultExt = "json",
                FileName = path is null ? "testdata_definition.json" : Path.GetFileName(path),
                InitialDirectory = path is null ? _lastFolder : Path.GetDirectoryName(path),
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return false;
            path = dlg.FileName;
        }
        try
        {
            _config.Save(path, CurrentDefinition());
            _configPath = path;
            _lastFolder = Path.GetDirectoryName(path) ?? _lastFolder;
            _dirty = false;
            UpdateTitle();
            SetStatus($"設定を保存しました：{path}");
            return true;
        }
        catch (TestDataException ex)
        {
            AppLogger.Error("設定保存エラー", ex);
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            AppLogger.Error("設定保存エラー", ex);
            ShowError("設定ファイルを保存できませんでした。\n" + ex.Message);
        }
        return false;
    }

    private bool ConfirmDiscardChanges()
    {
        CommitGridEdit();
        if (!_dirty) return true;
        var r = MessageBox.Show(this, "現在のデータ定義は保存されていません。\n保存しますか？", AppTitle,
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return r switch
        {
            DialogResult.Yes => SaveConfig(false),
            DialogResult.No => true,
            _ => false,
        };
    }

    private void MarkDirty()
    {
        if (_loadingGrid || _dirty) return;
        _dirty = true;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        string name = _configPath is null ? "" : " - " + Path.GetFileName(_configPath);
        Text = AppTitle + name + (_dirty ? " *" : "");
    }

    // ---- ドラッグ＆ドロップ ----

    private void SetupDragDrop(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += OnDragEnter;
        control.DragDrop += OnDragDrop;
        foreach (Control child in control.Controls) SetupDragDrop(child);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = !_busy && e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (_busy || e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        // ドロップ元（エクスプローラー）を待たせないよう、処理は後で行う
        BeginInvoke(() =>
        {
            Activate();
            if (files.Length > 1)
            {
                ShowWarning("ファイルは1つずつドロップしてください。");
                return;
            }
            string path = files[0];
            if (Directory.Exists(path))
            {
                ShowWarning("フォルダは読み込めません。CSVファイル（*.csv）をドロップしてください。");
                return;
            }
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".csv") ImportCsv(path);
            else if (ext == ".json") ImportConfig(path);
            else ShowWarning($"「{Path.GetFileName(path)}」は読み込めません。\nCSVファイル（*.csv）をドロップしてください。");
        });
    }

    // ---- プレビュー・生成 ----

    /// <summary>入力内容を確認し、問題がなければ定義のコピーと件数を返す。</summary>
    private bool TryPrepare(out List<FieldDefinition> fields, out int count)
    {
        CommitGridEdit();
        fields = _fields.Select(f => f.Clone()).ToList();
        if (!ValidationService.TryParseRecordCount(_count.Text, out count, out string countError))
        {
            ShowError(countError);
            _count.Focus();
            _count.SelectAll();
            return false;
        }
        var result = _validation.Validate(fields, count);
        if (!result.IsValid)
        {
            const int max = 10;
            var lines = result.Errors.Take(max).Select(e => "・" + e).ToList();
            if (result.Errors.Count > max) lines.Add($"ほか{result.Errors.Count - max}件");
            ShowError("入力内容に誤りがあります。\n\n" + string.Join("\n", lines));
            return false;
        }
        return true;
    }

    private void ShowPreview()
    {
        if (_busy || !TryPrepare(out var fields, out int count)) return;
        using var form = new PreviewForm(fields, count);
        form.ShowDialog(this);
    }

    private async Task ExportAsync(IDataExporter exporter)
    {
        if (_busy || !TryPrepare(out var fields, out int count)) return;

        string path;
        using (var dlg = new SaveFileDialog
        {
            Title = $"{exporter.DisplayName}ファイルの保存先",
            Filter = exporter.FileFilter,
            DefaultExt = exporter.DefaultExtension,
            FileName = $"testdata_{DateTime.Now:yyyyMMdd_HHmmss}.{exporter.DefaultExtension}",
            InitialDirectory = _lastFolder,
            OverwritePrompt = true,
        })
        {
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            path = dlg.FileName;
        }
        _lastFolder = Path.GetDirectoryName(path) ?? _lastFolder;
        if (exporter is CsvService csv) csv.WriteBom = _bom.Checked;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        SetBusy(true, count);
        var sw = Stopwatch.StartNew();
        var progress = new Progress<long>(n => UpdateProgress(n, count));
        AppLogger.Info($"出力開始：形式={exporter.DisplayName} 件数={count} 項目数={fields.Count}");
        try
        {
            await Task.Run(() => exporter.Export(path, fields, _generation.GenerateRows(fields, count, null, token), progress, token), token);
            sw.Stop();
            UpdateProgress(count, count);
            AppLogger.Info($"出力完了：件数={count} 処理時間={sw.Elapsed.TotalSeconds:F1}秒");
            SetStatus($"{count:N0}件を出力しました：{path}（{sw.Elapsed.TotalSeconds:F1}秒）");
            SetBusy(false, 0);
            if (!_closeAfterCancel)
            {
                var r = MessageBox.Show(this,
                    $"出力が完了しました。\n\n件数：{count:N0}件\nファイル：{path}\n処理時間：{sw.Elapsed.TotalSeconds:F1}秒\n\n保存先のフォルダを開きますか？",
                    AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (r == DialogResult.Yes) OpenInExplorer(path);
            }
        }
        catch (OperationCanceledException)
        {
            AppLogger.Info("出力キャンセル");
            SetStatus("キャンセルしました。ファイルは出力されていません。");
        }
        catch (TestDataException ex)
        {
            AppLogger.Error("出力エラー", ex);
            SetStatus("エラーのため出力を中止しました。");
            ShowError(ex.Message);
        }
        catch (OutOfMemoryException ex)
        {
            AppLogger.Error("メモリ不足", ex);
            SetStatus("エラーのため出力を中止しました。");
            ShowError("メモリが不足しています。生成件数を減らすか、重複禁止の項目を減らしてください。");
        }
        catch (Exception ex)
        {
            AppLogger.Error("出力中の予期しないエラー", ex);
            SetStatus("エラーのため出力を中止しました。");
            ShowError($"予期しないエラーが発生しました。\n{ex.Message}\n\n詳細はログファイルを確認してください。\n{AppLogger.CurrentLogFile}");
        }
        finally
        {
            SetBusy(false, 0);
            _cts.Dispose();
            _cts = null;
            if (_closeAfterCancel) Close();
        }
    }

    private void CancelGeneration()
    {
        if (_cts is null) return;
        _cancel.Enabled = false;
        _progressLabel.Text = "キャンセルしています...";
        _cts.Cancel();
    }

    private void SetBusy(bool busy, int total)
    {
        _busy = busy;
        foreach (var c in _lockWhileBusy) c.Enabled = !busy;
        foreach (var m in _menuLockWhileBusy) m.Enabled = !busy;
        _progressPanel.Visible = busy;
        _cancel.Enabled = busy;
        UseWaitCursor = false;
        if (busy)
        {
            _progress.Value = 0;
            _progressLabel.Text = $"0 / {total:N0}";
            SetStatus("生成中...");
            _cancel.Focus();
        }
    }

    private void UpdateProgress(long done, long total)
    {
        if (!_busy || _cts is { IsCancellationRequested: true }) return;
        int percent = total == 0 ? 100 : (int)(done * 100 / total);
        _progress.Value = Math.Clamp(percent, 0, 100);
        _progressLabel.Text = $"{done:N0} / {total:N0}（{percent}%）";
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_busy)
        {
            e.Cancel = true;
            if (MessageBox.Show(this, "生成中です。中止して終了しますか？", AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _closeAfterCancel = true;
                CancelGeneration();
            }
            return;
        }
        if (_closeAfterCancel) return;
        if (!ConfirmDiscardChanges()) e.Cancel = true;
    }

    // ---- ヘルプ ----

    private void ShowUsage()
    {
        MessageBox.Show(this,
            "【基本的な使い方】\n\n" +
            "1. 「CSVを読み込む」で列名だけのCSVを読み込むか、ドラッグ＆ドロップします。\n" +
            "   （「項目を追加」で手動で作ることもできます）\n" +
            "2. 一覧で各項目の「データ型」「NULL率」「重複禁止」を設定します。\n" +
            "3. 右側の「生成ルール」で範囲や形式を設定します。\n" +
            "4. 生成件数を入力し、「プレビュー」で内容を確認します。\n" +
            "5. 「CSV生成」または「Excel生成」で保存します。\n\n" +
            "定義は「設定を保存」でJSONファイルに保存し、次回「設定を読み込む」で再利用できます。\n\n" +
            "【ショートカット】\n" +
            "F5：プレビュー　Ctrl+S：設定を保存　Ctrl+O：設定を読み込む　Ctrl+I：CSVを読み込む",
            "使い方 - " + AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowAbout()
    {
        string version = typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        MessageBox.Show(this,
            $"TestDataMaker Ver.{version}\n\n" +
            "テストデータ作成ツール\n\n" +
            "・インターネット通信は一切行いません（完全オフライン動作）。\n" +
            "・生成される氏名・住所・電話番号等はすべてダミーデータです。\n" +
            "・入力した定義や生成データを外部に送信することはありません。\n\n" +
            $"ログ保存先：{AppLogger.LogDirectory}",
            "バージョン情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(AppLogger.LogDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppLogger.LogDirectory}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLogger.Error("ログフォルダを開けません", ex);
            ShowError("ログフォルダを開けませんでした。\n" + AppLogger.LogDirectory);
        }
    }

    private static void OpenInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLogger.Error("エクスプローラーを開けません", ex);
        }
    }

    // ---- メッセージ ----

    private void SetStatus(string text) => _status.Text = text;

    private void ShowError(string message) =>
        MessageBox.Show(this, message, "エラー - " + AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

    private void ShowWarning(string message) =>
        MessageBox.Show(this, message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _cts?.Dispose();
        base.Dispose(disposing);
    }
}
