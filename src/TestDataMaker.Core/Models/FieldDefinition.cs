using System.Globalization;

namespace TestDataMaker.Models;

/// <summary>
/// 1 項目（1 列）分の生成定義。
/// 型ごとの設定はフラットに保持し、該当しない型では無視する（JSON の互換性を保ちやすくするため）。
/// </summary>
public sealed class FieldDefinition
{
    public string Name { get; set; } = "";
    public DataType DataType { get; set; } = DataType.String;

    /// <summary>NULL 率（0～100 %）。</summary>
    public double NullRate { get; set; }

    /// <summary>true の場合、NULL 以外の値が重複しないように生成する。</summary>
    public bool Unique { get; set; }

    // 文字列
    public int MinLength { get; set; } = 8;
    public int MaxLength { get; set; } = 8;
    public StringCharSet CharSet { get; set; } = StringCharSet.Alphanumeric;
    public string CustomChars { get; set; } = "";
    public string Prefix { get; set; } = "";

    // 数値
    public decimal MinValue { get; set; } = 0;
    public decimal MaxValue { get; set; } = 100;
    public int DecimalPlaces { get; set; }

    // 連番
    public long StartValue { get; set; } = 1;
    public long Increment { get; set; } = 1;

    // 日付
    public DateTime StartDate { get; set; } = DateTime.Today.AddYears(-1);
    public DateTime EndDate { get; set; } = DateTime.Today;
    public string DateFormat { get; set; } = "yyyy/MM/dd";

    // 日本人名
    public NameFormat NameFormat { get; set; } = NameFormat.FullName;

    // メールアドレス
    public string EmailDomain { get; set; } = "example.com";
    public EmailMode EmailMode { get; set; } = EmailMode.Sequential;

    // 電話番号
    public PhoneType PhoneType { get; set; } = PhoneType.Mixed;

    // 固定値
    public string FixedValue { get; set; } = "";

    public FieldDefinition Clone() => (FieldDefinition)MemberwiseClone();

    /// <summary>一覧の「生成ルール」列に表示する要約。</summary>
    public string RuleSummary
    {
        get
        {
            var ci = CultureInfo.InvariantCulture;
            return DataType switch
            {
                DataType.String => $"{DisplayNames.Of(CharSet)} {(MinLength == MaxLength ? $"{MinLength}" : $"{MinLength}～{MaxLength}")}文字"
                                   + (Prefix.Length > 0 ? $"（接頭辞 {Prefix}）" : ""),
                DataType.JapaneseName => DisplayNames.Of(NameFormat),
                DataType.Number => $"{MinValue.ToString(ci)}～{MaxValue.ToString(ci)}" + (DecimalPlaces > 0 ? $"（小数{DecimalPlaces}桁）" : ""),
                DataType.Sequence => $"開始 {StartValue} / 増分 {Increment}",
                DataType.Date => $"{StartDate:yyyy/MM/dd}～{EndDate:yyyy/MM/dd}（{DateFormat}）",
                DataType.Email => $"@{EmailDomain}（{DisplayNames.Of(EmailMode)}）",
                DataType.Phone => DisplayNames.Of(PhoneType),
                DataType.PostalCode => "NNN-NNNN",
                DataType.Address => "都道府県＋市区町村＋番地",
                DataType.Fixed => $"\"{FixedValue}\"",
                _ => "",
            };
        }
    }

    /// <summary>項目名とデータ型から、よく使われる初期設定を作る。</summary>
    public static FieldDefinition Create(string name, DataType type)
    {
        var f = new FieldDefinition { Name = name, DataType = type };
        switch (type)
        {
            case DataType.Sequence:
                f.Unique = true;
                break;
            case DataType.Date:
                f.StartDate = DateTime.Today.AddYears(-50);
                f.EndDate = DateTime.Today;
                break;
        }
        return f;
    }
}
