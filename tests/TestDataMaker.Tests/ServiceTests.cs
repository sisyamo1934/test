using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using TestDataMaker.Models;
using TestDataMaker.Services;

namespace TestDataMaker.Tests;

/// <summary>テストごとに一時フォルダを作り、終了時に削除する。</summary>
public abstract class TempDirTest : IDisposable
{
    protected string Dir { get; } = Path.Combine(Path.GetTempPath(), "TestDataMakerTests_" + Guid.NewGuid().ToString("N"));

    protected TempDirTest() => Directory.CreateDirectory(Dir);

    public void Dispose()
    {
        try { Directory.Delete(Dir, true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }
}

public class CsvServiceTests : TempDirTest
{
    private readonly CsvService _csv = new();

    [Fact]
    public void CSVの列名を読み込める()
    {
        string path = Path.Combine(Dir, "a.csv");
        File.WriteAllText(path, "ID,NAME,AGE,BIRTHDAY,STATUS\r\n1,山田太郎,35,1991/03/12,ACTIVE\r\n", new UTF8Encoding(false));
        var result = _csv.ReadHeader(path);
        Assert.Equal(new[] { "ID", "NAME", "AGE", "BIRTHDAY", "STATUS" }, result.Headers);
        Assert.Single(result.SampleRows);
    }

    [Fact]
    public void UTF8のBOM付きCSVを読み込める()
    {
        string path = Path.Combine(Dir, "bom.csv");
        File.WriteAllText(path, "顧客ID,氏名,住所\r\n", new UTF8Encoding(true));
        var result = _csv.ReadHeader(path);
        Assert.Equal(new[] { "顧客ID", "氏名", "住所" }, result.Headers);
        Assert.Equal(Encoding.UTF8.WebName, result.Encoding.WebName);
    }

    [Fact]
    public void UTF8のBOMなしCSVを読み込める()
    {
        string path = Path.Combine(Dir, "nobom.csv");
        File.WriteAllText(path, "顧客ID,氏名\n", new UTF8Encoding(false));
        var result = _csv.ReadHeader(path);
        Assert.Equal(new[] { "顧客ID", "氏名" }, result.Headers);
        Assert.Equal(Encoding.UTF8.WebName, result.Encoding.WebName);
    }

    [Fact]
    public void ShiftJISのCSVを読み込める()
    {
        string path = Path.Combine(Dir, "sjis.csv");
        File.WriteAllBytes(path, CsvService.ShiftJis.GetBytes("顧客ID,氏名,生年月日,電話番号\r\n1,佐藤花子,1984/05/02,090-1234-5678\r\n"));
        var result = _csv.ReadHeader(path);
        Assert.Equal(new[] { "顧客ID", "氏名", "生年月日", "電話番号" }, result.Headers);
        Assert.Equal(932, result.Encoding.CodePage);
    }

    [Fact]
    public void クォートされた列名を読み込める()
    {
        string path = Path.Combine(Dir, "q.csv");
        File.WriteAllText(path, "\"ID\",\"NAME, FULL\",\"MEMO \"\"x\"\"\"\r\n");
        Assert.Equal(new[] { "ID", "NAME, FULL", "MEMO \"x\"" }, _csv.ReadHeader(path).Headers);
    }

    [Fact]
    public void 空のCSVはエラー()
    {
        string path = Path.Combine(Dir, "empty.csv");
        File.WriteAllBytes(path, Array.Empty<byte>());
        var ex = Assert.Throws<TestDataException>(() => _csv.ReadHeader(path));
        Assert.StartsWith("CSVファイルを読み込めませんでした。", ex.Message);
    }

    [Fact]
    public void CSV以外の拡張子はエラー()
    {
        string path = Path.Combine(Dir, "a.txt");
        File.WriteAllText(path, "A,B");
        Assert.Throws<TestDataException>(() => _csv.ReadHeader(path));
    }

    [Fact]
    public void バイナリファイルはエラー()
    {
        string path = Path.Combine(Dir, "bin.csv");
        File.WriteAllBytes(path, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0xFF, 0x00 });
        Assert.Throws<TestDataException>(() => _csv.ReadHeader(path));
    }

    [Fact]
    public void CSVを出力できる()
    {
        string path = Path.Combine(Dir, "out.csv");
        var fields = new[] { FieldDefinition.Create("ID", DataType.Sequence), FieldDefinition.Create("MEMO", DataType.String) };
        var rows = new[] { new string?[] { "1", "a,b" }, new string?[] { "2", null }, new string?[] { "3", "say \"hi\"" } };
        _csv.Export(path, fields, rows);

        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        string text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.Equal("ID,MEMO\r\n1,\"a,b\"\r\n2,\r\n3,\"say \"\"hi\"\"\"\r\n", text);
        Assert.Empty(Directory.GetFiles(Dir, "*.tmp"));
    }

    [Fact]
    public void BOMなしでCSVを出力できる()
    {
        string path = Path.Combine(Dir, "nobom_out.csv");
        new CsvService { WriteBom = false }.Export(path, new[] { FieldDefinition.Create("名前", DataType.String) }, Array.Empty<string?[]>());
        Assert.Equal("名前\r\n", File.ReadAllText(path, new UTF8Encoding(false)));
        Assert.NotEqual(0xEF, File.ReadAllBytes(path)[0]);
    }

    [Fact]
    public void 出力したCSVを再度読み込める()
    {
        string path = Path.Combine(Dir, "roundtrip.csv");
        var fields = new[] { FieldDefinition.Create("ID", DataType.Sequence), FieldDefinition.Create("氏名", DataType.JapaneseName) };
        var rows = new DataGenerationService().GenerateRows(fields, 10);
        _csv.Export(path, fields, rows);
        var read = _csv.ReadHeader(path);
        Assert.Equal(new[] { "ID", "氏名" }, read.Headers);
        Assert.Equal(10, read.SampleRows.Count);
    }

    [Fact]
    public void キャンセルすると出力ファイルが残らない()
    {
        string path = Path.Combine(Dir, "cancel.csv");
        var fields = new[] { FieldDefinition.Create("ID", DataType.Sequence) };
        using var cts = new CancellationTokenSource();
        var rows = new DataGenerationService().GenerateRows(fields, 100_000).Select((r, i) =>
        {
            if (i == 5000) cts.Cancel();
            return r;
        });
        Assert.ThrowsAny<OperationCanceledException>(() => _csv.Export(path, fields, rows, null, cts.Token));
        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(Dir));
    }
}

