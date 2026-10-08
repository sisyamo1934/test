using System.Globalization;

namespace TestDataMaker.Generators;

/// <summary>
/// 開始日～終了日（両端を含む）の日付を一様に生成する。
/// 日付形式に時刻（H/h/m/s）が含まれる場合は秒単位の日時を生成する。
/// </summary>
public sealed class DateGenerator : ValueGenerator
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ja-JP");

    private readonly DateTime _start;
    private readonly long _count;
    private readonly bool _includesTime;
    private readonly string _format;

    public DateGenerator(DateTime startDate, DateTime endDate, string format)
    {
        _start = startDate.Date;
        var end = endDate.Date;
        if (_start > end) throw new ArgumentException("開始日は終了日以前に設定してください。");
        _format = string.IsNullOrWhiteSpace(format) ? "yyyy/MM/dd" : format;
        _includesTime = ContainsTime(_format);
        long days = (long)(end - _start).TotalDays + 1;
        _count = _includesTime ? days * 86400 : days;
    }

    public static bool ContainsTime(string format) => format.IndexOfAny(new[] { 'H', 'h', 'm', 's', 'f' }) >= 0;

    /// <summary>日付形式として使えるか確認する。</summary>
    public static bool IsValidFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format)) return false;
        try
        {
            _ = new DateTime(2000, 12, 31, 23, 59, 59).ToString(format, Culture);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public override long DomainSize => _count;

    public override string Generate(Random random, long rowIndex) => GetValue(random.NextInt64(_count));

    public override string GetValue(long index) => ToDateTime(index).ToString(_format, Culture);

    internal DateTime ToDateTime(long index) => _includesTime ? _start.AddSeconds(index) : _start.AddDays(index);
}
