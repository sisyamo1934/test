using TestDataMaker.Generators;
using TestDataMaker.Models;

namespace TestDataMaker.UI;

/// <summary>選択中の項目の「生成ルール」をデータ型に応じて編集するパネル。</summary>
internal sealed class FieldDetailPanel : UserControl
{
    private static readonly string[] DateFormats =
    {
        "yyyy/MM/dd", "yyyy-MM-dd", "yyyyMMdd", "yyyy年M月d日",
        "yyyy/MM/dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyyMMddHHmmss",
    };

    private readonly Label _title = new() { AutoSize = true, Font = new Font("Yu Gothic UI", 10.5f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) };
    private readonly TableLayoutPanel _table = new() { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Label _sample = new() { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 12, 0, 0), MaximumSize = new Size(360, 0) };
    private FieldDefinition? _field;
    private bool _loading;

    /// <summary>設定が変更されたときに発生する。</summary>
    public event EventHandler? FieldChanged;

    public FieldDetailPanel()
    {
        AutoScroll = true;
        Padding = new Padding(12);
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        layout.Controls.Add(_title);
        layout.Controls.Add(_table);
        layout.Controls.Add(_sample);
        Controls.Add(layout);
        Field = null;
    }

    public FieldDefinition? Field
    {
        get => _field;
        set
        {
            _field = value;
            Rebuild();
        }
    }

    /// <summary>データ型変更時など、外部から設定が変わったときに再表示する。</summary>
    public void Rebuild()
    {
        _loading = true;
        SuspendLayout();
        _table.SuspendLayout();
        foreach (Control c in _table.Controls.Cast<Control>().ToList()) c.Dispose();
        _table.Controls.Clear();
        _table.RowStyles.Clear();
        _table.RowCount = 0;

        if (_field is null)
        {
            _title.Text = "生成ルール";
            AddNote("左の一覧で項目を選択すると、ここで生成ルールを設定できます。");
        }
        else
        {
            _title.Text = $"生成ルール：{(_field.Name.Length > 0 ? _field.Name : "（項目名なし）")}（{DisplayNames.Of(_field.DataType)}）";
            BuildEditors(_field);
        }

        _table.ResumeLayout();
        ResumeLayout();
        _loading = false;
        UpdateSample();
    }

