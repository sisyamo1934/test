using System.Globalization;

namespace TestDataMaker.Generators;

/// <summary>開始値から増分ずつ増える連番。行番号から値が決まる。</summary>
public sealed class SequenceGenerator : ValueGenerator
{
    private readonly long _start;
    private readonly long _increment;

    public SequenceGenerator(long start, long increment)
    {
        if (increment == 0) throw new ArgumentException("増分は0以外を指定してください。", nameof(increment));
        _start = start;
        _increment = increment;
    }

    public override bool IsInherentlyUnique => true;

    public override long DomainSize => long.MaxValue;

    public override string Generate(Random random, long rowIndex) => GetValue(rowIndex);

    public override string GetValue(long index)
    {
        try
        {
            return checked(_start + _increment * index).ToString(CultureInfo.InvariantCulture);
        }
        catch (OverflowException)
        {
            throw new InvalidOperationException("連番が数値の上限を超えました。開始値・増分を見直してください。");
        }
    }

    /// <summary>件数分の連番が long の範囲に収まるか。</summary>
    public static bool Fits(long start, long increment, long count)
    {
        if (count <= 0) return true;
        try
        {
            _ = checked(start + increment * (count - 1));
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
