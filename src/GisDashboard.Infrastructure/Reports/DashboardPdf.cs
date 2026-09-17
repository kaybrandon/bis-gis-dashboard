using System.Globalization;
using GisDashboard.Application.WorkItems;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GisDashboard.Infrastructure.Reports;

public static class DashboardPdf
{
    private static readonly Color HeaderBar = Color.FromHex("#001529");
    private static readonly Color Muted = Color.FromHex("#595959");
    private static readonly Color RowAlt = Color.FromHex("#f5f7fa");

    static DashboardPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Build(DashboardResponse data, DashboardQuery query, string generatedBy)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#262626"));
                page.Header().Element(h => Header(h, data, generatedBy));
                page.Content().Element(c => Body(c, data));
                page.Footer().AlignRight().DefaultTextStyle(x => x.FontSize(8).FontColor(Muted)).Text(t =>
                {
                    t.Span("GIS Dashboard  ·  ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    public static string FileName(DashboardResponse data) =>
        $"GIS-Dashboard-{data.From:yyyyMMdd}-{data.To:yyyyMMdd}.pdf";

    private static void Header(IContainer container, DashboardResponse data, string generatedBy)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("BIS Consultants").Bold().FontSize(14).FontColor("#1890ff");
                    left.Item().Text("GIS Dashboard").FontSize(16).Bold();
                });
                row.RelativeItem().AlignRight().Column(right =>
                {
                    right.Item().AlignRight().Text(data.RangeLabel).FontSize(11);
                    right.Item().AlignRight().Text($"Prepared {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor(Muted);
                    right.Item().AlignRight().Text(generatedBy).FontSize(8).FontColor(Muted);
                });
            });
            col.Item().PaddingTop(6).LineHorizontal(1).LineColor("#d9d9d9");
        });
    }

    private static void Body(IContainer container, DashboardResponse data)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(8).Text("Filters on this report match the Dashboard page: organization, status, assignee, and date range.").FontSize(9).FontColor(Muted);

            col.Item().PaddingBottom(10).Row(row =>
            {
                foreach (var kpi in data.Kpis)
                {
                    row.RelativeItem().PaddingRight(6).Border(1).BorderColor("#d9d9d9").Padding(8).Column(box =>
                    {
                        box.Item().Text(kpi.Label).FontSize(9).FontColor(Muted);
                        box.Item().Text(kpi.Count.ToString(CultureInfo.InvariantCulture)).FontSize(18).Bold().FontColor(kpi.Color ?? "#262626");
                    });
                }
            });

            Section(col, "Volume by status", table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.RelativeColumn(1);
                });
                table.Header(h =>
                {
                    Head(h, "Status");
                    Head(h, "Items");
                });
                var i = 0;
                foreach (var item in data.StatusCounts.Where(x => x.Count > 0))
                {
                    var bg = i++ % 2 == 0 ? Colors.White : RowAlt;
                    Cell(table, item.Name, bg, true);
                    Cell(table, item.Count.ToString(CultureInfo.InvariantCulture), bg);
                }
            });

            Section(col, $"Work over {data.RangeLabel}", table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1);
                    c.RelativeColumn(1);
                });
                table.Header(h =>
                {
                    Head(h, "Date");
                    Head(h, "Uploaded");
                    Head(h, "Completed");
                });
                var i = 0;
                foreach (var day in data.VolumeOverTime.Where(x => x.Uploaded > 0 || x.Completed > 0))
                {
                    var bg = i++ % 2 == 0 ? Colors.White : RowAlt;
                    Cell(table, day.Date, bg, true);
                    Cell(table, day.Uploaded.ToString(CultureInfo.InvariantCulture), bg);
                    Cell(table, day.Completed.ToString(CultureInfo.InvariantCulture), bg);
                }

                if (data.VolumeOverTime.All(x => x.Uploaded == 0 && x.Completed == 0))
                {
                    Cell(table, "No uploads or completions in this range.", Colors.White, true);
                    Cell(table, "0", Colors.White);
                    Cell(table, "0", Colors.White);
                }
            });

            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().PaddingRight(6).Element(left => HoursTable(left, "Hours by assignee", data.HoursByAssignee));
                row.RelativeItem().Element(right => HoursTable(right, "Hours by client", data.HoursByClient));
            });

            Section(col, "Recently completed", table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2.2f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(1.1f);
                    c.RelativeColumn(0.8f);
                });
                table.Header(h =>
                {
                    Head(h, "File");
                    Head(h, "Client");
                    Head(h, "Assigned to");
                    Head(h, "Worked");
                    Head(h, "Hours");
                });
                if (data.RecentCompleted.Count == 0)
                {
                    Cell(table, "No completed work items match these filters.", Colors.White, true);
                    Cell(table, "—", Colors.White);
                    Cell(table, "—", Colors.White);
                    Cell(table, "—", Colors.White);
                    Cell(table, "—", Colors.White);
                    return;
                }

                var i = 0;
                foreach (var item in data.RecentCompleted)
                {
                    var bg = i++ % 2 == 0 ? Colors.White : RowAlt;
                    Cell(table, item.FileName, bg, true);
                    Cell(table, item.OrganizationName, bg, true);
                    Cell(table, item.AssignedToName ?? "—", bg, true);
                    Cell(table, item.WorkedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—", bg);
                    Cell(table, item.HoursLabel, bg);
                }
            });
        });
    }

    private static void HoursTable(IContainer container, string title, IReadOnlyList<HoursSlice> rows)
    {
        container.Column(col =>
        {
            col.Item().Background(HeaderBar).Padding(6).Text(title).FontColor(Colors.White).Bold().FontSize(11);
            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.RelativeColumn(1);
                });
                table.Header(h =>
                {
                    Head(h, "Name");
                    Head(h, "Hours");
                });
                if (rows.Count == 0)
                {
                    Cell(table, "No hours in this range.", Colors.White, true);
                    Cell(table, "0", Colors.White);
                    return;
                }

                var i = 0;
                foreach (var row in rows)
                {
                    var bg = i++ % 2 == 0 ? Colors.White : RowAlt;
                    Cell(table, row.Name, bg, true);
                    Cell(table, row.Hours.ToString("0.##", CultureInfo.InvariantCulture), bg);
                }
            });
        });
    }

    private static void Section(ColumnDescriptor col, string title, Action<TableDescriptor> content)
    {
        col.Item().PaddingTop(8).Background(HeaderBar).Padding(6).Text(title).FontColor(Colors.White).Bold().FontSize(11);
        col.Item().PaddingTop(4).Table(content);
    }

    private static void Head(TableCellDescriptor header, string text) =>
        header.Cell().Background(HeaderBar).Padding(4).Text(text).FontColor(Colors.White).SemiBold().FontSize(8);

    private static void Cell(TableDescriptor table, string text, Color bg, bool left = false)
    {
        var cell = table.Cell().Background(bg).Padding(4);
        if (left)
        {
            cell.Text(text).FontSize(8);
        }
        else
        {
            cell.AlignRight().Text(text).FontSize(8);
        }
    }
}
