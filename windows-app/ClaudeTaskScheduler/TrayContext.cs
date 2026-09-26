using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ClaudeTaskScheduler;

/// <summary>
/// Keeps the process alive as a background tray app. There is no main window;
/// the dashboard is opened in the system browser instead.
/// </summary>
public sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly AppHost _host;

    public TrayContext(AppHost host)
    {
        _host = host;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open dashboard", null, (_, _) => OpenDashboard());
        menu.Items.Add("Copy API key", null, (_, _) => CopyApiKey());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Claude Task Scheduler",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _notifyIcon.DoubleClick += (_, _) => OpenDashboard();

        _notifyIcon.ShowBalloonTip(3000, "Claude Task Scheduler",
            $"Running at {_host.BaseUrl}\nRight-click the tray icon to copy the API key.",
            ToolTipIcon.Info);
    }

    private void OpenDashboard()
    {
        Process.Start(new ProcessStartInfo(_host.BaseUrl) { UseShellExecute = true });
    }

    private void CopyApiKey()
    {
        Clipboard.SetText(_host.ApiKey);
        _notifyIcon.ShowBalloonTip(2000, "Claude Task Scheduler", "API key copied to clipboard.", ToolTipIcon.Info);
    }

    private void Exit()
    {
        _notifyIcon.Visible = false;
        ExitThread();
    }
}
