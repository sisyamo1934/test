namespace TestDataMaker.Generators;

/// <summary>NULL 率に従って、その値を NULL にするかどうかを決める。</summary>
public sealed class NullGenerator
{
    private readonly double _rate;

    /// <param name="nullRate">NULL 率（0～100 %）。</param>
    public NullGenerator(double nullRate)
    {
        _rate = Math.Clamp(nullRate, 0, 100);
    }

    public bool ShouldBeNull(Random random)
    {
        if (_rate <= 0) return false;
        if (_rate >= 100) return true;
        return random.NextDouble() * 100 < _rate;
    }
}
