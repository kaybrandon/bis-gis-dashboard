using System.Globalization;
using GisDashboard.Application.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GisDashboard.Infrastructure.Reports;

public static class MaintenanceReportPdf
{
    private static readonly Color HeaderBar = Color.FromHex("#1f1f1f");
    private static readonly Color Accent = Color.FromHex("#c23b32");
    private static readonly Color Muted = Color.FromHex("#595959");
    private static readonly Color RowAlt = Color.FromHex("#f5f7fa");

    static MaintenanceReportPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Build(ReportDetail report)
    {
        var snapshot = report.Snapshot;
        var dated = ReportCadences.IsAnnual(snapshot.Cadence)
            ? new DateTime(report.Year, 12, 31).ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture)
            : report.GeneratedAt.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#262626"));
                page.Header().Element(h => PageHeader(h, snapshot, dated));
                page.Content().Element(c => Body(c, snapshot));
                page.Footer().Element(Footer);
            });
        }).GeneratePdf();
    }

    public static string FileName(ReportSnapshot snapshot) =>
        ReportCadences.IsAnnual(snapshot.Cadence)
            ? $"{Sanitize(snapshot.OrganizationName)} {snapshot.MonthName} Annual GIS Maintenance Report.pdf"
            : $"{Sanitize(snapshot.OrganizationName)} {snapshot.MonthLabel} GIS Maintenance Report.pdf";

    private static void PageHeader(IContainer container, ReportSnapshot snapshot, string dated)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("BIS").Bold().FontSize(16).FontColor("#1890ff");
                    left.Item().Text("CONSULTING").FontSize(8).FontColor(Muted);
                });
                row.RelativeItem(2).AlignCenter().Column(center =>
                {
                    center.Item().AlignCenter().Text(snapshot.Title).Bold().FontSize(16);
                    center.Item().AlignCenter().Text(snapshot.MonthLabel).FontSize(11).FontColor(Muted);
                });
                row.RelativeItem().AlignRight().Column(right =>
                {
                    right.Item().AlignRight().Text($"Phone: {snapshot.Contact.Phone}").FontSize(8);
                    right.Item().AlignRight().Text(snapshot.Contact.Email).FontSize(8).FontColor("#1890ff");
                    right.Item().AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(x => x.FontSize(8).FontColor(Muted));
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            });
            col.Item().PaddingTop(8).Text($"Dated: {dated}").FontSize(9);
            col.Item().Text(snapshot.OrganizationName).Bold().FontSize(11);
            col.Item().PaddingBottom(8).LineHorizontal(1).LineColor("#d9d9d9");
        });
    }

    private static void Body(IContainer container, ReportSnapshot snapshot)
    {
        container.Column(col =>
        {
            col.Item().Text(snapshot.Intro).FontSize(10);
            col.Item().PaddingTop(16).Element(c =>
            {
                if (snapshot.IncludeParcelStatus)
                {
                    c.Row(row =>
                    {
                        row.RelativeItem().PaddingRight(10).Element(p => ParcelTable(p, snapshot));
                        row.RelativeItem().PaddingLeft(10).Element(p => CompletedSummary(p, snapshot));
                    });
                }
                else
                {
                    CompletedSummary(c, snapshot);
                }
            });

            col.Item().PaddingTop(16).Element(c => CompletedTable(c, snapshot));

            col.Item().PaddingTop(20).Text(snapshot.Closing);
            col.Item().PaddingTop(8).Text("Thank you,").Bold();
            col.Item().Text(snapshot.Contact.Department);
            col.Item().Text(snapshot.Contact.Company);
        });
    }

    private static void ParcelTable(IContainer container, ReportSnapshot snapshot)
    {
        var parcel = snapshot.ParcelStatus;
        container.Column(col =>
        {
            col.Item().Background(HeaderBar).Padding(6)
                .Text("Parcel Status To Date").FontColor(Colors.White).Bold().FontSize(11);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                });

                Row("Total Real Accounts", FormatCount(parcel.TotalRealAccounts, parcel.Available));
                Row("Parcels With Ownership Information", FormatCount(parcel.ParcelsWithOwnership, parcel.Available));
                Row("Missing Real Accounts", FormatCount(parcel.MissingRealAccounts, parcel.Available));
                Row("Percent Complete", parcel.Available && parcel.PercentComplete is { } pct
                    ? $"{pct:0}%"
                    : "Not available");

                void Row(string label, string value)
                {
                    table.Cell().BorderBottom(1).BorderColor("#e8e8e8").Padding(6).Text(label);
                    table.Cell().BorderBottom(1).BorderColor("#e8e8e8").Padding(6).AlignRight().Text(value).Bold();
                }
            });
            if (!parcel.Available)
            {
                col.Item().PaddingTop(8).Text(parcel.Note).FontSize(8).FontColor(Muted).Italic();
            }
        });
    }

    private static void CompletedSummary(IContainer container, ReportSnapshot snapshot)
    {
        container.Column(col =>
        {
            col.Item().Background(HeaderBar).Padding(6)
                .Text($"Total Maintenance Items Completed in {snapshot.MonthName}")
                .FontColor(Colors.White).Bold().FontSize(11);
            col.Item().PaddingTop(16).AlignCenter().Width(160).Height(160).Element(circle =>
            {
                circle.Border(14).BorderColor(Accent).AlignCenter().AlignMiddle().Column(inner =>
                {
                    inner.Item().AlignCenter().Text("Total").FontSize(10).FontColor(Muted);
                    inner.Item().AlignCenter().Text(snapshot.Completed.ToString("N0")).Bold().FontSize(28).FontColor(Accent);
                });
            });
            col.Item().PaddingTop(12).Column(legend =>
            {
                if (snapshot.MaintenanceByType.Count == 0)
                {
                    legend.Item().AlignCenter().Text($"No completed maintenance items {snapshot.PeriodPhrase}.").FontColor(Muted);
                    return;
                }

                foreach (var slice in snapshot.MaintenanceByType)
                {
                    legend.Item().AlignCenter().Text($"{slice.Name} ({slice.Count})").FontSize(10);
                }
            });
        });
    }

    private static void CompletedTable(IContainer container, ReportSnapshot snapshot)
    {
        container.Column(col =>
        {
            col.Item().Background(HeaderBar).Padding(6)
                .Text($"Completed Maintenance Items in {snapshot.MonthName}")
                .FontColor(Colors.White).Bold().FontSize(11);

            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.4f);
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(0.8f);
                    columns.RelativeColumn(0.8f);
                    columns.RelativeColumn(0.7f);
                    columns.RelativeColumn(0.7f);
                    columns.RelativeColumn(0.7f);
                    columns.RelativeColumn(1.6f);
                });

                table.Header(header =>
                {
                    Header(header, "File Name");
                    Header(header, "Upload Date");
                    Header(header, "Worked Date");
                    Header(header, "Annexations");
                    Header(header, "Corrections");
                    Header(header, "Plats");
                    Header(header, "Deeds");
                    Header(header, "Sketch");
                    Header(header, "Property Ids");
                });

                var index = 0;
                foreach (var item in snapshot.CompletedItems)
                {
                    var bg = index % 2 == 0 ? Colors.White : RowAlt;
                    Cell(item.FileName, bg, left: true);
                    Cell(item.UploadedAt.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture), bg);
                    Cell(item.WorkedOn?.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) ?? "—", bg);
                    Cell(item.Annexations.ToString(), bg);
                    Cell(item.Corrections.ToString(), bg);
                    Cell(item.Plats.ToString(), bg);
                    Cell(item.Deeds.ToString(), bg);
                    Cell(item.Sketch ? "Yes" : "No", bg);
                    Cell(item.PropertyIds, bg, left: true);
                    index++;
                }

                if (snapshot.CompletedItems.Count == 0)
                {
                    table.Cell().ColumnSpan(9).Padding(10)
                        .Text($"No work items were completed {snapshot.PeriodPhrase}.").FontColor(Muted);
                }

                void Header(TableCellDescriptor header, string text) =>
                    header.Cell().Background(HeaderBar).Padding(4).Text(text).FontColor(Colors.White).Bold().FontSize(8);

                void Cell(string text, Color bg, bool left = false)
                {
                    var cell = table.Cell().Background(bg).BorderBottom(1).BorderColor("#e8e8e8").Padding(4);
                    if (left)
                    {
                        cell.Text(text).FontSize(8);
                    }
                    else
                    {
                        cell.AlignCenter().Text(text).FontSize(8);
                    }
                }
            });
        });
    }

    private static void Footer(IContainer container)
    {
        container.PaddingTop(8).Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Item().Text("GIS Department").FontSize(8).FontColor(Muted);
                left.Item().Text($"Phone: {ReportBranding.Phone}").FontSize(8).FontColor(Muted);
            });
            row.RelativeItem().AlignRight().Column(right =>
            {
                right.Item().AlignRight().Text(ReportBranding.Company).FontSize(8).FontColor(Muted);
                right.Item().AlignRight().Text(ReportBranding.Website).FontSize(8).FontColor("#1890ff");
            });
        });
    }

    private static string FormatCount(int? value, bool available) =>
        available && value is { } n ? n.ToString("N0", CultureInfo.InvariantCulture) : "Not available";

    private static string Sanitize(string value)
    {
        var chars = value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c).ToArray();
        return new string(chars).Trim();
    }
}
