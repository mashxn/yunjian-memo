using System.Threading;
using System.Windows;

namespace YunJianMemo;

public partial class App : Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\YunJianMemo.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "云笺已经在运行中。\n\n如果桌面上没有看到它，请查看屏幕右下角的系统托盘图标。",
                "云笺 · 桌面备忘录",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Store.Log("未处理的界面异常。", args.Exception);
            System.Windows.MessageBox.Show(
                "云笺遇到一个问题，已记录到日志。\n\n" + args.Exception.Message,
                "云笺 · 桌面备忘录", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            Store.Log("启动失败。", ex);
            System.Windows.MessageBox.Show(
                "云笺启动失败：\n" + ex.Message,
                "云笺 · 桌面备忘录", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _singleInstance?.ReleaseMutex();
        }
        catch { /* ignore */ }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
