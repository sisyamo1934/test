using TestDataMaker.Data;
using TestDataMaker.Models;

namespace TestDataMaker.Generators;

/// <summary>アプリ内蔵の姓・名データから日本人名（ダミー）を生成する。</summary>
public sealed class NameGenerator : ValueGenerator
{
    private readonly NameFormat _format;
    private readonly string[] _last;
    private readonly string[] _first;

    public NameGenerator(NameFormat format)
    {
        _format = format;
        bool kana = format == NameFormat.FullNameKana;
        _last = JapaneseNameData.LastNames.Select(n => kana ? n.Kana : n.Kanji).Distinct().ToArray();
        _first = JapaneseNameData.FirstNames.Select(n => kana ? n.Kana : n.Kanji).Distinct().ToArray();
    }

    public override long DomainSize => _format switch
    {
        NameFormat.LastName => _last.Length,
        NameFormat.FirstName => _first.Length,
        _ => (long)_last.Length * _first.Length,
    };

    public override string Generate(Random random, long rowIndex) => GetValue(random.NextInt64(DomainSize));

    public override string GetValue(long index) => _format switch
    {
        NameFormat.LastName => _last[index],
        NameFormat.FirstName => _first[index],
        NameFormat.FullNameNoSpace => _last[index / _first.Length] + _first[index % _first.Length],
        NameFormat.FullNameKana => _last[index / _first.Length] + "　" + _first[index % _first.Length],
        _ => _last[index / _first.Length] + " " + _first[index % _first.Length],
    };
}
