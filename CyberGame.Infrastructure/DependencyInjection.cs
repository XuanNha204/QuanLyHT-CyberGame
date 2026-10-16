using CyberGame.Application.Common;
using CyberGame.Application.Services;
using CyberGame.Infrastructure.Data;
using CyberGame.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CyberGame.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost;Database=CyberGameDb;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddDbContext<CyberGameDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(CyberGameDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            });
        });

        services.AddScoped<ICyberGameDbContext>(provider => provider.GetRequiredService<CyberGameDbContext>());

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<DbInitializer>();

        // Application Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBranchMachineService, BranchMachineService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<ICustomerWalletService, CustomerWalletService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<ICatalogInventoryService, CatalogInventoryService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IDashboardReportService, DashboardReportService>();
        services.AddScoped<IMemberService, MemberService>();

        return services;
    }
}
