using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OmniAI.Application;
using OmniAI.Domain.Interfaces;
using OmniAI.Infrastructure.BackgroundWorkers;
using OmniAI.Infrastructure.Encryption;
using OmniAI.Infrastructure.Persistence;
using OmniAI.Infrastructure.Services;
using OmniAI.Infrastructure.Services.AI;
using OmniAI.Infrastructure.Services.KnowledgeIndexing;
using OmniAI.Infrastructure.Services.WhatsApp;

namespace OmniAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration cfg)
    {
        // ── Database ──────────────────────────────────────────────────
        services.AddDbContext<OmniDbContext>(opt =>
            opt.UseSqlServer(
                cfg.GetConnectionString("Default"),
                sql =>
                {
                    sql.MigrationsAssembly("OmniAI.Infrastructure");
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                    sql.CommandTimeout(30);
                }));

        services.AddScoped<IOmniDbContext>(sp => sp.GetRequiredService<OmniDbContext>());

        // ── Encryption ────────────────────────────────────────────────
        var encKey = cfg["Encryption:Key"];
        var encIv = cfg["Encryption:IV"];

        if (!string.IsNullOrWhiteSpace(encKey) && !string.IsNullOrWhiteSpace(encIv))
            AesEncryptor.Initialize(encKey, encIv);
        else
            AesEncryptor.InitializeDev(); // chave fixa só para desenvolvimento local

        // ── HTTP Clients com retry automático ─────────────────────────
        services.AddHttpClient<IWhatsAppService, WhatsAppService>()
                .AddStandardResilienceHandler();

        services.AddHttpClient<IAiService, OpenAiService>()
                .AddStandardResilienceHandler();

        services.AddHttpClient(); // client genérico para crawling de URLs

        // ── Domain services ───────────────────────────────────────────
        services.AddScoped<IHandoffService, HandoffService>();
        services.AddScoped<IKnowledgeIndexingService, KnowledgeIndexingService>();

        // ── Background workers & queues ───────────────────────────────
        services.AddSingleton<IBackgroundTaskQueue, KnowledgeIndexingQueue>();
        services.AddSingleton<IncomingMessageQueue>();
        services.AddHostedService<KnowledgeIndexingWorker>();
        services.AddHostedService<MessageProcessorWorker>();

        return services;
    }
}
