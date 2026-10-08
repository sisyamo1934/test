using System.Text;
using TestDataMaker.Models;

namespace TestDataMaker.Generators;

public sealed class StringGenerator : ValueGenerator
{
    public const string UpperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string LowerChars = "abcdefghijklmnopqrstuvwxyz";
    public const string DigitChars = "0123456789";
    public const string HiraganaChars = "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをん";
    public const string KatakanaChars = "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン";

    private readonly char[] _chars;
    private readonly int _minLength;
    private readonly int _maxLength;
    private readonly string _prefix;

    public StringGenerator(string chars, int minLength, int maxLength, string prefix = "")
    {
        _chars = (chars ?? "").Distinct().ToArray();
        if (_chars.Length == 0) throw new ArgumentException("使用文字が指定されていません。", nameof(chars));
        if (minLength < 0 || maxLength < minLength) throw new ArgumentOutOfRangeException(nameof(minLength));
        _minLength = minLength;
        _maxLength = maxLength;
        _prefix = prefix ?? "";
    }

    public static string CharsOf(StringCharSet charSet, string customChars) => charSet switch
    {
        StringCharSet.Alphanumeric => UpperChars + LowerChars + DigitChars,
        StringCharSet.Alphabet => UpperChars + LowerChars,
        StringCharSet.UpperCase => UpperChars,
        StringCharSet.LowerCase => LowerChars,
        StringCharSet.Numeric => DigitChars,
        StringCharSet.Hiragana => HiraganaChars,
        StringCharSet.Katakana => KatakanaChars,
        StringCharSet.Custom => customChars ?? "",
        _ => UpperChars + LowerChars + DigitChars,
    };

    public override long DomainSize
    {
        get
        {
            long total = 0;
            for (int len = _minLength; len <= _maxLength; len++)
            {
                total = SaturatingMath.Add(total, SaturatingMath.Pow(_chars.Length, len));
                if (total == long.MaxValue) break;
            }
            return total;
        }
    }

    public override string Generate(Random random, long rowIndex)
    {
        int len = random.Next(_minLength, _maxLength + 1);
        var sb = new StringBuilder(_prefix.Length + len);
        sb.Append(_prefix);
        for (int i = 0; i < len; i++)
            sb.Append(_chars[random.Next(_chars.Length)]);
        return sb.ToString();
    }

    public override string GetValue(long index)
    {
        for (int len = _minLength; len <= _maxLength; len++)
        {
            long bucket = SaturatingMath.Pow(_chars.Length, len);
            if (index < bucket || bucket == long.MaxValue)
                return _prefix + ToDigits(index, len);
            index -= bucket;
        }
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    private string ToDigits(long index, int len)
    {
        var buf = new char[len];
        for (int i = len - 1; i >= 0; i--)
        {
            buf[i] = _chars[index % _chars.Length];
            index /= _chars.Length;
        }
        return new string(buf);
    }
}