public class ExcelServiceTests : TempDirTest
{
    [Fact]
    public void Excelファイルを出力できる()
    {
        string path = Path.Combine(Dir, "out.xlsx");
        var fields = new[]
        {
            FieldDefinition.Create("ID", DataType.Sequence),
            FieldDefinition.Create("NAME", DataType.JapaneseName),
            FieldDefinition.Create("PRICE", DataType.Number),
        };
        var rows = new[]
        {
            new string?[] { "1", "山田 太郎", "12.5" },
            new string?[] { "2", null, "0" },
        };
        new ExcelService().Export(path, fields, rows);

        using var doc = SpreadsheetDocument.Open(path, false);
        var wb = doc.WorkbookPart!;
        var sheet = wb.Workbook!.Sheets!.Elements<Sheet>().Single();
        Assert.Equal("TestData", sheet.Name!.Value);

        var data = ((WorksheetPart)wb.GetPartById(sheet.Id!.Value!)).Worksheet!.GetFirstChild<SheetData>()!;
        var r = data.Elements<Row>().ToList();
        Assert.Equal(3, r.Count);
        Assert.Equal(new[] { "ID", "NAME", "PRICE" }, r[0].Elements<Cell>().Select(c => c.InnerText));
        var row1 = r[1].Elements<Cell>().ToList();
        Assert.Equal(CellValues.Number, row1[0].DataType!.Value);
        Assert.Equal("1", row1[0].InnerText);
        Assert.Equal("山田 太郎", row1[1].InnerText);
        Assert.Equal("12.5", row1[2].InnerText);
        Assert.Equal("", r[2].Elements<Cell>().ElementAt(1).InnerText);
    }
}

public class ConfigServiceTests : TempDirTest
{
    private readonly ConfigService _config = new();

    private static TestDataDefinition Sample()
    {
        var name = FieldDefinition.Create("NAME", DataType.JapaneseName);
        name.NullRate = 5;
        var age = FieldDefinition.Create("AGE", DataType.Number);
        age.MinValue = 18;
        age.MaxValue = 80;
        age.DecimalPlaces = 1;
        var birthday = FieldDefinition.Create("BIRTHDAY", DataType.Date);
        birthday.StartDate = new DateTime(1970, 1, 1);
        birthday.EndDate = new DateTime(2000, 12, 31);
        birthday.DateFormat = "yyyyMMdd";
        var status = FieldDefinition.Create("STATUS", DataType.Fixed);
        status.FixedValue = "ACTIVE";
        var code = FieldDefinition.Create("CODE", DataType.String);
        code.Unique = true;
        code.CharSet = StringCharSet.Custom;
        code.CustomChars = "XYZ";
        code.MinLength = 3;
        code.MaxLength = 5;
        return new TestDataDefinition
        {
            RecordCount = 12345,
            Fields = { FieldDefinition.Create("ID", DataType.Sequence), name, age, birthday, status, code },
        };
    }

