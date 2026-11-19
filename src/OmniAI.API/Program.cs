using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OmniAI.API;
using OmniAI.API.Hubs;
using OmniAI.Application;
using OmniAI.Domain.Entities;
using OmniAI.Infrastructure;
using OmniAI.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting OmniAI API...");
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

    var keysPath = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
        ? "/app/keys" : Path.Combine(AppContext.BaseDirectory, "keys");
    Directory.CreateDirectory(keysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("OmniAI");

    builder.Services.AddControllers()
        .AddJsonOptions(o =>
            o.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "OmniAI WhatsApp Manager", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new()
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });
        c.AddSecurityRequirement(new()
        {
            {
                new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(IOmniDbContext).Assembly);
        cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    });

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddHttpClient<OmniAI.API.WhatsApp.MetaSignupClient>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    .RemoveAllLoggers()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
    builder.Services.AddSignalR();
    builder.Services.AddScoped<IDashboardNotifier, SignalRDashboardNotifier>();

    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  ?? ["http://localhost:3000", "http://localhost:5173"];
    builder.Services.AddCors(opt =>
        opt.AddDefaultPolicy(p =>
            p.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

    var signingKey = builder.Configuration["Jwt:Key"];
    if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32 ||
        signingKey.StartsWith("omni-ai-dev-", StringComparison.Ordinal))
        throw new InvalidOperationException("Configure Jwt:Key with a unique secret of at least 32 bytes.");

    builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "OmniAI",
            ValidateAudience = true,
            ValidAudience = "omni-ai-app",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Path == "/hubs/dashboard" &&
                    context.HttpContext.WebSockets.IsWebSocketRequest)
                    context.Token = context.Request.Query["access_token"];
                return Task.CompletedTask;
            }
        };
    });
    builder.Services.AddAuthorization();

    builder.Services.AddHealthChecks()
        .AddSqlServer(builder.Configuration.GetConnectionString("Default")!, name: "sqlserver");

    var app = builder.Build();
    await MigrateWithRetryAsync(app.Services);

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OmniAI v1");
        c.RoutePrefix = string.Empty;
    });

    if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
        app.UseHttpsRedirection();

    app.UseCors();
    app.UseSerilogRequestLogging(o =>
    {
        o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} -> {StatusCode} ({Elapsed:0.0}ms)";
        o.GetLevel = (ctx, _, _) =>
            ctx.Request.Path.StartsWithSegments("/health") || ctx.Request.Path.StartsWithSegments("/swagger")
                ? LogEventLevel.Verbose : LogEventLevel.Information;
    });

    app.UseWebSockets();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<DashboardHub>("/hubs/dashboard", options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization();
    app.MapHealthChecks("/health");
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

    Log.Information("OmniAI API ready -> http://localhost:5000");
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "OmniAI API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
return 0;

// ── MigrateWithRetryAsync ────────────────────────────────────────────────────
static async Task MigrateWithRetryAsync(IServiceProvider services)
{
    const int maxAttempts = 10;
    var delay = TimeSpan.FromSeconds(3);

    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OmniDbContext>();
            Log.Information("Migration attempt {Attempt}/{Max}...", attempt, maxAttempts);
            await db.Database.MigrateAsync();


            Log.Information("Database ready.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            Log.Warning("Migration attempt {Attempt} failed: {Message}. Retrying in {Delay}s...",
                attempt, ex.Message, (int)delay.TotalSeconds);
            await Task.Delay(delay);
            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 30));
        }
    }

    // Última tentativa sem catch — deixa estourar
    using var finalScope = services.CreateScope();
    var finalDb = finalScope.ServiceProvider.GetRequiredService<OmniDbContext>();
    await finalDb.Database.MigrateAsync();
}
