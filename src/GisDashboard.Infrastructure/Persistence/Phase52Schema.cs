using GisDashboard.Domain;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

/// <summary>
/// CR11 — apply the approved status-name mapping on existing rows. IDs stay put.
/// QC'd is not renamed (no Brandon map). Safe to run on every startup.
/// </summary>
public static class Phase52Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.WorkItemStatuses.AnyAsync(cancellationToken))
        {
            return;
        }

        var rows = await db.WorkItemStatuses.ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            if (row.Id == SeedIds.StatusInProgress || row.Name == "In Progress")
            {
                row.Name = StatusDisplay.Active;
                row.SortOrder = 1;
                if (string.IsNullOrWhiteSpace(row.Color))
                {
                    row.Color = "#1890ff";
                }
            }
            else if (row.Id == SeedIds.StatusPending || row.Name == StatusDisplay.Pending)
            {
                row.Name = StatusDisplay.Pending;
                row.SortOrder = 2;
            }
            else if (row.Id == SeedIds.StatusWorked || row.Name == "Worked")
            {
                row.Name = StatusDisplay.Complete;
                row.SortOrder = 3;
                if (string.IsNullOrWhiteSpace(row.Color))
                {
                    row.Color = "#52c41a";
                }
            }
            else if (row.Id == SeedIds.StatusHeld || row.Name == "Held")
            {
                row.Name = StatusDisplay.OnHold;
                row.SortOrder = 4;
                if (string.IsNullOrWhiteSpace(row.Color))
                {
                    row.Color = "#fa8c16";
                }
            }
            else if (row.Id == SeedIds.StatusCancelled || row.Name == StatusDisplay.Cancelled)
            {
                row.Name = StatusDisplay.Cancelled;
                row.SortOrder = 5;
            }
            else if (row.Id == SeedIds.StatusNeedsReview || row.Name == StatusDisplay.NeedsReview)
            {
                row.Name = StatusDisplay.NeedsReview;
                row.SortOrder = 6;
                if (string.IsNullOrWhiteSpace(row.Color))
                {
                    row.Color = "#eb2f96";
                }
            }
            else if (row.Id == SeedIds.StatusQcd)
            {
                // No approved map — keep stored name and park after the canonical set.
                row.SortOrder = 7;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
