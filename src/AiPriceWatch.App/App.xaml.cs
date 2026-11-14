using System.Windows;
using Velopack;

namespace AiPriceWatch.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        VelopackApp.Build().Run();
        base.OnStartup(e);

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
