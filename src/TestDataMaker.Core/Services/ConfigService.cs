using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using TestDataMaker.Models;

namespace TestDataMaker.Services;

/// <summary>データ定義を JSON 設定ファイルとして保存・読み込みする。</summary>
public sealed class ConfigService
{
    public const string FileFilter = "TestDataMaker 設定ファイル (*.json)|*.json";
    public const string LoadErrorMessage = "設定ファイルを読み込めませんでした。\nTestDataMakerで保存したJSONファイルか確認してください。";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // 日本語をエスケープせず、人が読める形で保存する
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Serialize(TestDataDefinition definition) => JsonSerializer.Serialize(definition, Options);

    public TestDataDefinition Deserialize(string json)
    {
        TestDataDefinition? def;
        try
        {
            def = JsonSerializer.Deserialize<TestDataDefinition>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new TestDataException(LoadErrorMessage, ex);
        }
        if (def is null || def.Fields is null) throw new TestDataException(LoadErrorMessage);
        if (def.FormatVersion > TestDataDefinition.CurrentFormatVersion)
            throw new TestDataException("新しいバージョンのTestDataMakerで作成された設定ファイルのため読み込めません。");

        def.Fields.RemoveAll(f => f is null);
        foreach (var f in def.Fields)
        {
            f.Name ??= "";
            f.CustomChars ??= "";
            f.Prefix ??= "";
            f.DateFormat ??= "yyyy/MM/dd";
            f.EmailDomain ??= "example.com";
            f.FixedValue ??= "";
            if (!Enum.IsDefined(f.DataType)) throw new TestDataException(LoadErrorMessage + $"\n（不明なデータ型：{f.DataType}）");
        }
        return def;
    }

    public void Save(string path, TestDataDefinition definition)
    {
        SafeFileWriter.Write(path, temp => File.WriteAllText(temp, Serialize(definition), new UTF8Encoding(false)));
    }

    public TestDataDefinition Load(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (FileNotFoundException ex)
        {
            throw new TestDataException("ファイルが見つかりません。\n" + path, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TestDataException(LoadErrorMessage + "\n（ファイルを開けません）", ex);
        }
        return Deserialize(json);
    }
}
