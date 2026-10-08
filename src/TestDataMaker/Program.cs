using TestDataMaker.Services;
using TestDataMaker.UI;

namespace TestDataMaker;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // 想定外の例外でもアプリが突然落ちないよう、ログに記録してメッセージを表示する
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => HandleFatal(e.Exception, canContinue: true);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => HandleFatal(e.ExceptionObject as Exception, canContinue: false);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLogger.Error("未処理のタスク例外", e.Exception);
            e.SetObserved();
        };

        ApplicationConfiguration.Initialize();
        AppLogger.CleanupOldLogs();
        AppLogger.Info($"起動 Ver.{typeof(Program).Assembly.GetName().Version?.ToString(3)} OS={Environment.OSVersion}");

        Application.Run(new MainForm());
    }

    private static void HandleFatal(Exception? ex, bool canContinue)
    {
        AppLogger.Error("予期しないエラー", ex);
        try
        {
            string message = ex is TestDataException
                ? ex.Message
                : "予期しないエラーが発生しました。\n" + (ex?.Message ?? "") +
                  "\n\n詳細はログファイルを確認してください。\n" + AppLogger.CurrentLogFile;
            if (!canContinue) message += "\n\nアプリケーションを終了します。";
            MessageBox.Show(message, "エラー - " + MainForm.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch
        {
            // 表示に失敗しても何もしない
        }
    }
}
