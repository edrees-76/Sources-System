using ClosedXML.Excel;
using Sources.Models;
using Sources.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sources.Services
{
    public class ReportingService : IReportingService
    {
        public ReportingService()
        {
            // Set QuestPDF License
            QuestPDF.Settings.License = LicenseType.Community;
        }

        private static string GetNoDataText()
        {
            try
            {
                string text = TranslationHelper.GetString("LabelNoDataAvailable");
                if (!string.IsNullOrEmpty(text) && text != "LabelNoDataAvailable")
                    return text;
            }
            catch { }
            return "لا توجد بيانات حالياً";
        }

        public async Task GenerateInventoryReportExcelAsync(IEnumerable<Source> sources, string filePath, string reportTitle)
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add(reportTitle);
                
                worksheet.RightToLeft = true;

                // Headers
                string[] headers = { "رقم المصدر", "النظير", "النشاط الحالي", "الموقع", "الحالة", "أُضيف بواسطة" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int row = 2;
                foreach (var source in sources)
                {
                    worksheet.Cell(row, 1).Value = source.SourceCode;
                    worksheet.Cell(row, 2).Value = source.DisplayIsotopes;
                    worksheet.Cell(row, 3).Value = source.CurrentActivityWithUnit;
                    worksheet.Cell(row, 4).Value = source.Location?.LocationName ?? "غير محدد";
                    worksheet.Cell(row, 5).Value = source.ArabicStatus;
                    worksheet.Cell(row, 6).Value = source.AddedBy ?? "غير معروف";
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            });
        }

        public async Task GenerateInventoryReportPdfAsync(IEnumerable<Source> sources, string filePath, string reportTitle)
        {
            await Task.Run(() =>
            {
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        
                        // Use Arial for Arabic characters support
                        page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));
                        page.ContentFromRightToLeft();
 
                        page.Header().Element(c => ComposeHeaderInventory(c, reportTitle));
                        page.Content().Element(x => ComposeContentInventory(x, sources));
                        page.Footer().Element(ComposeFooter);
                    });
                })
                .GeneratePdf(filePath);
            });
        }

        private void ComposeHeader(IContainer container, string reportTitle) => ComposeHeaderInventory(container, reportTitle);

        private void ComposeHeaderInventory(IContainer container, string reportTitle)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(reportTitle).FontSize(24).SemiBold().FontColor(Colors.Blue.Medium);
                    column.Item().PaddingTop(5).Text($"تاريخ التقرير: {DateTime.Now:yyyy/MM/dd}").FontSize(12).FontColor(Colors.Grey.Medium);
                    column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });
            });
        }

        private void ComposeContentInventory(IContainer container, IEnumerable<Source> sources)
        {
            var list = sources?.ToList() ?? new List<Source>();
            string noDataText = GetNoDataText();

            container.PaddingVertical(0.5f, Unit.Centimetre).Column(column =>
            {
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2); // رقم المصدر
                        columns.RelativeColumn(3); // النظير
                        columns.RelativeColumn(3); // النشاط الحالي
                        columns.RelativeColumn(3); // الموقع
                        columns.RelativeColumn(2); // الحالة
                        columns.RelativeColumn(2); // أُضيف بواسطة
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderStyle).Text("رقم المصدر");
                        header.Cell().Element(HeaderStyle).Text("النظير");
                        header.Cell().Element(HeaderStyle).Text("النشاط الحالي");
                        header.Cell().Element(HeaderStyle).Text("الموقع");
                        header.Cell().Element(HeaderStyle).Text("الحالة");
                        header.Cell().Element(HeaderStyle).Text("أُضيف بواسطة");

                        static IContainer HeaderStyle(IContainer container)
                        {
                            return container.Background(Colors.Blue.Medium).PaddingVertical(8).AlignCenter().DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White));
                        }
                    });

                    if (!list.Any())
                    {
                        table.Cell().ColumnSpan(6).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        int i = 0;
                        foreach (var source in list)
                        {
                            var backgroundColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                            
                            table.Cell().Element(CellStyle).Text(source.SourceCode);
                            table.Cell().Element(CellStyle).Text(source.DisplayIsotopes);
                            table.Cell().Element(CellStyle).Text(source.CurrentActivityWithUnit);
                            table.Cell().Element(CellStyle).Text(source.Location?.LocationName ?? "-");
                            table.Cell().Element(CellStyle).Text(source.ArabicStatus);
                            table.Cell().Element(CellStyle).Text(source.AddedBy ?? "-");

                            IContainer CellStyle(IContainer container)
                            {
                                return container.Background(backgroundColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(6).AlignCenter();
                            }
                            i++;
                        }
                    }
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(x =>
            {
                x.Span("الصفحة ");
                x.CurrentPageNumber();
                x.Span(" من ");
                x.TotalPages();
            });
        }

        public async Task GenerateBorrowHistoryExcelAsync(IEnumerable<BorrowRequest> requests, string filePath)
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("سجل الاستعارات");
                worksheet.RightToLeft = true;

                string[] headers = { "رقم المصدر", "المستعير", "الغرض", "تاريخ الإرجاع", "الحالة", "المسؤول", "تاريخ الطلب" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int row = 2;
                foreach (var req in requests)
                {
                    worksheet.Cell(row, 1).Value = req.Source?.SourceCode ?? "-";
                    worksheet.Cell(row, 2).Value = req.DisplayBorrowerName;
                    worksheet.Cell(row, 3).Value = req.Purpose ?? "-";
                    worksheet.Cell(row, 4).Value = req.ExpectedReturnDate.ToString("yyyy/MM/dd");
                    worksheet.Cell(row, 5).Value = req.ArabicStatus;
                    worksheet.Cell(row, 6).Value = req.AddedBy ?? "-";
                    worksheet.Cell(row, 7).Value = req.RequestDate.ToString("yyyy/MM/dd HH:mm");
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            });
        }

        public async Task GenerateBorrowHistoryPdfAsync(IEnumerable<BorrowRequest> requests, string filePath)
        {
             await Task.Run(() =>
             {
                 var list = requests?.ToList() ?? new List<BorrowRequest>();
                 string noDataText = GetNoDataText();

                 Document.Create(container =>
                 {
                     container.Page(page =>
                     {
                         page.Size(PageSizes.A4);
                         page.Margin(2, Unit.Centimetre);
                         page.PageColor(Colors.White);
                         page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));
                         page.ContentFromRightToLeft();

                         page.Header().Row(row =>
                         {
                             row.RelativeItem().Column(column =>
                             {
                                 column.Item().Text("منظومة مصادر — تقرير استعارة المصادر").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                                 column.Item().Text($"تاريخ التقرير: {DateTime.Now:yyyy/MM/dd}").FontSize(14).FontColor(Colors.Grey.Darken1);
                             });
                         });

                         page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                         {
                             column.Item().Table(table =>
                             {
                                 table.ColumnsDefinition(columns =>
                                 {
                                     columns.RelativeColumn(2); // المصدر
                                     columns.RelativeColumn(3); // المستعير
                                     columns.RelativeColumn(3); // الغرض
                                     columns.RelativeColumn(2); // تاريخ الإرجاع المتوقع
                                     columns.RelativeColumn(1.5f); // الحالة
                                     columns.RelativeColumn(2); // المسؤول
                                     columns.RelativeColumn(2); // تاريخ الطلب
                                 });

                                 table.Header(header =>
                                 {
                                     header.Cell().Element(HeaderStyle).Text("رقم المصدر");
                                     header.Cell().Element(HeaderStyle).Text("المستعير");
                                     header.Cell().Element(HeaderStyle).Text("الغرض");
                                     header.Cell().Element(HeaderStyle).Text("تاريخ الإرجاع");
                                     header.Cell().Element(HeaderStyle).Text("الحالة");
                                     header.Cell().Element(HeaderStyle).Text("المسؤول");
                                     header.Cell().Element(HeaderStyle).Text("تاريخ الطلب");

                                     static IContainer HeaderStyle(IContainer container)
                                     {
                                         return container.Background(Colors.Blue.Medium).PaddingVertical(8).AlignCenter().DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White));
                                     }
                                 });

                                 if (!list.Any())
                                 {
                                     table.Cell().ColumnSpan(7).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                                 }
                                 else
                                 {
                                     int i = 0;
                                     foreach (var req in list)
                                     {
                                         var backgroundColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                         
                                         table.Cell().Element(CellStyle).Text(req.Source?.SourceCode ?? "-");
                                         table.Cell().Element(CellStyle).Text(req.DisplayBorrowerName);
                                         table.Cell().Element(CellStyle).Text(req.Purpose ?? "-");
                                         table.Cell().Element(CellStyle).Text(req.ExpectedReturnDate.ToString("yyyy/MM/dd"));
                                         table.Cell().Element(CellStyle).Text(req.ArabicStatus);
                                         table.Cell().Element(CellStyle).Text(req.AddedBy ?? "-");
                                         table.Cell().Element(CellStyle).Text(req.RequestDate.ToString("yyyy/MM/dd"));

                                         IContainer CellStyle(IContainer container)
                                         {
                                             return container.Background(backgroundColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(6).AlignCenter();
                                         }
                                         i++;
                                     }
                                 }
                             });
                         });

                         page.Footer().Element(ComposeFooter);
                     });
                 }).GeneratePdf(filePath);
             });
        }

        public async Task GenerateCalibrationReportExcelAsync(IEnumerable<Source> sources, string filePath)
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("تنبيهات انخفاض النشاط");
                worksheet.RightToLeft = true;

                string[] headers = { "رقم المصدر", "النظير", "تاريخ آخر معايرة", "الحالة", "النشاط الحالي", "المسؤول" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.OrangeRed;
                    worksheet.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
                }

                int row = 2;
                foreach (var source in sources)
                {
                    worksheet.Cell(row, 1).Value = source.SourceCode;
                    worksheet.Cell(row, 2).Value = source.DisplayIsotopes;
                    worksheet.Cell(row, 3).Value = source.CalibrationDate.ToString("yyyy/MM/dd");
                    worksheet.Cell(row, 4).Value = source.ArabicStatus;
                    worksheet.Cell(row, 5).Value = source.CurrentActivityWithUnit;
                    worksheet.Cell(row, 6).Value = source.AddedBy ?? "-";
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            });
        }

        public async Task GenerateCalibrationReportPdfAsync(IEnumerable<Source> sources, string filePath)
        {
            await Task.Run(() =>
            {
                var list = sources?.ToList() ?? new List<Source>();
                string noDataText = GetNoDataText();

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));
                        page.ContentFromRightToLeft();

                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item().Text("منظومة مصادر — تقرير تنبيهات انخفاض النشاط").FontSize(22).SemiBold().FontColor(Colors.Red.Medium);
                                column.Item().Text($"تاريخ الاستخراج: {DateTime.Now:yyyy/MM/dd HH:mm}").FontSize(12).FontColor(Colors.Grey.Darken2);
                            });
                        });

                        page.Content().PaddingVertical(1, Unit.Centimetre).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(BlockStyle).Text("المصدر");
                                header.Cell().Element(BlockStyle).Text("النظير");
                                header.Cell().Element(BlockStyle).Text("تاريخ المعايرة");
                                header.Cell().Element(BlockStyle).Text("الحالة");
                                header.Cell().Element(BlockStyle).Text("المسؤول");

                                static IContainer BlockStyle(IContainer container) => container.Background(Colors.Red.Lighten4).Padding(5).BorderBottom(1).BorderColor(Colors.Red.Medium);
                            });

                            if (!list.Any())
                            {
                                table.Cell().ColumnSpan(5).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                            }
                            else
                            {
                                foreach (var s in list)
                                {
                                    table.Cell().Element(CellStyle).Text(s.SourceCode);
                                    table.Cell().Element(CellStyle).Text(s.DisplayIsotopes);
                                    table.Cell().Element(CellStyle).Text(s.CalibrationDate.ToString("yyyy/MM/dd"));
                                    table.Cell().Element(CellStyle).Text(s.ArabicStatus);
                                    table.Cell().Element(CellStyle).Text(s.AddedBy ?? "-");

                                    static IContainer CellStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4);
                                }
                            }
                        });

                        page.Footer().Element(ComposeFooter);
                    });
                }).GeneratePdf(filePath);
            });
        }
        public async Task GenerateGeneralReportExcelAsync(IEnumerable<Source> inventory, IEnumerable<BorrowRequest> borrowing, IEnumerable<Source> lowActivity, IEnumerable<Source> calibration, string filePath)
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                
                // 1. Inventory Sheet
                var wsInventory = workbook.Worksheets.Add("جرد المصادر");
                wsInventory.RightToLeft = true;
                string[] invHeaders = { "رقم المصدر", "النظير", "النشاط الحالي", "الموقع", "الحالة", "أُضيف بواسطة" };
                for (int i = 0; i < invHeaders.Length; i++)
                {
                    wsInventory.Cell(1, i + 1).Value = invHeaders[i];
                    wsInventory.Cell(1, i + 1).Style.Font.Bold = true;
                    wsInventory.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                int row = 2;
                foreach (var s in inventory)
                {
                    wsInventory.Cell(row, 1).Value = s.SourceCode;
                    wsInventory.Cell(row, 2).Value = s.DisplayIsotopes;
                    wsInventory.Cell(row, 3).Value = s.CurrentActivityWithUnit;
                    wsInventory.Cell(row, 4).Value = s.Location?.LocationName ?? "غير محدد";
                    wsInventory.Cell(row, 5).Value = s.ArabicStatus;
                    wsInventory.Cell(row, 6).Value = s.AddedBy ?? "غير معروف";
                    row++;
                }
                wsInventory.Columns().AdjustToContents();

                // 2. Borrowing Sheet
                var wsBorrowing = workbook.Worksheets.Add("سجل الاستعارات");
                wsBorrowing.RightToLeft = true;
                string[] borHeaders = { "رقم المصدر", "المستعير", "الغرض", "تاريخ الإرجاع", "الحالة", "المسؤول", "تاريخ الطلب" };
                for (int i = 0; i < borHeaders.Length; i++)
                {
                    wsBorrowing.Cell(1, i + 1).Value = borHeaders[i];
                    wsBorrowing.Cell(1, i + 1).Style.Font.Bold = true;
                    wsBorrowing.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                row = 2;
                foreach (var req in borrowing)
                {
                    wsBorrowing.Cell(row, 1).Value = req.Source?.SourceCode ?? "-";
                    wsBorrowing.Cell(row, 2).Value = req.DisplayBorrowerName;
                    wsBorrowing.Cell(row, 3).Value = req.Purpose ?? "-";
                    wsBorrowing.Cell(row, 4).Value = req.ExpectedReturnDate.ToString("yyyy/MM/dd");
                    wsBorrowing.Cell(row, 5).Value = req.ArabicStatus;
                    wsBorrowing.Cell(row, 6).Value = req.AddedBy ?? "-";
                    wsBorrowing.Cell(row, 7).Value = req.RequestDate.ToString("yyyy/MM/dd HH:mm");
                    row++;
                }
                wsBorrowing.Columns().AdjustToContents();

                // 3. Low Activity Sheet
                var wsLowAct = workbook.Worksheets.Add("المصادر منخفضة النشاط");
                wsLowAct.RightToLeft = true;
                string[] lowActHeaders = { "رقم المصدر", "النظير", "النشاط الحالي", "الموقع", "الحالة", "أُضيف بواسطة" };
                for (int i = 0; i < lowActHeaders.Length; i++)
                {
                    wsLowAct.Cell(1, i + 1).Value = lowActHeaders[i];
                    wsLowAct.Cell(1, i + 1).Style.Font.Bold = true;
                    wsLowAct.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.YellowGreen;
                }
                row = 2;
                foreach (var s in lowActivity)
                {
                    wsLowAct.Cell(row, 1).Value = s.SourceCode;
                    wsLowAct.Cell(row, 2).Value = s.DisplayIsotopes;
                    wsLowAct.Cell(row, 3).Value = s.CurrentActivityWithUnit;
                    wsLowAct.Cell(row, 4).Value = s.Location?.LocationName ?? "غير محدد";
                    wsLowAct.Cell(row, 5).Value = s.ArabicStatus;
                    wsLowAct.Cell(row, 6).Value = s.AddedBy ?? "غير معروف";
                    row++;
                }
                wsLowAct.Columns().AdjustToContents();

                // 4. Low Activity Alerts Sheet
                var wsCalib = workbook.Worksheets.Add("تنبيهات انخفاض النشاط");
                wsCalib.RightToLeft = true;
                string[] calibHeaders = { "رقم المصدر", "النظير", "تاريخ آخر معايرة", "الحالة", "النشاط الحالي", "المسؤول" };
                for (int i = 0; i < calibHeaders.Length; i++)
                {
                    wsCalib.Cell(1, i + 1).Value = calibHeaders[i];
                    wsCalib.Cell(1, i + 1).Style.Font.Bold = true;
                    wsCalib.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.OrangeRed;
                    wsCalib.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
                }
                row = 2;
                foreach (var s in calibration)
                {
                    wsCalib.Cell(row, 1).Value = s.SourceCode;
                    wsCalib.Cell(row, 2).Value = s.DisplayIsotopes;
                    wsCalib.Cell(row, 3).Value = s.CalibrationDate.ToString("yyyy/MM/dd");
                    wsCalib.Cell(row, 4).Value = s.ArabicStatus;
                    wsCalib.Cell(row, 5).Value = s.CurrentActivityWithUnit;
                    wsCalib.Cell(row, 6).Value = s.AddedBy ?? "-";
                    row++;
                }
                wsCalib.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            });
        }

        public async Task GenerateGeneralReportPdfAsync(IEnumerable<Source> inventory, IEnumerable<BorrowRequest> borrowing, IEnumerable<Source> lowActivity, IEnumerable<Source> calibration, string filePath)
        {
            await Task.Run(() =>
            {
                var borrowList = borrowing?.ToList() ?? new List<BorrowRequest>();
                var calibList = calibration?.ToList() ?? new List<Source>();
                string noDataText = GetNoDataText();

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));
                        page.ContentFromRightToLeft();

                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item().Text("منظومة مصادر — التقرير العام الشامل").FontSize(22).SemiBold().FontColor(Colors.Blue.Darken2);
                                column.Item().Text($"تاريخ الاستخراج: {DateTime.Now:yyyy/MM/dd HH:mm}").FontSize(12).FontColor(Colors.Grey.Darken2);
                                column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                            });
                        });

                        page.Content().Column(column =>
                        {
                            // 1. Inventory Section
                            column.Item().PaddingVertical(10).Text("1. تقرير جرد المصادر والمواد المشعة").FontSize(16).SemiBold().FontColor(Colors.Blue.Medium);
                            column.Item().Element(c => ComposeContentInventory(c, inventory));

                            // 2. Borrowing Section
                            column.Item().PaddingTop(20).PaddingBottom(10).Text("2. تقرير سجل الاستعارات").FontSize(16).SemiBold().FontColor(Colors.Blue.Medium);
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2); // المصدر
                                    columns.RelativeColumn(3); // المستعير
                                    columns.RelativeColumn(3); // الغرض
                                    columns.RelativeColumn(2); // تاريخ الإرجاع
                                    columns.RelativeColumn(1.5f); // الحالة
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderStyle).Text("رقم المصدر");
                                    header.Cell().Element(HeaderStyle).Text("المستعير");
                                    header.Cell().Element(HeaderStyle).Text("الغرض");
                                    header.Cell().Element(HeaderStyle).Text("تاريخ الإرجاع");
                                    header.Cell().Element(HeaderStyle).Text("الحالة");
                                    static IContainer HeaderStyle(IContainer c) => c.Background(Colors.Blue.Medium).PaddingVertical(6).AlignCenter().DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White));
                                });

                                if (!borrowList.Any())
                                {
                                    table.Cell().ColumnSpan(5).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                                }
                                else
                                {
                                    int i = 0;
                                    foreach (var req in borrowList)
                                    {
                                        var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(req.Source?.SourceCode ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(req.DisplayBorrowerName);
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(req.Purpose ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(req.ExpectedReturnDate.ToString("yyyy/MM/dd"));
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(req.ArabicStatus);
                                        static IContainer CellStyle(IContainer c, string bg) => c.Background(bg).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).AlignCenter();
                                        i++;
                                    }
                                }
                            });

                            // 3. Low Activity Section
                            column.Item().PaddingTop(20).PaddingBottom(10).Text("3. تقرير المصادر منخفضة النشاط الإشعاعي").FontSize(16).SemiBold().FontColor(Colors.Blue.Medium);
                            column.Item().Element(c => ComposeContentInventory(c, lowActivity)); // Reuses inventory table format with empty check

                            // 4. Calibration / Low Activity Alert Section
                            column.Item().PaddingTop(20).PaddingBottom(10).Text("4. تنبيهات انخفاض النشاط").FontSize(16).SemiBold().FontColor(Colors.Red.Medium);
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderStyle).Text("المصدر");
                                    header.Cell().Element(HeaderStyle).Text("النظير");
                                    header.Cell().Element(HeaderStyle).Text("تاريخ المعايرة");
                                    header.Cell().Element(HeaderStyle).Text("النشاط الحالي");
                                    static IContainer HeaderStyle(IContainer c) => c.Background(Colors.Red.Lighten4).Padding(5).BorderBottom(1).BorderColor(Colors.Red.Medium);
                                });

                                if (!calibList.Any())
                                {
                                    table.Cell().ColumnSpan(4).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                                }
                                else
                                {
                                    foreach (var s in calibList)
                                    {
                                        table.Cell().Element(CellStyle).Text(s.SourceCode);
                                        table.Cell().Element(CellStyle).Text(s.DisplayIsotopes);
                                        table.Cell().Element(CellStyle).Text(s.CalibrationDate.ToString("yyyy/MM/dd"));
                                        table.Cell().Element(CellStyle).Text(s.CurrentActivityWithUnit);
                                        static IContainer CellStyle(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4);
                                    }
                                }
                            });
                        });

                        page.Footer().Element(ComposeFooter);
                    });
                }).GeneratePdf(filePath);
            });
        }

        public async Task GenerateUsersReportExcelAsync(IEnumerable<User> users, string filePath)
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("المستخدمين والكوادر");
                worksheet.RightToLeft = true;

                string[] headers = { "#", "الاسم الكامل", "اسم المستخدم", "الدور / الصلاحية", "البريد الإلكتروني", "الحالة", "حالة القفل", "آخر تسجيل دخول" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int row = 2;
                int index = 1;
                foreach (var u in users ?? Enumerable.Empty<User>())
                {
                    worksheet.Cell(row, 1).Value = index++;
                    worksheet.Cell(row, 2).Value = u.FullName;
                    worksheet.Cell(row, 3).Value = u.Username;
                    worksheet.Cell(row, 4).Value = u.Role?.DisplayName ?? "-";
                    worksheet.Cell(row, 5).Value = u.Email ?? "-";
                    worksheet.Cell(row, 6).Value = u.StatusDisplayName;
                    worksheet.Cell(row, 7).Value = (u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTime.Now) ? "مقفل مؤقتاً" : "طبيعي";
                    worksheet.Cell(row, 8).Value = u.LastLoginDate.HasValue ? u.LastLoginDate.Value.ToString("yyyy/MM/dd HH:mm") : "لم يسجل بعد";
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            });
        }

        public async Task GenerateUsersReportPdfAsync(IEnumerable<User> users, string filePath)
        {
            await Task.Run(() =>
            {
                var list = users?.ToList() ?? new List<User>();
                string noDataText = GetNoDataText();

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));
                        page.ContentFromRightToLeft();

                        page.Header().Element(c => ComposeHeader(c, "منظومة مصادر — تقرير الكوادر والمستخدمين"));

                        page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1); // #
                                    columns.RelativeColumn(3); // Full Name
                                    columns.RelativeColumn(2.5f); // Username
                                    columns.RelativeColumn(2.5f); // Role
                                    columns.RelativeColumn(3); // Email
                                    columns.RelativeColumn(2); // Status
                                    columns.RelativeColumn(2.5f); // Last Login
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderStyle).Text("#");
                                    header.Cell().Element(HeaderStyle).Text("الاسم الكامل");
                                    header.Cell().Element(HeaderStyle).Text("اسم المستخدم");
                                    header.Cell().Element(HeaderStyle).Text("الدور");
                                    header.Cell().Element(HeaderStyle).Text("البريد الإلكتروني");
                                    header.Cell().Element(HeaderStyle).Text("الحالة");
                                    header.Cell().Element(HeaderStyle).Text("آخر تسجيل دخول");

                                    static IContainer HeaderStyle(IContainer c) => c.Background(Colors.Blue.Medium).PaddingVertical(6).AlignCenter().DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White));
                                });

                                if (!list.Any())
                                {
                                    table.Cell().ColumnSpan(7).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                                }
                                else
                                {
                                    int i = 1;
                                    foreach (var u in list)
                                    {
                                        var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(i.ToString());
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(u.FullName);
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(u.Username);
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(u.Role?.DisplayName ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(u.Email ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(u.StatusDisplayName);
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(u.LastLoginDate.HasValue ? u.LastLoginDate.Value.ToString("yyyy/MM/dd HH:mm") : "-");
                                        i++;
                                    }
                                }

                                static IContainer CellStyle(IContainer c, string bg) => c.Background(bg).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).AlignCenter();
                            });
                        });

                        page.Footer().Element(ComposeFooter);
                    });
                }).GeneratePdf(filePath);
            });
        }

        public async Task GenerateAuditLogsExcelAsync(IEnumerable<AuditLog> logs, string filePath)
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("سجل التدقيق والنشاطات");
                worksheet.RightToLeft = true;

                string[] headers = { "#", "المستخدم", "نوع العملية", "الجدول المتأثر", "التفاصيل", "التاريخ والوقت" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int row = 2;
                int index = 1;
                foreach (var log in logs ?? Enumerable.Empty<AuditLog>())
                {
                    worksheet.Cell(row, 1).Value = index++;
                    worksheet.Cell(row, 2).Value = log.User?.FullName ?? "مدير النظام / تلقائي";
                    worksheet.Cell(row, 3).Value = log.Action;
                    worksheet.Cell(row, 4).Value = log.TableName ?? "-";
                    worksheet.Cell(row, 5).Value = log.Details ?? "-";
                    worksheet.Cell(row, 6).Value = log.ActionDate.ToString("yyyy/MM/dd HH:mm:ss");
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            });
        }

        public async Task GenerateAuditLogsPdfAsync(IEnumerable<AuditLog> logs, string filePath)
        {
            await Task.Run(() =>
            {
                var list = logs?.ToList() ?? new List<AuditLog>();
                string noDataText = GetNoDataText();

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));
                        page.ContentFromRightToLeft();

                        page.Header().Element(c => ComposeHeader(c, "منظومة مصادر — تقرير سجل التدقيق والنشاطات"));

                        page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1); // #
                                    columns.RelativeColumn(2.5f); // User
                                    columns.RelativeColumn(2); // Action
                                    columns.RelativeColumn(2); // Table
                                    columns.RelativeColumn(4.5f); // Details
                                    columns.RelativeColumn(2.5f); // Date
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderStyle).Text("#");
                                    header.Cell().Element(HeaderStyle).Text("المستخدم");
                                    header.Cell().Element(HeaderStyle).Text("العملية");
                                    header.Cell().Element(HeaderStyle).Text("الجدول");
                                    header.Cell().Element(HeaderStyle).Text("التفاصيل");
                                    header.Cell().Element(HeaderStyle).Text("التاريخ والوقت");

                                    static IContainer HeaderStyle(IContainer c) => c.Background(Colors.Blue.Medium).PaddingVertical(6).AlignCenter().DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White));
                                });

                                if (!list.Any())
                                {
                                    table.Cell().ColumnSpan(6).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                                }
                                else
                                {
                                    int i = 1;
                                    foreach (var log in list)
                                    {
                                        var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(i.ToString());
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(log.User?.FullName ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(log.Action);
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(log.TableName ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(log.Details ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(log.ActionDate.ToString("yyyy/MM/dd HH:mm"));
                                        i++;
                                    }
                                }

                                static IContainer CellStyle(IContainer c, string bg) => c.Background(bg).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).AlignCenter();
                            });
                        });

                        page.Footer().Element(ComposeFooter);
                    });
                }).GeneratePdf(filePath);
            });
        }
    }
}
