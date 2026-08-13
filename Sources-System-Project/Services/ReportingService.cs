using ClosedXML.Excel;
using Sources.Models;
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

        public async Task GenerateInventoryReportExcelAsync(IEnumerable<Source> sources, string filePath, string reportTitle)
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add(reportTitle);
                
                worksheet.RightToLeft = true;

                // Headers
                string[] headers = { "رقم المصدر", "النظير", "النشاط الحالي", "المسار / الموقع", "الحالة", "أُضيف بواسطة" };
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

                    int i = 0;
                    foreach (var source in sources)
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
                    string borrower = !string.IsNullOrEmpty(req.BorrowerName) ? req.BorrowerName : (req.BorrowerUser?.FullName ?? "-");
                    worksheet.Cell(row, 1).Value = req.Source?.SourceCode ?? "-";
                    worksheet.Cell(row, 2).Value = borrower;
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
                                 column.Item().Text("تقرير استعارة المصادر").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
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

                                 int i = 0;
                                 foreach (var req in requests)
                                 {
                                     var backgroundColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                     string borrower = !string.IsNullOrEmpty(req.BorrowerName) ? req.BorrowerName : (req.BorrowerUser?.FullName ?? "-");
                                     
                                     table.Cell().Element(CellStyle).Text(req.Source?.SourceCode ?? "-");
                                     table.Cell().Element(CellStyle).Text(borrower);
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
                var worksheet = workbook.Worksheets.Add("تنبيهات المعايرة");
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
                                column.Item().Text("نظام مسار - تقرير تنبيهات المعايرة").FontSize(22).SemiBold().FontColor(Colors.Red.Medium);
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

                            foreach (var s in sources)
                            {
                                table.Cell().Element(CellStyle).Text(s.SourceCode);
                                table.Cell().Element(CellStyle).Text(s.DisplayIsotopes);
                                table.Cell().Element(CellStyle).Text(s.CalibrationDate.ToString("yyyy/MM/dd"));
                                table.Cell().Element(CellStyle).Text(s.ArabicStatus);
                                table.Cell().Element(CellStyle).Text(s.AddedBy ?? "-");

                                static IContainer CellStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4);
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
                string[] invHeaders = { "رقم المصدر", "النظير", "النشاط الحالي", "المسار / الموقع", "الحالة", "أُضيف بواسطة" };
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
                    string borrower = !string.IsNullOrEmpty(req.BorrowerName) ? req.BorrowerName : (req.BorrowerUser?.FullName ?? "-");
                    wsBorrowing.Cell(row, 1).Value = req.Source?.SourceCode ?? "-";
                    wsBorrowing.Cell(row, 2).Value = borrower;
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
                string[] lowActHeaders = { "رقم المصدر", "النظير", "النشاط الحالي", "المسار / الموقع", "الحالة", "أُضيف بواسطة" };
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

                // 4. Calibration Sheet
                var wsCalib = workbook.Worksheets.Add("تنبيهات المعايرة");
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
                                column.Item().Text("نظام مسار - التقرير العام الشامل").FontSize(22).SemiBold().FontColor(Colors.Blue.Darken2);
                                column.Item().Text($"تاريخ الاستخراج: {DateTime.Now:yyyy/MM/dd HH:mm}").FontSize(12).FontColor(Colors.Grey.Darken2);
                                column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                            });
                        });

                        page.Content().Column(column =>
                        {
                            // 1. Inventory Section
                            column.Item().PaddingVertical(10).Text("1. تقرير جرد المصادر والمواد المشعة").FontSize(16).SemiBold().FontColor(Colors.Blue.Medium);
                            column.Item().Element(c => ComposeContentInventory(c, inventory));

                            // Page Break before next section if desired, or just let it flow.
                            // We will let QuestPDF handle page breaks naturally, just add some spacing.

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
                                int i = 0;
                                foreach (var req in borrowing)
                                {
                                    var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                    string borrower = !string.IsNullOrEmpty(req.BorrowerName) ? req.BorrowerName : (req.BorrowerUser?.FullName ?? "-");
                                    table.Cell().Element(c => CellStyle(c, bg)).Text(req.Source?.SourceCode ?? "-");
                                    table.Cell().Element(c => CellStyle(c, bg)).Text(borrower);
                                    table.Cell().Element(c => CellStyle(c, bg)).Text(req.Purpose ?? "-");
                                    table.Cell().Element(c => CellStyle(c, bg)).Text(req.ExpectedReturnDate.ToString("yyyy/MM/dd"));
                                    table.Cell().Element(c => CellStyle(c, bg)).Text(req.ArabicStatus);
                                    static IContainer CellStyle(IContainer c, string bg) => c.Background(bg).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).AlignCenter();
                                    i++;
                                }
                            });

                            // 3. Low Activity Section
                            column.Item().PaddingTop(20).PaddingBottom(10).Text("3. تقرير المصادر منخفضة النشاط الإشعاعي").FontSize(16).SemiBold().FontColor(Colors.Blue.Medium);
                            column.Item().Element(c => ComposeContentInventory(c, lowActivity)); // Can reuse inventory format

                            // 4. Calibration Section
                            column.Item().PaddingTop(20).PaddingBottom(10).Text("4. تنبيهات المعايرة").FontSize(16).SemiBold().FontColor(Colors.Red.Medium);
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
                                foreach (var s in calibration)
                                {
                                    table.Cell().Element(CellStyle).Text(s.SourceCode);
                                    table.Cell().Element(CellStyle).Text(s.DisplayIsotopes);
                                    table.Cell().Element(CellStyle).Text(s.CalibrationDate.ToString("yyyy/MM/dd"));
                                    table.Cell().Element(CellStyle).Text(s.CurrentActivityWithUnit);
                                    static IContainer CellStyle(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4);
                                }
                            });
                        });

                        page.Footer().Element(ComposeFooter);
                    });
                }).GeneratePdf(filePath);
            });
        }
    }
}
