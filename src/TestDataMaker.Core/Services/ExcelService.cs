using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using TestDataMaker.Models;

namespace TestDataMaker.Services;

/// <summary>
/// .xlsx 形式で出力する（Excel 未インストールの PC でも動作）。
/// 大量データでもメモリを消費しないよう、Open XML SDK の OpenXmlWriter でストリーミング書き込みする。
/// </summary>
public sealed class ExcelService : IDataExporter
{
    public const string SheetName = "TestData";

    /// <summary>Excel の最大行数（ヘッダー行を含む）。</summary>
    public const int MaxExcelRows = 1_048_576;

    /// <summary>Excel で精度が落ちない整数の桁数。超える場合は文字列として出力する。</summary>
    private const int MaxNumericDigits = 15;

    private const uint HeaderStyleIndex = 1;

    public string DisplayName => "Excel";
    public string FileFilter => "Excel ブック (*.xlsx)|*.xlsx";
    public string DefaultExtension => "xlsx";

    public void Export(
        string path,
        IReadOnlyList<FieldDefinition> fields,
        IEnumerable<string?[]> rows,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        SafeFileWriter.Write(path, temp => WriteWorkbook(temp, fields, rows, progress, cancellationToken));
    }

    private static void WriteWorkbook(
        string path,
        IReadOnlyList<FieldDefinition> fields,
        IEnumerable<string?[]> rows,
        IProgress<long>? progress,
        CancellationToken ct)
    {
        using var doc = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = doc.AddWorkbookPart();
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = CreateStylesheet();
        stylesPart.Stylesheet.Save();

        var sheetPart = workbookPart.AddNewPart<WorksheetPart>();
        bool[] numeric = fields.Select(f => f.DataType is DataType.Number or DataType.Sequence).ToArray();

        using (var writer = OpenXmlWriter.Create(sheetPart))
        {
            writer.WriteStartElement(new Worksheet());

            // 1 行目（ヘッダー）を固定表示
            writer.WriteElement(new SheetViews(
                new SheetView(
                    new Pane { VerticalSplit = 1D, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen },
                    new Selection { Pane = PaneValues.BottomLeft })
                { TabSelected = true, WorkbookViewId = 0U }));

            var columns = new Columns();
            for (int i = 0; i < fields.Count; i++)
            {
                columns.Append(new Column
                {
                    Min = (uint)(i + 1),
                    Max = (uint)(i + 1),
                    Width = ColumnWidth(fields[i]),
                    CustomWidth = true,
                });
            }
            writer.WriteElement(columns);

            writer.WriteStartElement(new SheetData());

            writer.WriteStartElement(new Row());
            foreach (var f in fields) WriteString(writer, f.Name, HeaderStyleIndex);
            writer.WriteEndElement();

            long n = 0;
            foreach (var row in rows)
            {
                if (n + 1 >= MaxExcelRows)
                    throw new TestDataException($"Excelに出力できるのは{MaxExcelRows - 1:N0}件までです。");

                writer.WriteStartElement(new Row());
                for (int i = 0; i < row.Length; i++)
                {
                    string? v = row[i];
                    if (v is null)
                        writer.WriteElement(new Cell());
                    else if (numeric[i] && IsExcelSafeNumber(v))
                        writer.WriteElement(new Cell { DataType = CellValues.Number, CellValue = new CellValue(v) });
                    else
                        WriteString(writer, v, null);
                }
                writer.WriteEndElement();

                n++;
                if ((n & 0x3FF) == 0)
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report(n);
                }
            }
            progress?.Report(n);

            writer.WriteEndElement(); // SheetData
            writer.WriteEndElement(); // Worksheet
        }

        workbookPart.Workbook = new Workbook(
            new Sheets(new Sheet { Name = SheetName, SheetId = 1U, Id = workbookPart.GetIdOfPart(sheetPart) }));
        workbookPart.Workbook.Save();
    }

    private static void WriteString(OpenXmlWriter writer, string value, uint? style)
    {
        var text = new Text(RemoveInvalidXmlChars(value));
        // 前後の空白・改行を保持する必要がある場合のみ指定する（ファイルサイズ削減のため）
        if (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]) || value.Contains('\n')))
            text.Space = SpaceProcessingModeValues.Preserve;
        var cell = new Cell { DataType = CellValues.InlineString, InlineString = new InlineString(text) };
        if (style is uint s) cell.StyleIndex = s;
        writer.WriteElement(cell);
    }

    private static bool IsExcelSafeNumber(string v)
    {
        int digits = v.Count(char.IsAsciiDigit);
        return digits > 0 && digits <= MaxNumericDigits && decimal.TryParse(v, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    /// <summary>XML に書けない制御文字を除く。</summary>
    private static string RemoveInvalidXmlChars(string s)
    {
        foreach (char c in s)
        {
            if (!XmlConvertIsValid(c)) return new string(s.Where(XmlConvertIsValid).ToArray());
        }
        return s;
    }

    private static bool XmlConvertIsValid(char c) => c >= 0x20 || c == '\t' || c == '\n' || c == '\r';

    private static double ColumnWidth(FieldDefinition f)
    {
        double byType = f.DataType switch
        {
            DataType.Address => 40,
            DataType.Email => 28,
            DataType.Date => DateGeneratorWidth(f.DateFormat),
            DataType.JapaneseName => 16,
            DataType.Phone => 15,
            DataType.String => Math.Clamp(f.MaxLength + f.Prefix.Length + 2, 8, 60),
            _ => 10,
        };
        double byHeader = f.Name.Sum(c => c > 0xFF ? 2 : 1) + 2;
        return Math.Max(byType, byHeader);
    }

    private static double DateGeneratorWidth(string format) => Math.Clamp((format?.Length ?? 10) + 4, 12, 30);

    private static Stylesheet CreateStylesheet() => new(
        new Fonts(
            new Font(new FontSize { Val = 11D }, new FontName { Val = "Yu Gothic" }),
            new Font(new Bold(), new FontSize { Val = 11D }, new FontName { Val = "Yu Gothic" })),
        new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 })),
        new Borders(new Border()),
        new CellFormats(
            new CellFormat { FontId = 0, FillId = 0, BorderId = 0 },
            new CellFormat { FontId = 1, FillId = 0, BorderId = 0, ApplyFont = true }));
}