    private void BuildEditors(FieldDefinition f)
    {
        switch (f.DataType)
        {
            case DataType.String:
            {
                var custom = new TextBox { Text = f.CustomChars, Width = 220, Enabled = f.CharSet == StringCharSet.Custom };
                AddRow("使用文字", EnumCombo(f.CharSet, DisplayNames.Of, v =>
                {
                    f.CharSet = v;
                    custom.Enabled = v == StringCharSet.Custom;
                }));
                AddRow("任意の文字", Bind(custom, v => f.CustomChars = v));
                AddNote("「任意の文字」を選んだ場合、ここに入力した文字から生成します。（例：ABC123）");
                AddRow("最小文字数", Number(f.MinLength, 0, Services.ValidationService.MaxStringLength, 0, v => f.MinLength = (int)v));
                AddRow("最大文字数", Number(f.MaxLength, 0, Services.ValidationService.MaxStringLength, 0, v => f.MaxLength = (int)v));
                AddRow("接頭辞", Bind(new TextBox { Text = f.Prefix, Width = 160 }, v => f.Prefix = v));
                AddNote("接頭辞を指定すると先頭に付けます。（例：test_）");
                break;
            }
            case DataType.JapaneseName:
                AddRow("形式", EnumCombo(f.NameFormat, DisplayNames.Of, v => f.NameFormat = v));
                AddNote("内蔵の姓・名をランダムに組み合わせたダミーの氏名です。");
                break;

            case DataType.Number:
            {
                var min = Number(f.MinValue, -999_999_999_999m, 999_999_999_999m, f.DecimalPlaces, v => f.MinValue = v);
                var max = Number(f.MaxValue, -999_999_999_999m, 999_999_999_999m, f.DecimalPlaces, v => f.MaxValue = v);
                AddRow("最小値", min);
                AddRow("最大値", max);
                AddRow("小数桁数", Number(f.DecimalPlaces, 0, NumberGenerator.MaxDecimalPlaces, 0, v =>
                {
                    f.DecimalPlaces = (int)v;
                    min.DecimalPlaces = max.DecimalPlaces = (int)v;
                }));
                AddNote("小数桁数を 0 にすると整数になります。");
                break;
            }
            case DataType.Sequence:
                AddRow("開始値", Number(f.StartValue, -999_999_999_999_999m, 999_999_999_999_999m, 0, v => f.StartValue = (long)v));
                AddRow("増分", Number(f.Increment, -999_999_999m, 999_999_999m, 0, v => f.Increment = (long)v));
                AddNote("1件目が開始値になり、以降は増分ずつ増えます。");
                break;

            case DataType.Date:
            {
                var start = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy/MM/dd", Width = 130, Value = Clamp(f.StartDate) };
                var end = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy/MM/dd", Width = 130, Value = Clamp(f.EndDate) };
                start.ValueChanged += (_, _) => Changed(() => f.StartDate = start.Value.Date);
                end.ValueChanged += (_, _) => Changed(() => f.EndDate = end.Value.Date);
                AddRow("開始日", start);
                AddRow("終了日", end);

                var preset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
                preset.Items.AddRange(new object[] { "（選択してください）", "過去1年", "過去10年", "過去50年", "今後1年", "18～80歳の生年月日" });
                preset.SelectedIndex = 0;
                preset.SelectedIndexChanged += (_, _) =>
                {
                    if (_loading || preset.SelectedIndex <= 0) return;
                    var today = DateTime.Today;
                    (DateTime s, DateTime e) = preset.SelectedIndex switch
                    {
                        1 => (today.AddYears(-1), today),
                        2 => (today.AddYears(-10), today),
                        3 => (today.AddYears(-50), today),
                        4 => (today, today.AddYears(1)),
                        _ => (today.AddYears(-80), today.AddYears(-18)),
                    };
                    start.Value = s;
                    end.Value = e;
                };
                AddRow("期間の簡単設定", preset);

                var format = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 200, Text = f.DateFormat };
                format.Items.AddRange(DateFormats);
                format.Text = f.DateFormat;
                format.TextChanged += (_, _) => Changed(() => f.DateFormat = format.Text);
                AddRow("日付形式", format);
                AddNote("yyyy=年 MM=月 dd=日 HH=時 mm=分 ss=秒");
                break;
            }
            case DataType.Email:
                AddRow("ドメイン", Bind(new TextBox { Text = f.EmailDomain, Width = 200 }, v => f.EmailDomain = v));
                AddRow("形式", EnumCombo(f.EmailMode, DisplayNames.Of, v => f.EmailMode = v));
                AddNote("連番：test001@example.com, test002@... の順に生成します。\nランダム：英数字10文字のアドレスを生成します。");
                AddNote("実在するドメインへの誤送信を防ぐため、example.com などテスト用ドメインの使用を推奨します。");
                break;

            case DataType.Phone:
                AddRow("種類", EnumCombo(f.PhoneType, DisplayNames.Of, v => f.PhoneType = v));
                AddNote("日本国内形式（090-1234-5678、03-1234-5678 など）のダミー番号です。");
                break;

            case DataType.PostalCode:
                AddNote("NNN-NNNN 形式（例：100-0001）の番号をランダムに生成します。");
                break;

            case DataType.Address:
                AddNote("都道府県・市区町村に、ランダムな町名・丁目・番地を組み合わせたダミー住所です。");
                break;

            case DataType.Fixed:
                AddRow("固定値", Bind(new TextBox { Text = f.FixedValue, Width = 220 }, v => f.FixedValue = v));
                AddNote("全レコードに同じ値を設定します。（例：ACTIVE）");
                break;
        }
    }

    private static DateTime Clamp(DateTime d)
    {
        if (d < DateTimePicker.MinimumDateTime) return DateTimePicker.MinimumDateTime;
        if (d > DateTimePicker.MaximumDateTime) return DateTimePicker.MaximumDateTime;
        return d;
    }

    private void Changed(Action apply)
    {
        if (_loading) return;
        apply();
        UpdateSample();
        FieldChanged?.Invoke(this, EventArgs.Empty);
    }

    private TextBox Bind(TextBox box, Action<string> apply)
    {
        box.TextChanged += (_, _) => Changed(() => apply(box.Text));
        return box;
    }

    private NumericUpDown Number(decimal value, decimal min, decimal max, int decimals, Action<decimal> apply)
    {
        var nud = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            DecimalPlaces = decimals,
            ThousandsSeparator = true,
            Width = 160,
            TextAlign = HorizontalAlignment.Right,
        };
        nud.Value = Math.Clamp(value, min, max);
        nud.ValueChanged += (_, _) => Changed(() => apply(nud.Value));
        return nud;
    }

    private ComboBox EnumCombo<T>(T value, Func<T, string> display, Action<T> apply) where T : struct, Enum
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        var items = Enum.GetValues<T>().Select(v => new ComboItem<T>(v, display(v))).ToArray();
        combo.Items.AddRange(items);
        combo.SelectedItem = items.First(i => EqualityComparer<T>.Default.Equals(i.Value, value));
        combo.SelectedIndexChanged += (_, _) => Changed(() => apply(((ComboItem<T>)combo.SelectedItem!).Value));
        return combo;
    }

    private void AddRow(string label, Control control)
    {
        int row = _table.RowCount++;
        _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) }, 0, row);
        control.Margin = new Padding(0, 3, 0, 3);
        control.Anchor = AnchorStyles.Left;
        _table.Controls.Add(control, 1, row);
    }

    private void AddNote(string text)
    {
        int row = _table.RowCount++;
        _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var note = new Label { Text = text, AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(360, 0), Margin = new Padding(0, 2, 0, 6) };
        _table.Controls.Add(note, 0, row);
        _table.SetColumnSpan(note, 2);
    }

    /// <summary>現在の設定で数件生成して例を表示する。</summary>
    private void UpdateSample()
    {
        if (_field is null)
        {
            _sample.Text = "";
            return;
        }
        try
        {
            var gen = GeneratorFactory.Create(_field);
            var random = new Random();
            var samples = Enumerable.Range(0, 3).Select(i => gen.Generate(random, i));
            _sample.ForeColor = SystemColors.GrayText;
            _sample.Text = "生成例：\n" + string.Join("\n", samples.Select(s => "  " + s));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            _sample.ForeColor = Color.Firebrick;
            _sample.Text = "設定エラー：" + ex.Message;
        }
    }
}

/// <summary>コンボボックス用の「値＋表示名」。</summary>
internal sealed record ComboItem<T>(T Value, string Text)
{
    public override string ToString() => Text;
}
