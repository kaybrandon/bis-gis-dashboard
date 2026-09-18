using GisDashboard.Application.AiFill;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GisDashboard.Infrastructure.AiFill;

public sealed class WorkItemAutoAiScanWorker : BackgroundService
{
    private readonly WorkItemAutoAiScanScheduler _scheduler;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<WorkItemAutoAiScanWorker> _logger;

    public WorkItemAutoAiScanWorker(
        IWorkItemAutoAiScanScheduler scheduler,
        IServiceScopeFactory scopes,
        ILogger<WorkItemAutoAiScanWorker> logger)
    {
        _scheduler = (WorkItemAutoAiScanScheduler)scheduler;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingAsync(stoppingToken);
        await foreach (var workItemId in _scheduler.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var fill = scope.ServiceProvider.GetRequiredService<IWorkItemAiFillService>();
                await fill.RunAutoScanAsync(workItemId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto AI-scan worker failed for {WorkItemId}", workItemId);
            }
        }
    }

    private async Task RecoverPendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pending = await db.WorkItems.AsNoTracking()
                .Where(x => x.AiScanStatus == WorkItemAiScanStatus.Pending
                    || x.AiScanStatus == WorkItemAiScanStatus.Running)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            foreach (var id in pending)
            {
                _scheduler.Enqueue(id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not recover pending AI scans on startup.");
        }
    }
}
