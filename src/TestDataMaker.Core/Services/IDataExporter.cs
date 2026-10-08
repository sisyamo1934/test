using TestDataMaker.Models;

namespace TestDataMaker.Services;

/// <summary>
/// 生成データのファイル出力。CSV・Excel のほか、将来の SQL INSERT 文（Oracle 等）・JSON・XML 出力も
/// このインターフェースを実装して追加する。
/// </summary>
public interface IDataExporter
{
    /// <summary>画面表示用の名前（例：CSV）。</summary>
    string DisplayName { get; }

    /// <summary>保存ダイアログのフィルター（例：CSV ファイル (*.csv)|*.csv）。</summary>
    string FileFilter { get; }

    /// <summary>既定の拡張子（ドットなし）。</summary>
    string DefaultExtension { get; }

    /// <summary>
    /// rows を path に書き出す。キャンセル・失敗時は出力途中のファイルを残さない。
    /// progress には書き出し済み件数を通知する。
    /// </summary>
    void Export(
        string path,
        IReadOnlyList<FieldDefinition> fields,
        IEnumerable<string?[]> rows,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default);
}

internal static class SafeFileWriter
{
    /// <summary>一時ファイルに書き出し、成功したときだけ目的のファイル名に置き換える。</summary>
    public static void Write(string path, Action<string> writeTo)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        string temp = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            writeTo(temp);
            File.Move(temp, path, overwrite: true);
        }
        catch (IOException ex) when (ex is not FileNotFoundException)
        {
            TryDelete(temp);
            throw new TestDataException(
                "ファイルを保存できませんでした。\n" +
                "ファイルが他のアプリ（Excel など）で開かれていないか、保存先に書き込み権限があるか確認してください。", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            TryDelete(temp);
            throw new TestDataException("保存先に書き込む権限がありません。別のフォルダを選択してください。", ex);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