    [Fact]
    public void JSONを保存できる()
    {
        string path = Path.Combine(Dir, "customer_testdata.json");
        _config.Save(path, Sample());
        string json = File.ReadAllText(path);
        Assert.Contains("\"dataType\": \"JapaneseName\"", json);
        Assert.Contains("\"fixedValue\": \"ACTIVE\"", json);
        Assert.Contains("\"recordCount\": 12345", json);
    }

    [Fact]
    public void JSONを読み込める()
    {
        string path = Path.Combine(Dir, "customer_testdata.json");
        var original = Sample();
        _config.Save(path, original);
        var loaded = _config.Load(path);

        Assert.Equal(original.RecordCount, loaded.RecordCount);
        Assert.Equal(original.Fields.Count, loaded.Fields.Count);
        for (int i = 0; i < original.Fields.Count; i++)
        {
            var a = original.Fields[i];
            var b = loaded.Fields[i];
            Assert.Equal(a.Name, b.Name);
            Assert.Equal(a.DataType, b.DataType);
            Assert.Equal(a.NullRate, b.NullRate);
            Assert.Equal(a.Unique, b.Unique);
            Assert.Equal(a.MinValue, b.MinValue);
            Assert.Equal(a.MaxValue, b.MaxValue);
            Assert.Equal(a.DecimalPlaces, b.DecimalPlaces);
            Assert.Equal(a.StartDate, b.StartDate);
            Assert.Equal(a.EndDate, b.EndDate);
            Assert.Equal(a.DateFormat, b.DateFormat);
            Assert.Equal(a.FixedValue, b.FixedValue);
            Assert.Equal(a.CharSet, b.CharSet);
            Assert.Equal(a.CustomChars, b.CustomChars);
            Assert.Equal(a.MinLength, b.MinLength);
            Assert.Equal(a.MaxLength, b.MaxLength);
        }
    }

    [Fact]
    public void 不正なJSONはエラー()
    {
        string path = Path.Combine(Dir, "bad.json");
        File.WriteAllText(path, "{ this is not json");
        var ex = Assert.Throws<TestDataException>(() => _config.Load(path));
        Assert.StartsWith("設定ファイルを読み込めませんでした。", ex.Message);
    }

    [Fact]
    public void 不明なデータ型はエラー()
    {
        Assert.Throws<TestDataException>(() => _config.Deserialize("{\"fields\":[{\"name\":\"A\",\"dataType\":\"Unknown\"}]}"));
    }

    [Fact]
    public void 省略された項目は既定値になる()
    {
        var def = _config.Deserialize("{\"fields\":[{\"name\":\"A\",\"dataType\":\"Email\"}]}");
        Assert.Equal("example.com", def.Fields[0].EmailDomain);
        Assert.Equal(1000, def.RecordCount);
    }
}

public class ValidationServiceTests
{
    private readonly ValidationService _validation = new();

    private static List<FieldDefinition> Fields(params FieldDefinition[] fields) => fields.ToList();

    [Fact]
    public void 正しい定義はエラーなし()
    {
        var result = _validation.Validate(Fields(FieldDefinition.Create("ID", DataType.Sequence), FieldDefinition.Create("NAME", DataType.JapaneseName)), 1000);
        Assert.True(result.IsValid, result.ToString());
    }

    [Fact]
    public void 最小値が最大値より大きい場合はエラー()
    {
        var f = FieldDefinition.Create("AGE", DataType.Number);
        f.MinValue = 80;
        f.MaxValue = 18;
        var result = _validation.Validate(Fields(f), 10);
        Assert.Contains(result.Errors, e => e.Contains("最小値は最大値以下に設定してください。"));
    }

    [Fact]
    public void 開始日が終了日より後の場合はエラー()
    {
        var f = FieldDefinition.Create("D", DataType.Date);
        f.StartDate = new DateTime(2026, 2, 1);
        f.EndDate = new DateTime(2026, 1, 1);
        Assert.Contains(_validation.Validate(Fields(f), 10).Errors, e => e.Contains("開始日は終了日以前"));
    }

