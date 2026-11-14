using System.Windows;
using AiPriceWatch.App.Services;
using Velopack;

namespace AiPriceWatch.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!AppEnvironment.IsPackagedStoreApp())
        {
            VelopackApp.Build().Run();
        }

        base.OnStartup(e);

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
