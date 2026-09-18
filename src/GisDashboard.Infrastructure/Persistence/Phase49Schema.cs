using GisDashboard.Domain;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase49Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.WorkItemStatuses.AnyAsync(cancellationToken))
        {
            return;
        }

        if (!await db.WorkItemStatuses.AnyAsync(
                x => x.Id == SeedIds.StatusNeedsReview || x.Name == "Needs Review",
                cancellationToken))
        {
            db.WorkItemStatuses.Add(new WorkItemStatus
            {
                Id = SeedIds.StatusNeedsReview,
                Name = "Needs Review",
                Color = "#eb2f96",
                SortOrder = 4
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var worked = await db.WorkItemStatuses.FirstOrDefaultAsync(x => x.Id == SeedIds.StatusWorked, cancellationToken);
        var qcd = await db.WorkItemStatuses.FirstOrDefaultAsync(x => x.Id == SeedIds.StatusQcd, cancellationToken);
        var cancelled = await db.WorkItemStatuses.FirstOrDefaultAsync(x => x.Id == SeedIds.StatusCancelled, cancellationToken);
        var needsReview = await db.WorkItemStatuses.FirstOrDefaultAsync(
            x => x.Id == SeedIds.StatusNeedsReview || x.Name == "Needs Review",
            cancellationToken);

        if (needsReview is not null)
        {
            needsReview.Name = "Needs Review";
            if (string.IsNullOrWhiteSpace(needsReview.Color))
            {
                needsReview.Color = "#eb2f96";
            }

            needsReview.SortOrder = 4;
        }

        if (worked is not null && worked.SortOrder == 4)
        {
            worked.SortOrder = 5;
        }

        if (qcd is not null && qcd.SortOrder == 5)
        {
            qcd.SortOrder = 6;
        }

        if (cancelled is not null && cancelled.SortOrder == 6)
        {
            cancelled.SortOrder = 7;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
