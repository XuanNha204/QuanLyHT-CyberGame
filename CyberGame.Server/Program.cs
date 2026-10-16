using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberGame.Application.Common;
using CyberGame.Infrastructure;
using CyberGame.Infrastructure.Data;
using CyberGame.Server.Middleware;
using CyberGame.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. Controller & JSON options (camelCase, string enum converters)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// 2. Add Infrastructure (EF Core, Services, Repositories, Hashers, Initializer)
builder.Services.AddInfrastructure(builder.Configuration);

// 3. Configure JWT Authentication - Fail fast if Secret is missing or < 32 characters
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException("Cấu hình 'Jwt:Secret' bị thiếu hoặc có độ dài nhỏ hơn 32 ký tự. Hệ thống từ chối khởi động vì lý do an toàn.");
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CyberGameServer";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "CyberGameClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim("actor_type", "staff")
        .Build();

    options.AddPolicy("OwnerOnly", policy => policy.RequireClaim("actor_type", "staff").RequireRole("Owner"));
    options.AddPolicy("ManagerOnly", policy => policy.RequireClaim("actor_type", "staff").RequireRole("Owner", "Manager"));
    options.AddPolicy("CashierOnly", policy => policy.RequireClaim("actor_type", "staff").RequireRole("Owner", "Manager", "Cashier"));
    options.AddPolicy("TechnicianOnly", policy => policy.RequireClaim("actor_type", "staff").RequireRole("Owner", "Manager", "Technician"));

    options.AddPolicy("MemberOnly", policy => policy.RequireAuthenticatedUser().RequireClaim("actor_type", "member"));
});

// 4. CORS: Loại bỏ AllowAll không cần thiết cho WPF desktop; chỉ bật khi có nguồn được chỉ định trong cấu hình
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ConfiguredCors", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });
}

builder.Services.AddOpenApi();

var app = builder.Build();

// 5. Middleware Pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (allowedOrigins.Length > 0)
{
    app.UseCors("ConfiguredCors");
}

app.UseAuthentication();
app.UseMiddleware<IdempotencyMiddleware>();
app.UseAuthorization();

app.MapControllers();

// 6. Database Migration & Initialization on startup (Không nuốt ngoại lệ)
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.InitializeAsync();
}

app.Run();
