namespace TestDataMaker.Generators;

/// <summary>
/// 1 項目分の値を生成する Generator の基底クラス。
/// 新しいデータ型（UUID、IP アドレス等）を追加する場合はこのクラスを継承し、
/// <see cref="GeneratorFactory"/> に登録する。
/// </summary>
public abstract class ValueGenerator
{
    /// <summary>1 件分の値を生成する。</summary>
    /// <param name="random">乱数（Seed 指定時の再現性のため、必ずこれを使うこと）。</param>
    /// <param name="rowIndex">0 始まりの行番号。</param>
    public abstract string Generate(Random random, long rowIndex);

    /// <summary>生成しうる値の種類数。long の範囲を超える場合は long.MaxValue。</summary>
    public abstract long DomainSize { get; }

    /// <summary>
    /// 0～DomainSize-1 のインデックスに対応する値を返す（異なるインデックスは原則異なる値）。
    /// 重複禁止で値が不足気味のときに、重複なく値を選ぶために使う。
    /// </summary>
    public abstract string GetValue(long index);

    /// <summary>行番号から値が決まり、常に重複しない Generator の場合 true（連番など）。</summary>
    public virtual bool IsInherentlyUnique => false;
}

internal static class SaturatingMath
{
    public static long Add(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;

    public static long Multiply(long a, long b)
    {
        if (a == 0 || b == 0) return 0;
        return a > long.MaxValue / b ? long.MaxValue : a * b;
    }

    public static long Pow(long b, int e)
    {
        long r = 1;
        for (int i = 0; i < e; i++)
        {
            r = Multiply(r, b);
            if (r == long.MaxValue) break;
        }
        return r;
    }
}
