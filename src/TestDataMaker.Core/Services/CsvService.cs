using System.Text;
using TestDataMaker.Models;

namespace TestDataMaker.Services;

public sealed class CsvReadResult
{
    public required string[] Headers { get; init; }

    /// <summary>先頭数行のデータ（型推定用）。</summary>
    public required List<string[]> SampleRows { get; init; }

    public required Encoding Encoding { get; init; }
}

/// <summary>CSV の読み込み（列名取得）と書き出し。</summary>
public sealed class CsvService : IDataExporter
{
    public const string ReadErrorMessage = "CSVファイルを読み込めませんでした。\n文字コードまたはファイル形式を確認してください。";

    /// <summary>列名取得のために読む最大バイト数。巨大な CSV でも先頭だけ読む。</summary>
    private const int MaxReadBytes = 1024 * 1024;
    private const int SampleRowCount = 20;

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);

    static CsvService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Encoding ShiftJis => Encoding.GetEncoding(932);

    /// <summary>出力時に BOM を付けるか（Excel で文字化けしないよう既定は true）。</summary>
    public bool WriteBom { get; set; } = true;

    public string DisplayName => "CSV";
    public string FileFilter => "CSV ファイル (*.csv)|*.csv";
    public string DefaultExtension => "csv";

    // ---- 読み込み ----

    public CsvReadResult ReadHeader(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
            throw new TestDataException("CSVファイル（拡張子 .csv）を指定してください。");

        byte[] bytes;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            int length = (int)Math.Min(fs.Length, MaxReadBytes);
            bytes = new byte[length];
            fs.ReadExactly(bytes, 0, length);
            if (fs.Length > MaxReadBytes) bytes = TrimToLastLine(bytes);
        }
        catch (FileNotFoundException ex)
        {
            throw new TestDataException("ファイルが見つかりません。\n" + path, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TestDataException(ReadErrorMessage + "\n（ファイルを開けません。他のアプリで使用中の可能性があります）", ex);
        }

        return Parse(bytes);
    }

    internal static CsvReadResult Parse(byte[] bytes)
    {
        if (bytes.Length == 0) throw new TestDataException(ReadErrorMessage + "\n（ファイルが空です）");

        var (encoding, text) = Decode(bytes);
        if (text.Contains('\0')) throw new TestDataException(ReadErrorMessage + "\n（テキスト形式のCSVではありません）");

        var records = ParseRecords(text).Take(SampleRowCount + 1).ToList();
        if (records.Count == 0 || records[0].All(string.IsNullOrWhiteSpace))
            throw new TestDataException(ReadErrorMessage + "\n（1行目に列名がありません）");

        string[] headers = records[0].Select((h, i) => string.IsNullOrWhiteSpace(h) ? $"COLUMN{i + 1}" : h.Trim()).ToArray();
        return new CsvReadResult
        {
            Headers = headers,
            SampleRows = records.Skip(1).Where(r => !(r.Length == 1 && r[0].Length == 0)).ToList(),
            Encoding = encoding,
        };
    }

    /// <summary>BOM・UTF-8 として正しいかで文字コードを判定する。UTF-8 でなければ Shift-JIS とみなす。</summary>
    internal static (Encoding, string) Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return (Encoding.UTF8, Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return (Encoding.Unicode, Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return (Encoding.BigEndianUnicode, Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2));
        try
        {
            return (Encoding.UTF8, StrictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            return (ShiftJis, ShiftJis.GetString(bytes));
        }
    }

    /// <summary>途中で切った場合に文字が分断されないよう、最後の改行までにする。</summary>
    private static byte[] TrimToLastLine(byte[] bytes)
    {
        int last = Array.LastIndexOf(bytes, (byte)'\n');
        return last > 0 ? bytes[..(last + 1)] : bytes;
    }

    /// <summary>RFC 4180 形式（ダブルクォート・改行を含む値に対応）でレコードに分割する。</summary>
    internal static IEnumerable<string[]> ParseRecords(string text)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        bool any = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            any = true;
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                fields.Add(sb.ToString());
                sb.Clear();
                yield return fields.ToArray();
                fields.Clear();
                any = false;
            }
            else
            {
                sb.Append(c);
            }
        }
        if (any)
        {
            fields.Add(sb.ToString());
            yield return fields.ToArray();
        }
    }

    // ---- 書き出し ----

    public void Export(
        string path,
        IReadOnlyList<FieldDefinition> fields,
        IEnumerable<string?[]> rows,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        SafeFileWriter.Write(path, temp =>
        {
            using var writer = new StreamWriter(temp, false, new UTF8Encoding(WriteBom), 1 << 16);
            Write(writer, fields.Select(f => f.Name).ToArray(), rows, progress, cancellationToken);
        });
    }

    public static void Write(
        TextWriter writer,
        IReadOnlyList<string> headers,
        IEnumerable<string?[]> rows,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        writer.NewLine = "\r\n";
        WriteRecord(writer, headers);
        long n = 0;
        foreach (var row in rows)
        {
            WriteRecord(writer, row);
            n++;
            if ((n & 0x3FF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(n);
            }
        }
        progress?.Report(n);
    }

    private static void WriteRecord(TextWriter writer, IReadOnlyList<string?> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) writer.Write(',');
            writer.Write(Escape(values[i]));
        }
        writer.WriteLine();
    }

    /// <summary>カンマ・ダブルクォート・改行を含む値をクォートする。NULL は空欄。</summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
