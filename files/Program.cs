using MediatR;
using Microsoft.AspNetCore.DataProtection;
using OmniAI.API.Hubs;
using OmniAI.Application;
using OmniAI.Infrastructure;
using OmniAI.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;

// ── Bootstrap logger (antes de tudo, para capturar erros de startup) ──
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting OmniAI API...");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog (lê de appsettings) ───────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

    // ── Data Protection ───────────────────────────────────────────
    // Persiste as chaves em disco dentro do container (pasta /app/keys)
    // Em produção: use Azure Key Vault ou Redis para compartilhar entre instâncias
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/app/keys"))
        .SetApplicationName("OmniAI");

    // ── Controllers + Swagger ─────────────────────────────────────
    builder.Services.AddControllers()
        .AddJsonOptions(opt =>
        {
            opt.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title   = "OmniAI WhatsApp Manager API",
            Version = "v1",
            Description = "API para gerenciamento de atendimento automatizado via WhatsApp Business"
        });
        c.AddSecurityDefinition("Bearer", new()
        {
            Type        = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme      = "bearer",
            BearerFormat = "JWT",
            Description = "Insira o token JWT"
        });
        c.AddSecurityRequirement(new()
        {
            {
                new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
                Array.Empty<string>()
            }
        });
    });

    // ── MediatR ───────────────────────────────────────────────────
    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(typeof(IOmniDbContext).Assembly));

    // ── Infrastructure (EF, services, workers) ───────────────────
    builder.Services.AddInfrastructure(builder.Configuration);

    // ── SignalR ───────────────────────────────────────────────────
    builder.Services.AddSignalR();
    builder.Services.AddScoped<IDashboardNotifier, SignalRDashboardNotifier>();

    // ── CORS ──────────────────────────────────────────────────────
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:Origins")
        .Get<string[]>()
        ?? ["http://localhost:3000", "http://localhost:5173"];

    builder.Services.AddCors(opt =>
        opt.AddDefaultPolicy(p =>
            p.WithOrigins(allowedOrigins)
             .AllowAnyMethod()
             .AllowAnyHeader()
             .AllowCredentials()));

    // ── Auth (JWT) ────────────────────────────────────────────────
    var authAuthority = builder.Configuration["Auth:Authority"];
    if (!string.IsNullOrEmpty(authAuthority))
    {
        builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", opt =>
            {
                opt.Authority = authAuthority;
                opt.Audience  = builder.Configuration["Auth:Audience"];
                opt.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            });
        builder.Services.AddAuthorization();
    }
    else
    {
        // Dev mode: auth desabilitada se Authority não configurada
        Log.Warning("Auth:Authority not configured — authentication is DISABLED. Do not use in production.");
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization(opt =>
            opt.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAssertion(_ => true)
                .Build());
    }

    // ── Health checks ─────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("Default")!,
            name: "sqlserver",
            tags: ["db", "ready"]);

    // ── Build ─────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Migrations + Seed ─────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<OmniDbContext>();
            Log.Information("Applying database migrations...");
            await db.Database.MigrateAsync();
            Log.Information("Seeding database...");
            await OmniDbSeeder.SeedAsync(db);
            Log.Information("Database ready.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Database migration/seed failed");
            throw;
        }
    }

    // ── Middleware pipeline ───────────────────────────────────────

    // Swagger sempre ativo (útil para testes mesmo em prod por ora)
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OmniAI v1");
        c.RoutePrefix = string.Empty; // Swagger na raiz: http://localhost:5000/
    });

    // NÃO usar UseHttpsRedirection dentro do Docker
    // O container recebe HTTP do reverse proxy externo (nginx/traefik)
    if (!IsRunningInContainer())
        app.UseHttpsRedirection();

    app.UseCors();
    app.UseSerilogRequestLogging(opt =>
    {
        opt.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} ({Elapsed:0.0}ms)";
        // Não logar health checks e swagger para não poluir
        opt.GetLevel = (ctx, elapsed, ex) =>
            ctx.Request.Path.StartsWithSegments("/health") ||
            ctx.Request.Path.StartsWithSegments("/swagger")
                ? LogEventLevel.Verbose
                : LogEventLevel.Information;
    });

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<DashboardHub>("/hubs/dashboard");
    app.MapHealthChecks("/health");

    // Root redirect → Swagger
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

    Log.Information("OmniAI API started. Swagger: http://localhost:5000");
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

// ── Helpers ───────────────────────────────────────────────────────
static bool IsRunningInContainer() =>
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
