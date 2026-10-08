using System.Text;

namespace TestDataMaker.Services;

/// <summary>
/// エラーログを %LOCALAPPDATA%\TestDataMaker\logs\ に日付ごとのファイルで記録する。
/// 生成データそのものは記録しないこと。ログ出力の失敗でアプリを止めないよう例外は握りつぶす。
/// </summary>
public static class AppLogger
{
    private static readonly object Lock = new();
    private const int RetentionDays = 30;

    public static string LogDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TestDataMaker", "logs");

    public static string CurrentLogFile => Path.Combine(LogDirectory, $"TestDataMaker_{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(" [").Append(level).Append("] ").AppendLine(message);
            if (ex is not null)
            {
                sb.Append("  種別: ").AppendLine(ex.GetType().FullName);
                sb.Append("  メッセージ: ").AppendLine(ex.Message);
                sb.AppendLine("  スタックトレース:");
                sb.AppendLine(ex.ToString());
            }
            lock (Lock)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(CurrentLogFile, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // ログが書けなくても処理は継続する
        }
    }

    /// <summary>古いログファイルを削除する。</summary>
    public static void CleanupOldLogs()
    {
        try
        {
            if (!Directory.Exists(LogDirectory)) return;
            foreach (var file in Directory.EnumerateFiles(LogDirectory, "TestDataMaker_*.log"))
            {
                if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-RetentionDays)) File.Delete(file);
            }
        }
        catch
        {
        }
    }
}
