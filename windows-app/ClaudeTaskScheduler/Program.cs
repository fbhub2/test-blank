using System.Windows.Forms;

namespace ClaudeTaskScheduler;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var host = new AppHost();
        host.Start();

        using var trayContext = new TrayContext(host);
        Application.Run(trayContext);

        host.StopAsync().GetAwaiter().GetResult();
    }
}
