namespace TestDataMaker.Generators;

/// <summary>全レコードに同じ値を設定する。</summary>
public sealed class FixedGenerator : ValueGenerator
{
    private readonly string _value;

    public FixedGenerator(string value)
    {
        _value = value ?? "";
    }

    public override long DomainSize => 1;

    public override string Generate(Random random, long rowIndex) => _value;

    public override string GetValue(long index) => _value;
}
