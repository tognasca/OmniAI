using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OmniAI.Application;
using OmniAI.Domain.Interfaces;

namespace OmniAI.Infrastructure.BackgroundWorkers;

public class KnowledgeIndexingQueue : IBackgroundTaskQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public async Task EnqueueAsync(Guid knowledgeSourceId, CancellationToken ct = default)
        => await _channel.Writer.WriteAsync(knowledgeSourceId, ct);

    public async Task<Guid> DequeueAsync(CancellationToken ct)
        => await _channel.Reader.ReadAsync(ct);
}

public class KnowledgeIndexingWorker(
    IBackgroundTaskQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<KnowledgeIndexingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("KnowledgeIndexingWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var sourceId = await queue.DequeueAsync(stoppingToken);
                logger.LogInformation("Processing knowledge source {Id}", sourceId);

                using var scope = scopeFactory.CreateScope();
                var indexer = scope.ServiceProvider.GetRequiredService<IKnowledgeIndexingService>();
                await indexer.IndexSourceAsync(sourceId, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in KnowledgeIndexingWorker");
                await Task.Delay(5000, stoppingToken); // back-off on error
            }
        }

        logger.LogInformation("KnowledgeIndexingWorker stopped");
    }
}

/// <summary>
/// Message processing background worker.
/// Receives ProcessIncomingMessageCommand from the webhook controller via channel
/// to ensure the HTTP response is immediate (meta requires < 5s).
/// </summary>
public class IncomingMessageQueue
{
    private readonly Channel<IncomingMessageJob> _channel = Channel.CreateUnbounded<IncomingMessageJob>();

    public async Task EnqueueAsync(IncomingMessageJob job, CancellationToken ct = default)
        => await _channel.Writer.WriteAsync(job, ct);

    public async Task<IncomingMessageJob> DequeueAsync(CancellationToken ct)
        => await _channel.Reader.ReadAsync(ct);
}

public record IncomingMessageJob(
    string PhoneNumberId, string WaMessageId, string From,
    string MessageType, string? Content, string? MediaId);

public class MessageProcessorWorker(
    IncomingMessageQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<MessageProcessorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MessageProcessorWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await queue.DequeueAsync(stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider
                    .GetRequiredService<MediatR.IMediator>();

                await mediator.Send(
                    new OmniAI.Application.Contracts.Requests.ProcessIncomingMessageCommand(
                        job.PhoneNumberId, job.WaMessageId, job.From,
                        job.MessageType, job.Content, job.MediaId),
                    stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing incoming message");
            }
        }
    }
}
