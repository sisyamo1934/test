namespace TestDataMaker.Models;

/// <summary>テストデータ定義全体（設定ファイル 1 つ分）。</summary>
public sealed class TestDataDefinition
{
    /// <summary>設定ファイル形式のバージョン。互換性のない変更時に上げる。</summary>
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    public const int CurrentFormatVersion = 1;

    public int RecordCount { get; set; } = 1000;

    public List<FieldDefinition> Fields { get; set; } = new();
}

/// <summary>生成時のオプション。将来の Seed 指定などはここに追加する。</summary>
public sealed class GenerationOptions
{
    /// <summary>乱数シード。null の場合は毎回異なるデータになる。</summary>
    public int? Seed { get; set; }
}
