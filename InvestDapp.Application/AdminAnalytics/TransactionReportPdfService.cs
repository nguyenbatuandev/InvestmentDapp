using InvestDapp.Shared.Common.Request;
using InvestDapp.Shared.DTOs.Admin;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace InvestDapp.Application.AdminAnalytics
{
    public class TransactionReportPdfService : ITransactionReportPdfService
    {
        private readonly ITransactionReportService _transactionReportService;

        public TransactionReportPdfService(ITransactionReportService transactionReportService)
        {
            _transactionReportService = transactionReportService;
        }

        public async Task<byte[]> GenerateReportAsync(TransactionReportFilterRequest filterRequest)
        {
            filterRequest ??= new TransactionReportFilterRequest();
            filterRequest.IncludeAll = true;
            filterRequest.PageNumber = 1;
            filterRequest.PageSize = int.MaxValue;

            var report = await _transactionReportService.GetTransactionsAsync(filterRequest);
            var document = new TransactionReportDocument(report, filterRequest);
            return document.GeneratePdf();
        }

        private class TransactionReportDocument : IDocument
        {
            private readonly TransactionReportResultDto _data;
            private readonly TransactionReportFilterRequest _filter;
            private readonly CultureInfo _culture = new("vi-VN");

            public TransactionReportDocument(TransactionReportResultDto data, TransactionReportFilterRequest filter)
            {
                _data = data;
                _filter = filter;
            }

            public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

            public DocumentSettings GetSettings() => DocumentSettings.Default;

            public void Compose(IDocumentContainer container)
            {
                container.Page(page =>
                {
                    page.Margin(32);
                    page.DefaultTextStyle(text => text.FontSize(11));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            }

            private void ComposeHeader(IContainer container)
            {
                container.Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text("Báo cáo giao dịch").FontSize(20).SemiBold();
                        column.Item().Text($"Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}").FontColor(Colors.Grey.Medium);
                    });
                });
            }

            private void ComposeContent(IContainer container)
            {
                container.Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Element(ComposeFilterSummary);
                    column.Item().Element(ComposeStats);
                    column.Item().Element(ComposeTransactionsTable);
                });
            }

            private void ComposeFilterSummary(IContainer container)
            {
                container.Text(text =>
                {
                    text.Span("Khoảng thời gian: ");
                    var start = _filter.StartDate?.ToString("dd/MM/yyyy") ?? "--";
                    var end = _filter.EndDate?.ToString("dd/MM/yyyy") ?? "--";
                    text.Span($"{start} - {end}").SemiBold();

                    text.Span("    |    Loại giao dịch: ");
                    text.Span(string.IsNullOrWhiteSpace(_filter.TransactionType) ? "Tất cả" : _filter.TransactionType).SemiBold();

                    text.Span("    |    Chiến dịch: ");
                    text.Span(string.IsNullOrWhiteSpace(_filter.CampaignName) ? "Tất cả" : _filter.CampaignName!).SemiBold();
                });
            }

            private void ComposeStats(IContainer container)
            {
                container.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Cell().Element(StatCard("Tổng đầu tư", _data.Summary.TotalInvestment, Colors.Blue.Medium));
                    table.Cell().Element(StatCard("Tổng Refund", _data.Summary.TotalRefund, Colors.Orange.Medium));
                    table.Cell().Element(StatCard("Dòng tiền ròng", _data.Summary.NetAmount, _data.Summary.NetAmount >= 0 ? Colors.Green.Medium : Colors.Red.Medium));
                });
            }

            private Action<IContainer> StatCard(string label, decimal value, string color) => container =>
                container
                    .Border(1)
                    .BorderColor(color)
                    .Padding(12)
                    .Background(Colors.Grey.Lighten5)
                    .Column(col =>
                    {
                        col.Item().Text(label).FontSize(11).FontColor(color).SemiBold();
                        col.Item().Text(value.ToString("N4", _culture) + " BNB").FontSize(14).SemiBold();
                    });

            private void ComposeTransactionsTable(IContainer container)
            {
                var transactions = _data.Transactions;
                if (transactions.Count == 0)
                {
                    container.Background(Colors.Grey.Lighten5).Padding(16).AlignCenter().Text("Không có giao dịch phù hợp với bộ lọc.");
                    return;
                }

                container.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(90);
                        columns.RelativeColumn(1.2f);
                        columns.ConstantColumn(80);
                        columns.RelativeColumn(1.5f);
                        columns.ConstantColumn(90);
                        columns.ConstantColumn(70);
                        columns.RelativeColumn(1.5f);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell("Thời gian"));
                        header.Cell().Element(HeaderCell("Chiến dịch"));
                        header.Cell().Element(HeaderCell("Loại"));
                        header.Cell().Element(HeaderCell("Nhà đầu tư"));
                        header.Cell().Element(HeaderCell("Số tiền"));
                        header.Cell().Element(HeaderCell("Trạng thái"));
                        header.Cell().Element(HeaderCell("Tx Hash"));
                    });

                    foreach (var tx in transactions)
                    {
                        table.Cell().Element(ContentCell(tx.OccurredAt == DateTime.MinValue ? "--" : tx.OccurredAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")));
                        table.Cell().Element(ContentCell(tx.CampaignName ?? "Không xác định"));
                        table.Cell().Element(ContentCell(tx.TransactionType));
                        table.Cell().Element(ContentCell(Shorten(tx.InvestorAddress)));
                        table.Cell().Element(ContentCell(tx.Amount.ToString("N4", _culture)));
                        table.Cell().Element(ContentCell(tx.Status));
                        table.Cell().Element(ContentCell(string.IsNullOrWhiteSpace(tx.TransactionHash) ? "--" : tx.TransactionHash));
                    }
                });
            }

            private static Action<IContainer> HeaderCell(string text) => container =>
            {
                container.Background(Colors.Grey.Lighten3).Padding(6).Text(text).SemiBold().FontSize(10);
            };

            private static Action<IContainer> ContentCell(string text) => container =>
            {
                container.Padding(6).Text(text).FontSize(10);
            };

            private static string Shorten(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return "--";
                return value.Length <= 15 ? value : $"{value[..6]}...{value[^4..]}";
            }
        }
    }
}
