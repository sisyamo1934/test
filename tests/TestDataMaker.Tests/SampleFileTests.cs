using TestDataMaker.Models;
using TestDataMaker.Services;

namespace TestDataMaker.Tests;

/// <summary>配布物に同梱するサンプルファイルが読み込めることを確認する。</summary>
public class SampleFileTests
{
    private static string SampleDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TestDataMaker.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "sample");
    }

    [Fact]
    public void サンプルCSVを読み込んで型を推測できる()
    {
        var csv = new CsvService().ReadHeader(Path.Combine(SampleDir(), "sample.csv"));
        Assert.Equal(12, csv.Headers.Length);
        var fields = FieldTypeInferrer.Infer(csv);
        Assert.Equal(DataType.Sequence, fields[0].DataType);
        Assert.Equal(DataType.JapaneseName, fields[1].DataType);
        Assert.Equal(NameFormat.FullNameKana, fields[2].NameFormat);
        Assert.Equal(DataType.Email, fields[5].DataType);
        Assert.True(new ValidationService().Validate(fields, 1000).IsValid);
    }

    [Fact]
    public void サンプル設定を読み込んで生成できる()
    {
        var def = new ConfigService().Load(Path.Combine(SampleDir(), "sample.json"));
        Assert.Equal(13, def.Fields.Count);
        var validation = new ValidationService().Validate(def.Fields, def.RecordCount);
        Assert.True(validation.IsValid, validation.ToString());
        var rows = new DataGenerationService().GenerateRows(def.Fields, def.RecordCount).ToList();
        Assert.Equal(def.RecordCount, rows.Count);
        Assert.Equal("1000", rows[0][0]);
        Assert.StartsWith("M-", rows[0][11]);
    }
}