    [Fact]
    public void 文字数の範囲が不正な場合はエラー()
    {
        var f = FieldDefinition.Create("S", DataType.String);
        f.MinLength = 10;
        f.MaxLength = 5;
        Assert.Contains(_validation.Validate(Fields(f), 10).Errors, e => e.Contains("最小文字数は最大文字数以下"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void 生成件数が0以下はエラー(long count)
    {
        var result = _validation.Validate(Fields(FieldDefinition.Create("ID", DataType.Sequence)), count);
        Assert.Contains("生成件数は1以上にしてください。", result.Errors);
    }

    [Fact]
    public void 生成件数が上限超過はエラー()
    {
        var result = _validation.Validate(Fields(FieldDefinition.Create("ID", DataType.Sequence)), 1_000_001);
        Assert.Contains(result.Errors, e => e.StartsWith("生成件数は1,000,000以下"));
    }

    [Theory]
    [InlineData("0", false, "生成件数は1以上にしてください。")]
    [InlineData("", false, "生成件数を入力してください。")]
    [InlineData("abc", false, "生成件数は半角数字で入力してください。")]
    [InlineData("1000001", false, "生成件数は1,000,000以下にしてください。")]
    [InlineData("10,000", true, "")]
    [InlineData(" 1 ", true, "")]
    public void 生成件数の入力を検証できる(string text, bool ok, string message)
    {
        Assert.Equal(ok, ValidationService.TryParseRecordCount(text, out _, out string error));
        Assert.Equal(message, error);
    }

    [Fact]
    public void 項目がない場合はエラー()
    {
        Assert.Contains("項目を1つ以上定義してください。", _validation.Validate(new List<FieldDefinition>(), 10).Errors);
    }

    [Fact]
    public void 項目名が空の場合はエラー()
    {
        var result = _validation.Validate(Fields(FieldDefinition.Create(" ", DataType.String)), 10);
        Assert.Contains("1行目：項目名を入力してください。", result.Errors);
    }

    [Fact]
    public void 項目名が重複する場合はエラー()
    {
        var result = _validation.Validate(Fields(FieldDefinition.Create("ID", DataType.Sequence), FieldDefinition.Create("id", DataType.String)), 10);
        Assert.Contains(result.Errors, e => e.Contains("項目名が重複しています"));
    }

    [Fact]
    public void NULL率が範囲外の場合はエラー()
    {
        var f = FieldDefinition.Create("A", DataType.String);
        f.NullRate = 120;
        Assert.Contains(_validation.Validate(Fields(f), 10).Errors, e => e.Contains("NULL率は0～100%"));
    }

    [Fact]
    public void 重複禁止で値が不足する場合はエラー()
    {
        var f = FieldDefinition.Create("AGE", DataType.Number);
        f.MinValue = 18;
        f.MaxValue = 80;
        f.Unique = true;
        var result = _validation.Validate(Fields(f), 10000);
        Assert.Contains(result.Errors, e => e.Contains("指定された条件では10000件のユニークデータを生成できません。"));
    }

    [Fact]
    public void 重複禁止でも連番はエラーにならない()
    {
        var f = FieldDefinition.Create("ID", DataType.Sequence);
        f.Unique = true;
        Assert.True(_validation.Validate(Fields(f), 1_000_000).IsValid);
    }

    [Fact]
    public void 増分0はエラー()
    {
        var f = FieldDefinition.Create("ID", DataType.Sequence);
        f.Increment = 0;
        Assert.Contains(_validation.Validate(Fields(f), 10).Errors, e => e.Contains("増分は0以外"));
    }

    [Fact]
    public void 不正なドメインはエラー()
    {
        var f = FieldDefinition.Create("MAIL", DataType.Email);
        f.EmailDomain = "example";
        Assert.Contains(_validation.Validate(Fields(f), 10).Errors, e => e.Contains("ドメインが正しくありません"));
    }

    [Fact]
    public void 不正な日付形式はエラー()
    {
        var f = FieldDefinition.Create("D", DataType.Date);
        f.DateFormat = "%";
        Assert.Contains(_validation.Validate(Fields(f), 10).Errors, e => e.Contains("日付形式が正しくありません"));
    }
}

public class DataGenerationServiceTests
{
    private readonly DataGenerationService _service = new();

    [Fact]
    public void 指定件数の行を生成できる()
    {
        var fields = new[] { FieldDefinition.Create("ID", DataType.Sequence), FieldDefinition.Create("NAME", DataType.JapaneseName) };
        var rows = _service.GenerateRows(fields, 1000).ToList();
        Assert.Equal(1000, rows.Count);
        Assert.All(rows, r => Assert.Equal(2, r.Length));
        Assert.Equal("1", rows[0][0]);
        Assert.Equal("1000", rows[999][0]);
    }

    [Fact]
    public void 重複禁止なら値が重複しない()
    {
        var f = FieldDefinition.Create("CODE", DataType.Number);
        f.MinValue = 1;
        f.MaxValue = 10000;
        f.Unique = true;
        var values = _service.GenerateRows(new[] { f }, 10000).Select(r => r[0]).ToList();
        Assert.Equal(10000, values.Distinct().Count());
    }

    [Fact]
    public void 重複禁止で値が尽きたらエラー()
    {
        var f = FieldDefinition.Create("CODE", DataType.Number);
        f.MinValue = 1;
        f.MaxValue = 5;
        f.Unique = true;
        var ex = Assert.Throws<TestDataException>(() => _service.GenerateRows(new[] { f }, 6).ToList());
        Assert.Contains("6件のユニークデータを生成できません", ex.Message);
    }

    [Fact]
    public void 重複禁止でもNULLは複数許可される()
    {
        var f = FieldDefinition.Create("CODE", DataType.Number);
        f.MinValue = 1;
        f.MaxValue = 100;
        f.Unique = true;
        f.NullRate = 50;
        var values = _service.GenerateRows(new[] { f }, 150, new GenerationOptions { Seed = 1 }).Select(r => r[0]).ToList();
        var nonNull = values.Where(v => v is not null).ToList();
        Assert.Equal(nonNull.Count, nonNull.Distinct().Count());
        Assert.True(values.Count(v => v is null) > 1);
    }

    [Fact]
    public void 重複許可なら重複しうる()
    {
        var f = FieldDefinition.Create("FLAG", DataType.Number);
        f.MinValue = 0;
        f.MaxValue = 1;
        var values = _service.GenerateRows(new[] { f }, 100).Select(r => r[0]).ToList();
        Assert.True(values.Distinct().Count() <= 2);
    }

    [Fact]
    public void 同じSeedなら同じデータになる()
    {
        var fields = new[] { FieldDefinition.Create("NAME", DataType.JapaneseName), FieldDefinition.Create("S", DataType.String) };
        var a = _service.GenerateRows(fields, 100, new GenerationOptions { Seed = 42 }).SelectMany(r => r).ToList();
        var b = _service.GenerateRows(fields, 100, new GenerationOptions { Seed = 42 }).SelectMany(r => r).ToList();
        Assert.Equal(a, b);
    }

    [Fact]
    public void Seed未指定なら毎回異なるデータになる()
    {
        var fields = new[] { FieldDefinition.Create("S", DataType.String) };
        var a = _service.GenerateRows(fields, 100).SelectMany(r => r).ToList();
        var b = _service.GenerateRows(fields, 100).SelectMany(r => r).ToList();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void キャンセルできる()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            _service.GenerateRows(new[] { FieldDefinition.Create("ID", DataType.Sequence) }, 10_000, null, cts.Token).ToList());
    }

    [Fact]
    public void 十万件を生成できる()
    {
        var fields = Enum.GetValues<DataType>().Select(t => FieldDefinition.Create(t.ToString(), t)).ToArray();
        long n = _service.GenerateRows(fields, 100_000).LongCount();
        Assert.Equal(100_000, n);
    }
}

public class FieldTypeInferrerTests
{
    [Theory]
    [InlineData("ID", DataType.Sequence)]
    [InlineData("NAME", DataType.JapaneseName)]
    [InlineData("氏名", DataType.JapaneseName)]
    [InlineData("AGE", DataType.Number)]
    [InlineData("BIRTHDAY", DataType.Date)]
    [InlineData("EMAIL", DataType.Email)]
    [InlineData("TEL", DataType.Phone)]
    [InlineData("ZIP_CODE", DataType.PostalCode)]
    [InlineData("ADDRESS", DataType.Address)]
    [InlineData("STATUS", DataType.Fixed)]
    [InlineData("REMARKS", DataType.String)]
    public void 列名からデータ型を推測できる(string name, DataType expected)
    {
        Assert.Equal(expected, FieldTypeInferrer.Infer(name, Array.Empty<string>(), isFirstColumn: name == "ID").DataType);
    }

    [Fact]
    public void サンプル値から数値を推測できる()
    {
        var f = FieldTypeInferrer.Infer("SCORE", new[] { "12.5", "80.25" });
        Assert.Equal(DataType.Number, f.DataType);
        Assert.Equal(2, f.DecimalPlaces);
    }
}
