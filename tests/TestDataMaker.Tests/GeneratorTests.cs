using System.Globalization;
using System.Text.RegularExpressions;
using TestDataMaker.Generators;
using TestDataMaker.Models;

namespace TestDataMaker.Tests;

public class NumberGeneratorTests
{
    [Fact]
    public void 生成値は最小値以上かつ最大値以下になる()
    {
        var gen = new NumberGenerator(18, 80, 0);
        var random = new Random(1);
        for (int i = 0; i < 10_000; i++)
        {
            int v = int.Parse(gen.Generate(random, i), CultureInfo.InvariantCulture);
            Assert.InRange(v, 18, 80);
        }
    }

    [Fact]
    public void 最小値と最大値の両端が生成される()
    {
        var gen = new NumberGenerator(1, 3, 0);
        var random = new Random(2);
        var values = Enumerable.Range(0, 1000).Select(i => gen.Generate(random, i)).ToHashSet();
        Assert.Equal(new HashSet<string> { "1", "2", "3" }, values);
    }

    [Theory]
    [InlineData(0, 100000, 2)]
    [InlineData(-5.5, 5.5, 1)]
    [InlineData(0, 1, 4)]
    public void 小数桁数が正しく範囲内になる(double min, double max, int digits)
    {
        var gen = new NumberGenerator((decimal)min, (decimal)max, digits);
        var random = new Random(3);
        for (int i = 0; i < 2000; i++)
        {
            string s = gen.Generate(random, i);
            var parts = s.Split('.');
            Assert.Equal(2, parts.Length);
            Assert.Equal(digits, parts[1].Length);
            decimal v = decimal.Parse(s, CultureInfo.InvariantCulture);
            Assert.InRange(v, (decimal)min, (decimal)max);
        }
    }

    [Fact]
    public void 整数指定では小数点を含まない()
    {
        var gen = new NumberGenerator(0, 100000, 0);
        Assert.DoesNotContain(".", gen.Generate(new Random(4), 0));
    }

    [Fact]
    public void 最小値が最大値より大きいと例外()
    {
        Assert.Throws<ArgumentException>(() => new NumberGenerator(10, 1, 0));
    }

    [Fact]
    public void 値の種類数を計算できる()
    {
        Assert.Equal(63, new NumberGenerator(18, 80, 0).DomainSize);
        Assert.Equal(1001, new NumberGenerator(0, 10, 2).DomainSize);
        Assert.Equal(0, NumberGenerator.CountValues(0.1m, 0.2m, 0));
    }
}

public class SequenceGeneratorTests
{
    [Fact]
    public void 開始値が正しい()
    {
        var gen = new SequenceGenerator(1000, 1);
        Assert.Equal("1000", gen.Generate(new Random(), 0));
    }

    [Fact]
    public void 増分が正しい()
    {
        var gen = new SequenceGenerator(10, 5);
        var values = Enumerable.Range(0, 4).Select(i => gen.Generate(new Random(), i)).ToArray();
        Assert.Equal(new[] { "10", "15", "20", "25" }, values);
    }

    [Fact]
    public void 負の増分も扱える()
    {
        var gen = new SequenceGenerator(0, -2);
        Assert.Equal("-6", gen.Generate(new Random(), 3));
    }

    [Fact]
    public void 増分0は例外()
    {
        Assert.Throws<ArgumentException>(() => new SequenceGenerator(1, 0));
    }

    [Fact]
    public void 範囲外になるか判定できる()
    {
        Assert.True(SequenceGenerator.Fits(1, 1, 1_000_000));
        Assert.False(SequenceGenerator.Fits(long.MaxValue - 10, 1, 100));
    }
}

public class DateGeneratorTests
{
    [Fact]
    public void 指定期間内の日付になる()
    {
        var start = new DateTime(2025, 1, 1);
        var end = new DateTime(2025, 12, 31);
        var gen = new DateGenerator(start, end, "yyyy/MM/dd");
        var random = new Random(5);
        for (int i = 0; i < 5000; i++)
        {
            var d = DateTime.ParseExact(gen.Generate(random, i), "yyyy/MM/dd", CultureInfo.InvariantCulture);
            Assert.InRange(d, start, end);
        }
    }

    [Fact]
    public void 開始日と終了日が同じなら常にその日()
    {
        var gen = new DateGenerator(new DateTime(2026, 5, 13), new DateTime(2026, 5, 13), "yyyy-MM-dd");
        Assert.Equal("2026-05-13", gen.Generate(new Random(), 0));
        Assert.Equal(1, gen.DomainSize);
    }

    [Fact]
    public void 時刻を含む形式では期間内の日時になる()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 2);
        var gen = new DateGenerator(start, end, "yyyy/MM/dd HH:mm:ss");
        var random = new Random(6);
        for (int i = 0; i < 2000; i++)
        {
            var d = DateTime.ParseExact(gen.Generate(random, i), "yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
            Assert.InRange(d, start, end.AddDays(1).AddSeconds(-1));
        }
    }

    [Fact]
    public void 開始日が終了日より後なら例外()
    {
        Assert.Throws<ArgumentException>(() => new DateGenerator(new DateTime(2026, 2, 1), new DateTime(2026, 1, 1), "yyyy/MM/dd"));
    }

    [Theory]
    [InlineData("yyyy/MM/dd", true)]
    [InlineData("yyyyMMdd", true)]
    [InlineData("", false)]
    [InlineData("%", false)]
    public void 日付形式の妥当性を判定できる(string format, bool expected)
    {
        Assert.Equal(expected, DateGenerator.IsValidFormat(format));
    }
}

