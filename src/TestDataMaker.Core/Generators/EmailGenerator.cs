using TestDataMaker.Models;

namespace TestDataMaker.Generators;

/// <summary>メールアドレスを生成する。連番（test001@...）またはランダム。</summary>
public sealed class EmailGenerator : ValueGenerator
{
    private const string FirstChars = "abcdefghijklmnopqrstuvwxyz";
    private const string RestChars = "abcdefghijklmnopqrstuvwxyz0123456789";
    private const int RandomLength = 10;

    private readonly string _domain;
    private readonly EmailMode _mode;

    public EmailGenerator(string domain, EmailMode mode)
    {
        _domain = string.IsNullOrWhiteSpace(domain) ? "example.com" : domain.Trim().TrimStart('@');
        _mode = mode;
    }

    public override bool IsInherentlyUnique => _mode == EmailMode.Sequential;

    public override long DomainSize => _mode == EmailMode.Sequential
        ? long.MaxValue
        : SaturatingMath.Multiply(FirstChars.Length, SaturatingMath.Pow(RestChars.Length, RandomLength - 1));

    public override string Generate(Random random, long rowIndex)
    {
        if (_mode == EmailMode.Sequential) return Sequential(rowIndex);
        var buf = new char[RandomLength];
        buf[0] = FirstChars[random.Next(FirstChars.Length)];
        for (int i = 1; i < buf.Length; i++) buf[i] = RestChars[random.Next(RestChars.Length)];
        return new string(buf) + "@" + _domain;
    }

    public override string GetValue(long index)
    {
        if (_mode == EmailMode.Sequential) return Sequential(index);
        var buf = new char[RandomLength];
        for (int i = buf.Length - 1; i >= 1; i--)
        {
            buf[i] = RestChars[(int)(index % RestChars.Length)];
            index /= RestChars.Length;
        }
        buf[0] = FirstChars[(int)(index % FirstChars.Length)];
        return new string(buf) + "@" + _domain;
    }

    private string Sequential(long rowIndex) => $"test{rowIndex + 1:D3}@{_domain}";
}
