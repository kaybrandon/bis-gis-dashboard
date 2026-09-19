using System.Threading.Channels;
using GisDashboard.Application.AiFill;
using GisDashboard.Domain;
using Microsoft.Extensions.Logging;

namespace GisDashboard.Infrastructure.AiFill;

public sealed class WorkItemAutoAiScanScheduler : IWorkItemAutoAiScanScheduler
{
    private readonly IAzureOpenAiCompletions _completions;
    private readonly ILogger<WorkItemAutoAiScanScheduler> _logger;
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        AllowSynchronousContinuations = false
    });

    public WorkItemAutoAiScanScheduler(
        IAzureOpenAiCompletions completions,
        ILogger<WorkItemAutoAiScanScheduler> logger)
    {
        _completions = completions;
        _logger = logger;
    }

    internal ChannelReader<Guid> Reader => _channel.Reader;

    public bool PrepareNewUpload(WorkItem item)
    {
        try
        {
            item.AiScanBaselineJson = WorkItemAiScanJson.CaptureBaseline(item);
            if (!AiFillSourceKinds.IsAnalyzable(item.FileName, item.ContentType))
            {
                item.AiScanStatus = WorkItemAiScanStatus.Skipped;
                item.AiScanMessage = WorkItemAiScanStatus.SkippedMessage;
                item.AiScanCompletedAt = DateTimeOffset.UtcNow;
                item.AiScanResultJson = null;
                return false;
            }

            if (!_completions.IsConfigured)
            {
                item.AiScanStatus = WorkItemAiScanStatus.Unconfigured;
                item.AiScanMessage = WorkItemAiScanStatus.UnconfiguredMessage;
                item.AiScanCompletedAt = DateTimeOffset.UtcNow;
                return false;
            }

            item.AiScanStatus = WorkItemAiScanStatus.Pending;
            item.AiScanMessage = WorkItemAiScanStatus.PendingMessage;
            item.AiScanStartedAt = null;
            item.AiScanCompletedAt = null;
            item.AiScanResultJson = null;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not stamp AI scan state for work item {WorkItemId}", item.Id);
            return false;
        }
    }

    public void Enqueue(Guid workItemId)
    {
        if (workItemId == Guid.Empty)
        {
            return;
        }

        if (!_channel.Writer.TryWrite(workItemId))
        {
            _logger.LogWarning("AI scan queue rejected work item {WorkItemId}", workItemId);
        }
    }
}
