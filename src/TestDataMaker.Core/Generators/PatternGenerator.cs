using System.Text;
using TestDataMaker.Models;

namespace TestDataMaker.Generators;

/// <summary>
/// 「固定部分＋ランダム数字」のパターン群から値を生成する共通処理。
/// パターン中の '#' は 0～9、'N' は 1～9 の数字に置き換える。
/// </summary>
public abstract class PatternGenerator : ValueGenerator
{
    private readonly string[] _patterns;
    private readonly long[] _sizes;
    private readonly long _total;

    protected PatternGenerator(IEnumerable<string> patterns)
    {
        _patterns = patterns.ToArray();
        _sizes = _patterns.Select(SizeOf).ToArray();
        _total = _sizes.Aggregate(0L, SaturatingMath.Add);
    }

    private static long SizeOf(string pattern)
    {
        long size = 1;
        foreach (char c in pattern)
        {
            if (c == '#') size = SaturatingMath.Multiply(size, 10);
            else if (c == 'N') size = SaturatingMath.Multiply(size, 9);
        }
        return size;
    }

    public override long DomainSize => _total;

    public override string Generate(Random random, long rowIndex)
    {
        // パターン（市外局番など）は均等に選び、その中の数字をランダムにする
        int p = random.Next(_patterns.Length);
        return Fill(_patterns[p], random.NextInt64(_sizes[p]));
    }

    public override string GetValue(long index)
    {
        for (int p = 0; p < _patterns.Length; p++)
        {
            if (index < _sizes[p]) return Fill(_patterns[p], index);
            index -= _sizes[p];
        }
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    private static string Fill(string pattern, long index)
    {
        var buf = new StringBuilder(pattern);
        for (int i = buf.Length - 1; i >= 0; i--)
        {
            if (buf[i] == '#')
            {
                buf[i] = (char)('0' + index % 10);
                index /= 10;
            }
            else if (buf[i] == 'N')
            {
                buf[i] = (char)('1' + index % 9);
                index /= 9;
            }
        }
        return buf.ToString();
    }
}

/// <summary>日本国内形式の電話番号（ダミー）。</summary>
public sealed class PhoneGenerator : PatternGenerator
{
    private static readonly string[] Mobile = { "090-N###-####", "080-N###-####", "070-N###-####" };

    private static readonly string[] Landline =
    {
        "03-N###-####", "06-N###-####", "011-N##-####", "022-N##-####", "045-N##-####",
        "048-N##-####", "052-N##-####", "075-N##-####", "078-N##-####", "092-N##-####",
    };

    public PhoneGenerator(PhoneType type)
        : base(type switch
        {
            PhoneType.Mobile => Mobile,
            PhoneType.Landline => Landline,
            _ => Mobile.Concat(Landline),
        })
    {
    }
}

/// <summary>郵便番号（NNN-NNNN 形式）。</summary>
public sealed class PostalCodeGenerator : PatternGenerator
{
    public PostalCodeGenerator() : base(new[] { "###-####" })
    {
    }
}
