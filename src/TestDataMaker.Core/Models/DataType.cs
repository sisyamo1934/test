namespace TestDataMaker.Models;

/// <summary>生成するデータの種類。JSON には名前で保存されるため、既存の名前は変更しないこと。</summary>
public enum DataType
{
    String,
    JapaneseName,
    Number,
    Sequence,
    Date,
    Email,
    Phone,
    PostalCode,
    Address,
    Fixed,
}

public enum StringCharSet
{
    Alphanumeric,
    Alphabet,
    UpperCase,
    LowerCase,
    Numeric,
    Hiragana,
    Katakana,
    Custom,
}

public enum NameFormat
{
    FullName,
    FullNameNoSpace,
    LastName,
    FirstName,
    FullNameKana,
}

public enum EmailMode
{
    Sequential,
    Random,
}

public enum PhoneType
{
    Mixed,
    Mobile,
    Landline,
}

/// <summary>画面表示用の日本語名。</summary>
public static class DisplayNames
{
    public static string Of(DataType type) => type switch
    {
        DataType.String => "文字列",
        DataType.JapaneseName => "日本人名",
        DataType.Number => "数値",
        DataType.Sequence => "連番",
        DataType.Date => "日付",
        DataType.Email => "メールアドレス",
        DataType.Phone => "電話番号",
        DataType.PostalCode => "郵便番号",
        DataType.Address => "住所",
        DataType.Fixed => "固定値",
        _ => type.ToString(),
    };

    public static string Of(StringCharSet charSet) => charSet switch
    {
        StringCharSet.Alphanumeric => "英数字",
        StringCharSet.Alphabet => "英字",
        StringCharSet.UpperCase => "英大文字",
        StringCharSet.LowerCase => "英小文字",
        StringCharSet.Numeric => "数字",
        StringCharSet.Hiragana => "ひらがな",
        StringCharSet.Katakana => "カタカナ",
        StringCharSet.Custom => "任意の文字",
        _ => charSet.ToString(),
    };

    public static string Of(NameFormat format) => format switch
    {
        NameFormat.FullName => "姓 名（スペース区切り）",
        NameFormat.FullNameNoSpace => "姓名（区切りなし）",
        NameFormat.LastName => "姓のみ",
        NameFormat.FirstName => "名のみ",
        NameFormat.FullNameKana => "セイ メイ（カタカナ）",
        _ => format.ToString(),
    };

    public static string Of(EmailMode mode) => mode switch
    {
        EmailMode.Sequential => "連番",
        EmailMode.Random => "ランダム",
        _ => mode.ToString(),
    };

    public static string Of(PhoneType type) => type switch
    {
        PhoneType.Mixed => "携帯・固定混在",
        PhoneType.Mobile => "携帯電話",
        PhoneType.Landline => "固定電話",
        _ => type.ToString(),
    };
}
