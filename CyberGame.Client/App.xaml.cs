using System.Net.Http;
using System.Windows;
using CyberGame.Client.Services;
using CyberGame.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CyberGame.Client;

public partial class App : System.Windows.Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);

        ServiceProvider = services.BuildServiceProvider();

        var mainWindow = new MainWindow
        {
            DataContext = ServiceProvider.GetRequiredService<MainViewModel>()
        };

        mainWindow.Show();
    }

    public static void ConfigureServices(IServiceCollection services)
    {
        const string apiClientName = "CyberGameApi";

        services.AddHttpClient(apiClientName, client =>
        {
            client.BaseAddress = new Uri("http://localhost:5227/api/v1/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // Keep one authenticated API session for login and every management screen.
        services.AddSingleton<IApiClient>(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            return new ApiClient(factory.CreateClient(apiClientName));
        });

        services.AddSingleton<IStateService, StateService>();
        services.AddSingleton<MainViewModel>();
    }
}