public class NullGeneratorTests
{
    [Fact]
    public void NULL率0パーセントなら常にNULLにならない()
    {
        var gen = new NullGenerator(0);
        var random = new Random(7);
        Assert.All(Enumerable.Range(0, 10_000), _ => Assert.False(gen.ShouldBeNull(random)));
    }

    [Fact]
    public void NULL率100パーセントなら常にNULL()
    {
        var gen = new NullGenerator(100);
        var random = new Random(8);
        Assert.All(Enumerable.Range(0, 10_000), _ => Assert.True(gen.ShouldBeNull(random)));
    }

    [Fact]
    public void NULL率10パーセントならおおよそ10パーセントがNULL()
    {
        var gen = new NullGenerator(10);
        var random = new Random(9);
        int nulls = Enumerable.Range(0, 100_000).Count(_ => gen.ShouldBeNull(random));
        Assert.InRange(nulls, 9_000, 11_000);
    }
}

public class OtherGeneratorTests
{
    private static readonly Random Rnd = new(10);

    [Fact]
    public void 文字列は指定の文字数と使用文字になる()
    {
        var gen = new StringGenerator("abc", 3, 6, "T_");
        for (int i = 0; i < 1000; i++)
        {
            string s = gen.Generate(Rnd, i);
            Assert.StartsWith("T_", s);
            Assert.InRange(s.Length - 2, 3, 6);
            Assert.Matches("^T_[abc]+$", s);
        }
    }

    [Fact]
    public void 文字列のインデックス指定は重複しない()
    {
        var gen = new StringGenerator("ab", 1, 3);
        Assert.Equal(2 + 4 + 8, gen.DomainSize);
        var all = Enumerable.Range(0, (int)gen.DomainSize).Select(i => gen.GetValue(i)).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Theory]
    [InlineData(NameFormat.FullName, @"^\S+ \S+$")]
    [InlineData(NameFormat.FullNameNoSpace, @"^\S+$")]
    [InlineData(NameFormat.FullNameKana, @"^[ァ-ヶー]+　[ァ-ヶー]+$")]
    public void 日本人名を生成できる(NameFormat format, string pattern)
    {
        var gen = new NameGenerator(format);
        for (int i = 0; i < 200; i++) Assert.Matches(pattern, gen.Generate(Rnd, i));
    }

    [Fact]
    public void メールアドレス連番はtest001形式()
    {
        var gen = new EmailGenerator("example.com", EmailMode.Sequential);
        Assert.Equal("test001@example.com", gen.Generate(Rnd, 0));
        Assert.Equal("test002@example.com", gen.Generate(Rnd, 1));
        Assert.True(gen.IsInherentlyUnique);
    }

    [Fact]
    public void メールアドレスランダムは指定ドメインになる()
    {
        var gen = new EmailGenerator("@test.co.jp", EmailMode.Random);
        Assert.Matches(@"^[a-z][a-z0-9]{9}@test\.co\.jp$", gen.Generate(Rnd, 0));
    }

    [Theory]
    [InlineData(PhoneType.Mobile, @"^0[789]0-[1-9]\d{3}-\d{4}$")]
    [InlineData(PhoneType.Landline, @"^(0\d-[1-9]\d{3}|0\d{2}-[1-9]\d{2})-\d{4}$")]
    [InlineData(PhoneType.Mixed, @"^0\d{1,2}-\d{3,4}-\d{4}$")]
    public void 電話番号は国内形式(PhoneType type, string pattern)
    {
        var gen = new PhoneGenerator(type);
        for (int i = 0; i < 500; i++) Assert.Matches(pattern, gen.Generate(Rnd, i));
    }

    [Fact]
    public void 郵便番号はNNN_NNNN形式()
    {
        var gen = new PostalCodeGenerator();
        for (int i = 0; i < 500; i++) Assert.Matches(@"^\d{3}-\d{4}$", gen.Generate(Rnd, i));
        Assert.Equal("000-0000", gen.GetValue(0));
        Assert.Equal("999-9999", gen.GetValue(gen.DomainSize - 1));
    }

    [Fact]
    public void 住所は都道府県から始まる()
    {
        var gen = new AddressGenerator();
        var regex = new Regex(@"^(北海道|東京都|大阪府|京都府|.{2,3}県).+\d丁目\d+-\d+$");
        for (int i = 0; i < 500; i++) Assert.Matches(regex, gen.Generate(Rnd, i));
        Assert.Matches(regex, gen.GetValue(gen.DomainSize - 1));
    }

    [Fact]
    public void 固定値は常に同じ値()
    {
        var gen = new FixedGenerator("ACTIVE");
        Assert.All(Enumerable.Range(0, 100), i => Assert.Equal("ACTIVE", gen.Generate(Rnd, i)));
        Assert.Equal(1, gen.DomainSize);
    }

    [Fact]
    public void すべてのデータ型のGeneratorを作成できる()
    {
        foreach (DataType type in Enum.GetValues<DataType>())
        {
            var field = FieldDefinition.Create("X", type);
            if (type == DataType.Fixed) field.FixedValue = "A";
            var gen = GeneratorFactory.Create(field);
            Assert.NotNull(gen.Generate(Rnd, 0));
        }
    }
}
