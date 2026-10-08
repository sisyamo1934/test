using System.Globalization;

namespace TestDataMaker.Generators;

/// <summary>最小値～最大値の範囲で、指定した小数桁数の数値を一様に生成する。</summary>
public sealed class NumberGenerator : ValueGenerator
{
    public const int MaxDecimalPlaces = 6;

    private readonly long _low;
    private readonly long _count;
    private readonly decimal _scale;
    private readonly string _format;

    public NumberGenerator(decimal min, decimal max, int decimalPlaces)
    {
        if (min > max) throw new ArgumentException("最小値は最大値以下に設定してください。");
        if (decimalPlaces < 0 || decimalPlaces > MaxDecimalPlaces) throw new ArgumentOutOfRangeException(nameof(decimalPlaces));

        long? count = CountValues(min, max, decimalPlaces);
        if (count == 0) throw new ArgumentException("指定された範囲と小数桁数では生成できる数値がありません。");
        if (count is null) throw new ArgumentException("数値の範囲が大きすぎます。");

        _scale = Pow10(decimalPlaces);
        _low = (long)Math.Ceiling(min * _scale);
        _count = count.Value;
        _format = "F" + decimalPlaces.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>範囲と小数桁数から生成できる値の数を求める。0 は生成不可、null は範囲が大きすぎる。</summary>
    public static long? CountValues(decimal min, decimal max, int decimalPlaces)
    {
        if (min > max || decimalPlaces < 0 || decimalPlaces > MaxDecimalPlaces) return 0;
        try
        {
            decimal scale = Pow10(decimalPlaces);
            decimal lo = Math.Ceiling(min * scale);
            decimal hi = Math.Floor(max * scale);
            decimal count = hi - lo + 1;
            if (count <= 0) return 0;
            if (count > long.MaxValue || lo < long.MinValue || hi > long.MaxValue) return null;
            return (long)count;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static decimal Pow10(int n)
    {
        decimal r = 1;
        for (int i = 0; i < n; i++) r *= 10;
        return r;
    }

    public override long DomainSize => _count;

    public override string Generate(Random random, long rowIndex) => GetValue(random.NextInt64(_count));

    public override string GetValue(long index)
    {
        decimal value = (_low + index) / _scale;
        return value.ToString(_format, CultureInfo.InvariantCulture);
    }
}
