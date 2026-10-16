using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using CyberGame.MemberClient.Models;
using CyberGame.MemberClient.Services;
using CyberGame.MemberClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CyberGame.MemberClient;

public partial class App : System.Windows.Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        MemberClientConfig config;
        try
        {
            config = LoadAndValidateConfiguration();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"LỖI CẤU HÌNH NGHIÊM TRỌNG:\n{ex.Message}\n\nỨng dụng dừng khởi động. Vui lòng kiểm tra lại appsettings.json.",
                "Lỗi Cấu Hình CyberGame Member Client",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services, config);

        ServiceProvider = services.BuildServiceProvider();

        var mainWindow = new MainWindow
        {
            DataContext = ServiceProvider.GetRequiredService<MainViewModel>()
        };

        mainWindow.Show();
    }

    public static void ConfigureServices(IServiceCollection services, MemberClientConfig config)
    {
        const string memberApiClientName = "CyberGameMemberApi";

        services.AddSingleton(config);
        services.AddHttpClient(memberApiClientName, client =>
        {
            var baseUri = config.ApiBaseUrl.EndsWith('/') ? config.ApiBaseUrl : config.ApiBaseUrl + "/";
            client.BaseAddress = new Uri(baseUri);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // The login and active-session screens must share the same API instance,
        // because the authenticated member token is held by MemberApiClient.
        services.AddSingleton<IMemberApiClient>(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            return new MemberApiClient(factory.CreateClient(memberApiClientName), config);
        });

        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<ActiveSessionViewModel>();
        services.AddSingleton<MainViewModel>();
    }

    public static MemberClientConfig LoadAndValidateConfiguration(string? basePath = null)
    {
        var dir = basePath ?? AppDomain.CurrentDomain.BaseDirectory;
        var configPath = Path.Combine(dir, "appsettings.json");
        if (!File.Exists(configPath))
        {
            throw new InvalidOperationException($"Không tìm thấy tệp cấu hình '{configPath}'. Vui lòng thiết lập cấu hình máy trạm trước khi khởi động.");
        }

        string json;
        try
        {
            json = File.ReadAllText(configPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Không thể đọc tệp cấu hình '{configPath}': {ex.Message}", ex);
        }

        return ParseAndValidateConfig(json);
    }

    public static MemberClientConfig ParseAndValidateConfig(string json)
    {
        var config = new MemberClientConfig();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("MemberClient", out var section) || section.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Tệp cấu hình appsettings.json thiếu mục 'MemberClient'.");
            }

            if (section.TryGetProperty("ApiBaseUrl", out var urlElem) && urlElem.ValueKind == JsonValueKind.String)
            {
                config.ApiBaseUrl = urlElem.GetString()?.Trim() ?? string.Empty;
            }

            if (section.TryGetProperty("MachineCode", out var codeElem) && codeElem.ValueKind == JsonValueKind.String)
            {
                config.MachineCode = codeElem.GetString()?.Trim() ?? string.Empty;
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Nội dung tệp appsettings.json không đúng định dạng JSON: {ex.Message}", ex);
        }

        ValidateConfig(config);
        return config;
    }

    public static void ValidateConfig(MemberClientConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiBaseUrl))
        {
            throw new InvalidOperationException("Cấu hình 'ApiBaseUrl' trong appsettings.json không được để trống.");
        }

        if (!Uri.TryCreate(config.ApiBaseUrl, UriKind.Absolute, out var uri) || 
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"Cấu hình 'ApiBaseUrl' không phải là URL hợp lệ: '{config.ApiBaseUrl}'.");
        }

        // MachineCode is now selected by the customer on the first screen.
        // Keep the optional setting only for backward-compatible diagnostics.
    }
}
