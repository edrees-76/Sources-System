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

        private static readonly char[] InvalidSheetChars = { '\\', '/', '?', '*', '[', ']', ':' };

        public static string SanitizeSheetName(string? name, string defaultName = "تقرير")
        {
            if (string.IsNullOrWhiteSpace(name))
                return defaultName;

            // 1. Remove invalid characters: \ / ? * [ ] :
            var clean = new string(name.Where(c => !InvalidSheetChars.Contains(c) && !char.IsControl(c)).ToArray()).Trim();

            // 2. Remove single quotes from start and end
            clean = clean.Trim('\'').Trim();

            if (string.IsNullOrWhiteSpace(clean))
                clean = defaultName;

            // 3. Truncate to maximum 31 characters
            if (clean.Length > 31)
            {
                clean = clean.Substring(0, 31).Trim();
            }

            return string.IsNullOrWhiteSpace(clean) ? defaultName : clean;
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

        public async Task GenerateLocationsReportExcelAsync(IEnumerable<Location> locations, string filePath)
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("المواقع والمخازن");
                worksheet.RightToLeft = true;

                string[] headers = { "#", "اسم الموقع", "النوع", "المبنى", "الغرفة", "المسؤول", "عدد المصادر", "أُضيف بواسطة" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int row = 2;
                int index = 1;
                foreach (var loc in locations ?? Enumerable.Empty<Location>())
                {
                    worksheet.Cell(row, 1).Value = index++;
                    worksheet.Cell(row, 2).Value = loc.LocationName;
                    worksheet.Cell(row, 3).Value = loc.LocationType switch
                    {
                        "Lab" => "مختبر",
                        "Storage" => "مستودع / مخزن",
                        "Hospital" => "مستشفى",
                        "Clinic" => "عيادة",
                        _ => loc.LocationType ?? "-"
                    };
                    worksheet.Cell(row, 4).Value = loc.Building ?? "-";
                    worksheet.Cell(row, 5).Value = loc.Room ?? "-";
                    worksheet.Cell(row, 6).Value = loc.ResponsiblePerson ?? "-";
                    worksheet.Cell(row, 7).Value = loc.SourceCount;
                    worksheet.Cell(row, 8).Value = loc.AddedBy ?? "-";
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            });
        }

        public async Task GenerateLocationsReportPdfAsync(IEnumerable<Location> locations, string filePath)
        {
            await Task.Run(() =>
            {
                var list = locations?.ToList() ?? new List<Location>();
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

                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item().Text("منظومة مصادر — تقرير المواقع والمخازن").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                                column.Item().Text($"تاريخ التقرير: {DateTime.Now:yyyy/MM/dd}").FontSize(12).FontColor(Colors.Grey.Darken1);
                                column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                            });
                        });

                        page.Content().PaddingVertical(0.5f, Unit.Centimetre).Column(column =>
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1); // #
                                    columns.RelativeColumn(3); // اسم الموقع
                                    columns.RelativeColumn(2); // النوع
                                    columns.RelativeColumn(2); // المبنى
                                    columns.RelativeColumn(2); // الغرفة
                                    columns.RelativeColumn(2.5f); // المسؤول
                                    columns.RelativeColumn(1.8f); // عدد المصادر
                                    columns.RelativeColumn(2); // أُضيف بواسطة
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderStyle).Text("#");
                                    header.Cell().Element(HeaderStyle).Text("اسم الموقع");
                                    header.Cell().Element(HeaderStyle).Text("النوع");
                                    header.Cell().Element(HeaderStyle).Text("المبنى");
                                    header.Cell().Element(HeaderStyle).Text("الغرفة");
                                    header.Cell().Element(HeaderStyle).Text("المسؤول");
                                    header.Cell().Element(HeaderStyle).Text("عدد المصادر");
                                    header.Cell().Element(HeaderStyle).Text("أُضيف بواسطة");

                                    static IContainer HeaderStyle(IContainer c) => c.Background(Colors.Blue.Medium).PaddingVertical(6).AlignCenter().DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White));
                                });

                                if (!list.Any())
                                {
                                    table.Cell().ColumnSpan(8).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                                }
                                else
                                {
                                    int i = 1;
                                    foreach (var loc in list)
                                    {
                                        var bg = (i - 1) % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                        string typeDisplay = loc.LocationType switch
                                        {
                                            "Lab" => "مختبر",
                                            "Storage" => "مستودع / مخزن",
                                            "Hospital" => "مستشفى",
                                            "Clinic" => "عيادة",
                                            _ => loc.LocationType ?? "-"
                                        };

                                        table.Cell().Element(c => CellStyle(c, bg)).Text(i.ToString());
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(loc.LocationName);
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(typeDisplay);
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(loc.Building ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(loc.Room ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(loc.ResponsiblePerson ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(loc.SourceCount.ToString());
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(loc.AddedBy ?? "-");
                                        i++;
                                    }
                                }

                                static IContainer CellStyle(IContainer c, string bg) => c.Background(bg).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).AlignCenter();
                            });
                        });

                        page.Footer().Element(ComposeFooter);
                    });
                }).GeneratePdf(filePath);
            });
        }

        public async Task GenerateInventoryReportExcelAsync(IEnumerable<Source> sources, string filePath, string reportTitle)
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var sheetName = SanitizeSheetName(reportTitle, "جرد المصادر");
                var worksheet = workbook.Worksheets.Add(sheetName);
                
                worksheet.RightToLeft = true;

                // Headers
                string[] headers = { "#", "رقم المصدر", "النظير", "النشاط الحالي", "الموقع", "الحالة", "أُضيف بواسطة" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int row = 2;
                int index = 1;
                foreach (var source in sources ?? Enumerable.Empty<Source>())
                {
                    worksheet.Cell(row, 1).Value = index++;
                    worksheet.Cell(row, 2).Value = source.SourceCode;
                    worksheet.Cell(row, 3).Value = source.DisplayIsotopes;
                    worksheet.Cell(row, 4).Value = source.CurrentActivityWithUnit;
                    worksheet.Cell(row, 5).Value = source.Location?.LocationName ?? "غير محدد";
                    worksheet.Cell(row, 6).Value = source.ArabicStatus;
                    worksheet.Cell(row, 7).Value = source.AddedBy ?? "غير معروف";
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
                        columns.RelativeColumn(1); // #
                        columns.RelativeColumn(2.5f); // رقم المصدر
                        columns.RelativeColumn(2.5f); // النظير
                        columns.RelativeColumn(3); // النشاط الحالي
                        columns.RelativeColumn(3); // الموقع
                        columns.RelativeColumn(2); // الحالة
                        columns.RelativeColumn(2); // أُضيف بواسطة
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderStyle).Text("#");
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
                        table.Cell().ColumnSpan(7).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        int i = 1;
                        foreach (var source in list)
                        {
                            var backgroundColor = (i - 1) % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                            
                            table.Cell().Element(CellStyle).Text(i.ToString());
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

                string[] headers = { "#", "رقم المصدر", "المستعير", "الغرض", "تاريخ الإرجاع", "الحالة", "المسؤول", "تاريخ الطلب" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int row = 2;
                int index = 1;
                foreach (var req in requests ?? Enumerable.Empty<BorrowRequest>())
                {
                    worksheet.Cell(row, 1).Value = index++;
                    worksheet.Cell(row, 2).Value = req.DisplaySourceCode;
                    worksheet.Cell(row, 3).Value = req.DisplayBorrowerName;
                    worksheet.Cell(row, 4).Value = req.Purpose ?? "-";
                    worksheet.Cell(row, 5).Value = req.ExpectedReturnDate.ToString("yyyy/MM/dd");
                    worksheet.Cell(row, 6).Value = req.ArabicStatus;
                    worksheet.Cell(row, 7).Value = req.AddedBy ?? "-";
                    worksheet.Cell(row, 8).Value = req.RequestDate.ToString("yyyy/MM/dd HH:mm");
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
                                     columns.RelativeColumn(1); // #
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
                                     header.Cell().Element(HeaderStyle).Text("#");
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
                                     table.Cell().ColumnSpan(8).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                                 }
                                 else
                                 {
                                     int i = 1;
                                     foreach (var req in list)
                                     {
                                         var backgroundColor = (i - 1) % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                         
                                         table.Cell().Element(CellStyle).Text(i.ToString());
                                         table.Cell().Element(CellStyle).Text(req.DisplaySourceCode);
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

        public async Task GenerateLowActivityAlertReportExcelAsync(IEnumerable<Source> sources, string filePath)
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("تنبيهات انخفاض النشاط");
                worksheet.RightToLeft = true;

                string[] headers = { "#", "رقم المصدر", "النظير", "الخطورة", "تاريخ آخر معايرة", "الحالة", "النشاط الحالي", "المسؤول" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.OrangeRed;
                    worksheet.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
                }

                int row = 2;
                int index = 1;
                foreach (var source in sources ?? Enumerable.Empty<Source>())
                {
                    worksheet.Cell(row, 1).Value = index++;
                    worksheet.Cell(row, 2).Value = source.SourceCode;
                    worksheet.Cell(row, 3).Value = source.AlertWorstIsotope ?? source.DisplayIsotopes;
                    worksheet.Cell(row, 4).Value = source.AlertSeverityDisplay;
                    worksheet.Cell(row, 5).Value = source.CalibrationDate.ToString("yyyy/MM/dd");
                    worksheet.Cell(row, 6).Value = source.ArabicStatus;
                    worksheet.Cell(row, 7).Value = source.CurrentActivityWithUnit;
                    worksheet.Cell(row, 8).Value = source.AddedBy ?? "-";
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            });
        }

        public async Task GenerateLowActivityAlertReportPdfAsync(IEnumerable<Source> sources, string filePath)
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
                                columns.RelativeColumn(1); // #
                                columns.RelativeColumn(2.5f);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.8f);
                                columns.RelativeColumn(2.2f);
                                columns.RelativeColumn(2.2f);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(BlockStyle).Text("#");
                                header.Cell().Element(BlockStyle).Text("المصدر");
                                header.Cell().Element(BlockStyle).Text("النظير");
                                header.Cell().Element(BlockStyle).Text("الخطورة");
                                header.Cell().Element(BlockStyle).Text("تاريخ المعايرة");
                                header.Cell().Element(BlockStyle).Text("النشاط الحالي");
                                header.Cell().Element(BlockStyle).Text("الحالة");
                                header.Cell().Element(BlockStyle).Text("المسؤول");

                                static IContainer BlockStyle(IContainer container) => container.Background(Colors.Red.Lighten4).Padding(5).BorderBottom(1).BorderColor(Colors.Red.Medium);
                            });

                            if (!list.Any())
                            {
                                table.Cell().ColumnSpan(8).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                            }
                            else
                            {
                                int i = 1;
                                foreach (var s in list)
                                {
                                    table.Cell().Element(CellStyle).Text(i.ToString());
                                    table.Cell().Element(CellStyle).Text(s.SourceCode);
                                    table.Cell().Element(CellStyle).Text(s.AlertWorstIsotope ?? s.DisplayIsotopes);
                                    table.Cell().Element(CellStyle).Text(s.AlertSeverityDisplay);
                                    table.Cell().Element(CellStyle).Text(s.CalibrationDate.ToString("yyyy/MM/dd"));
                                    table.Cell().Element(CellStyle).Text(s.CurrentActivityWithUnit);
                                    table.Cell().Element(CellStyle).Text(s.ArabicStatus);
                                    table.Cell().Element(CellStyle).Text(s.AddedBy ?? "-");

                                    static IContainer CellStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4);
                                    i++;
                                }
                            }
                        });

                        page.Footer().Element(ComposeFooter);
                    });
                }).GeneratePdf(filePath);
            });
        }

        public async Task GenerateGeneralReportExcelAsync(IEnumerable<Source> inventory, IEnumerable<BorrowRequest> borrowing, IEnumerable<Source> lowActivity, IEnumerable<Source> lowActivityAlerts, string filePath)
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                
                // 1. Inventory Sheet
                var wsInventory = workbook.Worksheets.Add("جرد المصادر");
                wsInventory.RightToLeft = true;
                string[] invHeaders = { "#", "رقم المصدر", "النظير", "النشاط الحالي", "الموقع", "الحالة", "أُضيف بواسطة" };
                for (int i = 0; i < invHeaders.Length; i++)
                {
                    wsInventory.Cell(1, i + 1).Value = invHeaders[i];
                    wsInventory.Cell(1, i + 1).Style.Font.Bold = true;
                    wsInventory.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                int row = 2;
                int invIndex = 1;
                foreach (var s in inventory ?? Enumerable.Empty<Source>())
                {
                    wsInventory.Cell(row, 1).Value = invIndex++;
                    wsInventory.Cell(row, 2).Value = s.SourceCode;
                    wsInventory.Cell(row, 3).Value = s.DisplayIsotopes;
                    wsInventory.Cell(row, 4).Value = s.CurrentActivityWithUnit;
                    wsInventory.Cell(row, 5).Value = s.Location?.LocationName ?? "غير محدد";
                    wsInventory.Cell(row, 6).Value = s.ArabicStatus;
                    wsInventory.Cell(row, 7).Value = s.AddedBy ?? "غير معروف";
                    row++;
                }
                wsInventory.Columns().AdjustToContents();

                // 2. Borrowing Sheet
                var wsBorrowing = workbook.Worksheets.Add("سجل الاستعارات");
                wsBorrowing.RightToLeft = true;
                string[] borHeaders = { "#", "رقم المصدر", "المستعير", "الغرض", "تاريخ الإرجاع", "الحالة", "المسؤول", "تاريخ الطلب" };
                for (int i = 0; i < borHeaders.Length; i++)
                {
                    wsBorrowing.Cell(1, i + 1).Value = borHeaders[i];
                    wsBorrowing.Cell(1, i + 1).Style.Font.Bold = true;
                    wsBorrowing.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                row = 2;
                int borIndex = 1;
                foreach (var req in borrowing ?? Enumerable.Empty<BorrowRequest>())
                {
                    wsBorrowing.Cell(row, 1).Value = borIndex++;
                    wsBorrowing.Cell(row, 2).Value = req.DisplaySourceCode;
                    wsBorrowing.Cell(row, 3).Value = req.DisplayBorrowerName;
                    wsBorrowing.Cell(row, 4).Value = req.Purpose ?? "-";
                    wsBorrowing.Cell(row, 5).Value = req.ExpectedReturnDate.ToString("yyyy/MM/dd");
                    wsBorrowing.Cell(row, 6).Value = req.ArabicStatus;
                    wsBorrowing.Cell(row, 7).Value = req.AddedBy ?? "-";
                    wsBorrowing.Cell(row, 8).Value = req.RequestDate.ToString("yyyy/MM/dd HH:mm");
                    row++;
                }
                wsBorrowing.Columns().AdjustToContents();

                // 3. Low Activity Sheet
                var wsLowAct = workbook.Worksheets.Add("المصادر منخفضة النشاط");
                wsLowAct.RightToLeft = true;
                string[] lowActHeaders = { "#", "رقم المصدر", "النظير", "النشاط الحالي", "الموقع", "الحالة", "أُضيف بواسطة" };
                for (int i = 0; i < lowActHeaders.Length; i++)
                {
                    wsLowAct.Cell(1, i + 1).Value = lowActHeaders[i];
                    wsLowAct.Cell(1, i + 1).Style.Font.Bold = true;
                    wsLowAct.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.YellowGreen;
                }
                row = 2;
                int lowIndex = 1;
                foreach (var s in lowActivity ?? Enumerable.Empty<Source>())
                {
                    wsLowAct.Cell(row, 1).Value = lowIndex++;
                    wsLowAct.Cell(row, 2).Value = s.SourceCode;
                    wsLowAct.Cell(row, 3).Value = s.DisplayIsotopes;
                    wsLowAct.Cell(row, 4).Value = s.CurrentActivityWithUnit;
                    wsLowAct.Cell(row, 5).Value = s.Location?.LocationName ?? "غير محدد";
                    wsLowAct.Cell(row, 6).Value = s.ArabicStatus;
                    wsLowAct.Cell(row, 7).Value = s.AddedBy ?? "غير معروف";
                    row++;
                }
                wsLowAct.Columns().AdjustToContents();

                // 4. Low Activity Alerts Sheet
                var wsAlerts = workbook.Worksheets.Add("تنبيهات انخفاض النشاط");
                wsAlerts.RightToLeft = true;
                string[] alertHeaders = { "#", "رقم المصدر", "النظير", "الخطورة", "تاريخ آخر معايرة", "الحالة", "النشاط الحالي", "المسؤول" };
                for (int i = 0; i < alertHeaders.Length; i++)
                {
                    wsAlerts.Cell(1, i + 1).Value = alertHeaders[i];
                    wsAlerts.Cell(1, i + 1).Style.Font.Bold = true;
                    wsAlerts.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.OrangeRed;
                    wsAlerts.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
                }
                row = 2;
                int alertIndex = 1;
                foreach (var s in lowActivityAlerts ?? Enumerable.Empty<Source>())
                {
                    wsAlerts.Cell(row, 1).Value = alertIndex++;
                    wsAlerts.Cell(row, 2).Value = s.SourceCode;
                    wsAlerts.Cell(row, 3).Value = s.AlertWorstIsotope ?? s.DisplayIsotopes;
                    wsAlerts.Cell(row, 4).Value = s.AlertSeverityDisplay;
                    wsAlerts.Cell(row, 5).Value = s.CalibrationDate.ToString("yyyy/MM/dd");
                    wsAlerts.Cell(row, 6).Value = s.ArabicStatus;
                    wsAlerts.Cell(row, 7).Value = s.CurrentActivityWithUnit;
                    wsAlerts.Cell(row, 8).Value = s.AddedBy ?? "-";
                    row++;
                }
                wsAlerts.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            });
        }

        public async Task GenerateGeneralReportPdfAsync(IEnumerable<Source> inventory, IEnumerable<BorrowRequest> borrowing, IEnumerable<Source> lowActivity, IEnumerable<Source> lowActivityAlerts, string filePath)
        {
            await Task.Run(() =>
            {
                var borrowList = borrowing?.ToList() ?? new List<BorrowRequest>();
                var alertList = lowActivityAlerts?.ToList() ?? new List<Source>();
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
                                    columns.RelativeColumn(1); // #
                                    columns.RelativeColumn(2); // المصدر
                                    columns.RelativeColumn(3); // المستعير
                                    columns.RelativeColumn(3); // الغرض
                                    columns.RelativeColumn(2); // تاريخ الإرجاع
                                    columns.RelativeColumn(1.5f); // الحالة
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderStyle).Text("#");
                                    header.Cell().Element(HeaderStyle).Text("رقم المصدر");
                                    header.Cell().Element(HeaderStyle).Text("المستعير");
                                    header.Cell().Element(HeaderStyle).Text("الغرض");
                                    header.Cell().Element(HeaderStyle).Text("تاريخ الإرجاع");
                                    header.Cell().Element(HeaderStyle).Text("الحالة");
                                    static IContainer HeaderStyle(IContainer c) => c.Background(Colors.Blue.Medium).PaddingVertical(6).AlignCenter().DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White));
                                });

                                if (!borrowList.Any())
                                {
                                    table.Cell().ColumnSpan(6).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                                }
                                else
                                {
                                    int i = 1;
                                    foreach (var req in borrowList)
                                    {
                                        var bg = (i - 1) % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(i.ToString());
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(req.DisplaySourceCode);
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

                            // 4. Low Activity Alert Section
                            column.Item().PaddingTop(20).PaddingBottom(10).Text("4. تنبيهات انخفاض النشاط").FontSize(16).SemiBold().FontColor(Colors.Red.Medium);
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1); // #
                                    columns.RelativeColumn(2.5f);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1.8f);
                                    columns.RelativeColumn(2.2f);
                                    columns.RelativeColumn(2.5f);
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderStyle).Text("#");
                                    header.Cell().Element(HeaderStyle).Text("المصدر");
                                    header.Cell().Element(HeaderStyle).Text("النظير");
                                    header.Cell().Element(HeaderStyle).Text("الخطورة");
                                    header.Cell().Element(HeaderStyle).Text("تاريخ المعايرة");
                                    header.Cell().Element(HeaderStyle).Text("النشاط الحالي");
                                    static IContainer HeaderStyle(IContainer c) => c.Background(Colors.Red.Lighten4).Padding(5).BorderBottom(1).BorderColor(Colors.Red.Medium);
                                });

                                if (!alertList.Any())
                                {
                                    table.Cell().ColumnSpan(6).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                                }
                                else
                                {
                                    int i = 1;
                                    foreach (var s in alertList)
                                    {
                                        table.Cell().Element(c => CellStyle(c)).Text(i.ToString());
                                        table.Cell().Element(c => CellStyle(c)).Text(s.SourceCode);
                                        table.Cell().Element(c => CellStyle(c)).Text(s.AlertWorstIsotope ?? s.DisplayIsotopes);
                                        table.Cell().Element(c => CellStyle(c)).Text(s.AlertSeverityDisplay);
                                        table.Cell().Element(c => CellStyle(c)).Text(s.CalibrationDate.ToString("yyyy/MM/dd"));
                                        table.Cell().Element(c => CellStyle(c)).Text(s.CurrentActivityWithUnit);
                                        static IContainer CellStyle(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4);
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

        public async Task GenerateLeakTestsReportExcelAsync(IEnumerable<LeakTestRecord> records, string filePath, string reportTitle)
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var sheetName = SanitizeSheetName(reportTitle, "اختبارات التسرب");
                var ws = workbook.Worksheets.Add(sheetName);
                ws.RightToLeft = true;

                // Title
                ws.Cell(1, 1).Value = "منظومة مصادر — " + (string.IsNullOrWhiteSpace(reportTitle) ? "تقرير اختبارات التسرب الدوري" : reportTitle);
                ws.Range(1, 1, 1, 10).Merge().Style.Font.SetBold().Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                ws.Cell(2, 1).Value = $"تاريخ استخراج التقرير: {DateTime.Now:yyyy/MM/dd HH:mm}";
                ws.Range(2, 1, 2, 10).Merge().Style.Font.SetFontSize(10).Font.SetFontColor(XLColor.DarkGray).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                // Table Headers
                string[] headers = { "#", "كود المصدر", "النظير المشع", "تاريخ الفحص", "الاستحقاق القادم", "النتيجة", "النشاط المقاس (Bq)", "القائم بالفحص / المفتش", "رقم الشهادة", "ملاحظات" };
                for (int col = 0; col < headers.Length; col++)
                {
                    var cell = ws.Cell(4, col + 1);
                    cell.Value = headers[col];
                    cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
                    cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#1F5A66"));
                    cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                }

                int row = 5;
                int index = 1;
                foreach (var r in records)
                {
                    ws.Cell(row, 1).Value = index++;
                    ws.Cell(row, 2).Value = r.Source?.SourceCode ?? "-";
                    ws.Cell(row, 3).Value = r.Source?.DisplayIsotopes ?? "-";
                    ws.Cell(row, 4).Value = r.TestDate.ToString("yyyy/MM/dd");
                    ws.Cell(row, 5).Value = r.NextDueDate.ToString("yyyy/MM/dd");
                    ws.Cell(row, 6).Value = r.ArabicResult;
                    ws.Cell(row, 7).Value = r.MeasuredActivityBq.HasValue ? r.MeasuredActivityBq.Value.ToString("N2") : "-";
                    ws.Cell(row, 8).Value = !string.IsNullOrWhiteSpace(r.InspectorName) ? r.InspectorName : (r.PerformedByUser?.FullName ?? "-");
                    ws.Cell(row, 9).Value = r.CertificateNumber ?? "-";
                    ws.Cell(row, 10).Value = r.Notes ?? "-";

                    for (int c = 1; c <= 10; c++)
                    {
                        ws.Cell(row, c).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                        ws.Cell(row, c).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(XLColor.LightGray);
                        if (row % 2 == 0)
                        {
                            ws.Cell(row, c).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F9FAFB"));
                        }
                    }
                    row++;
                }

                ws.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            });
        }

        public async Task GenerateLeakTestsReportPdfAsync(IEnumerable<LeakTestRecord> records, string filePath, string reportTitle)
        {
            await Task.Run(() =>
            {
                var list = records?.ToList() ?? new List<LeakTestRecord>();
                var noDataText = GetNoDataText();
                var title = string.IsNullOrWhiteSpace(reportTitle) ? "تقرير اختبارات التسرب الدوري (Leak/Wipe Tests)" : reportTitle;

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1.5f, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9.5f));
                        page.ContentFromRightToLeft();

                        page.Header().Element(c => ComposeHeader(c, title));

                        page.Content().PaddingVertical(0.8f, Unit.Centimetre).Column(column =>
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(0.7f);  // #
                                    columns.RelativeColumn(1.8f);  // Source Code
                                    columns.RelativeColumn(1.8f);  // Isotope
                                    columns.RelativeColumn(1.8f);  // Test Date
                                    columns.RelativeColumn(1.8f);  // Next Due
                                    columns.RelativeColumn(1.5f);  // Result
                                    columns.RelativeColumn(2.0f);  // Activity
                                    columns.RelativeColumn(2.2f);  // Inspector
                                    columns.RelativeColumn(1.8f);  // Cert Number
                                    columns.RelativeColumn(2.5f);  // Notes
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderStyle).Text("#");
                                    header.Cell().Element(HeaderStyle).Text("كود المصدر");
                                    header.Cell().Element(HeaderStyle).Text("النظير");
                                    header.Cell().Element(HeaderStyle).Text("تاريخ الفحص");
                                    header.Cell().Element(HeaderStyle).Text("الاستحقاق القادم");
                                    header.Cell().Element(HeaderStyle).Text("النتيجة");
                                    header.Cell().Element(HeaderStyle).Text("النشاط (Bq)");
                                    header.Cell().Element(HeaderStyle).Text("المفتش / الفاحص");
                                    header.Cell().Element(HeaderStyle).Text("رقم الشهادة");
                                    header.Cell().Element(HeaderStyle).Text("ملاحظات");

                                    static IContainer HeaderStyle(IContainer c) => c.Background(Colors.Blue.Medium).PaddingVertical(6).AlignCenter().DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White));
                                });

                                if (!list.Any())
                                {
                                    table.Cell().ColumnSpan(10).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                                }
                                else
                                {
                                    int i = 1;
                                    foreach (var r in list)
                                    {
                                        var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(i.ToString());
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(r.Source?.SourceCode ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(r.Source?.DisplayIsotopes ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(r.TestDate.ToString("yyyy/MM/dd"));
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(r.NextDueDate.ToString("yyyy/MM/dd"));
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(r.ArabicResult);
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(r.MeasuredActivityBq.HasValue ? r.MeasuredActivityBq.Value.ToString("N2") : "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(!string.IsNullOrWhiteSpace(r.InspectorName) ? r.InspectorName : (r.PerformedByUser?.FullName ?? "-"));
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(r.CertificateNumber ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(r.Notes ?? "-");
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

        public async Task GenerateFailedLeakTestsReportExcelAsync(IEnumerable<LeakTestRecord> records, string filePath, string? reportTitle = null)
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var title = string.IsNullOrWhiteSpace(reportTitle) ? "المصادر الفاشلة في فحص التسرب" : reportTitle;
                var sheetName = SanitizeSheetName(title, "فحوصات فاشلة");
                var ws = workbook.Worksheets.Add(sheetName);
                ws.RightToLeft = true;

                // العنوان الرئيسي للتقرير
                ws.Cell(1, 1).Value = "منظومة مصادر — " + (string.IsNullOrWhiteSpace(reportTitle) ? "تقرير المصادر الفاشلة في فحص التسرب" : reportTitle);
                ws.Range(1, 1, 1, 7).Merge().Style
                    .Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.FromHtml("#DC2626"))
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                ws.Cell(2, 1).Value = $"تاريخ التصدير: {DateTime.Now:yyyy/MM/dd HH:mm}";
                ws.Range(2, 1, 2, 7).Merge().Style
                    .Font.SetFontSize(10).Font.SetFontColor(XLColor.Gray)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                string[] headers = { "#", "كود المصدر", "النظير", "الموقع", "تاريخ الفحص الفاشل", "حالة المصدر", "ملاحظات الفحص" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(4, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
                    cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#DC2626"));
                    cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                }

                int row = 5;
                int index = 1;
                foreach (var r in records ?? Enumerable.Empty<LeakTestRecord>())
                {
                    ws.Cell(row, 1).Value = index++;
                    ws.Cell(row, 2).Value = r.Source?.SourceCode ?? "-";
                    ws.Cell(row, 3).Value = r.Source?.DisplayIsotopes ?? "-";
                    ws.Cell(row, 4).Value = r.Source?.Location?.LocationName ?? "-";
                    ws.Cell(row, 5).Value = r.TestDate.ToString("yyyy/MM/dd");
                    ws.Cell(row, 6).Value = r.Source?.ArabicStatus ?? "-";
                    ws.Cell(row, 7).Value = r.Notes ?? "-";

                    for (int c = 1; c <= 7; c++)
                    {
                        ws.Cell(row, c).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                        ws.Cell(row, c).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(XLColor.LightGray);
                        if (row % 2 == 0)
                        {
                            ws.Cell(row, c).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FEF2F2"));
                        }
                    }
                    row++;
                }

                ws.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            });
        }

        public async Task GenerateFailedLeakTestsReportPdfAsync(IEnumerable<LeakTestRecord> records, string filePath, string? reportTitle = null)
        {
            await Task.Run(() =>
            {
                var list = records?.ToList() ?? new List<LeakTestRecord>();
                var noDataText = GetNoDataText();
                var title = string.IsNullOrWhiteSpace(reportTitle) ? "تقرير المصادر الفاشلة في فحص التسرب" : reportTitle;

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1.5f, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(9.5f));
                        page.ContentFromRightToLeft();

                        page.Header().Element(c => ComposeHeader(c, title));

                        page.Content().PaddingVertical(0.8f, Unit.Centimetre).Column(column =>
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(0.7f);  // #
                                    columns.RelativeColumn(2.0f);  // Source Code
                                    columns.RelativeColumn(1.8f);  // Isotope
                                    columns.RelativeColumn(2.5f);  // Location
                                    columns.RelativeColumn(2.2f);  // Failed Test Date
                                    columns.RelativeColumn(1.8f);  // Source Status
                                    columns.RelativeColumn(3.5f);  // Notes
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderStyle).Text("#");
                                    header.Cell().Element(HeaderStyle).Text("كود المصدر");
                                    header.Cell().Element(HeaderStyle).Text("النظير");
                                    header.Cell().Element(HeaderStyle).Text("الموقع");
                                    header.Cell().Element(HeaderStyle).Text("تاريخ الفحص الفاشل");
                                    header.Cell().Element(HeaderStyle).Text("حالة المصدر");
                                    header.Cell().Element(HeaderStyle).Text("ملاحظات الفحص");

                                    static IContainer HeaderStyle(IContainer c) => c.Background(Colors.Red.Medium).PaddingVertical(6).AlignCenter().DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White));
                                });

                                if (!list.Any())
                                {
                                    table.Cell().ColumnSpan(7).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text(noDataText).FontSize(11).FontColor(Colors.Grey.Darken1);
                                }
                                else
                                {
                                    int i = 1;
                                    foreach (var r in list)
                                    {
                                        var bg = i % 2 == 0 ? Colors.White : Colors.Red.Lighten5;
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(i.ToString());
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(r.Source?.SourceCode ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(r.Source?.DisplayIsotopes ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(r.Source?.Location?.LocationName ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(r.TestDate.ToString("yyyy/MM/dd"));
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(r.Source?.ArabicStatus ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(r.Notes ?? "-");
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

        /// <summary>إنشاء تقرير جرد المصادر النيترونية بصيغة Excel</summary>
        public async Task GenerateNeutronInventoryReportExcelAsync(IEnumerable<NeutronSource> sources, string filePath, string? reportTitle = null)
        {
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var title = reportTitle ?? "جرد المصادر النيترونية";
                var sheetName = SanitizeSheetName(title, "المصادر النيترونية");
                var worksheet = workbook.Worksheets.Add(sheetName);
                worksheet.RightToLeft = true;

                // Headers
                string[] headers = { "#", "رقم المصدر", "النوع المرجعي", "معدل الانبعاث (n/s)", "عدم اليقين %", "الموقع", "الحالة", "تاريخ المعايرة" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(31, 90, 102);
                    worksheet.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
                }

                int row = 2;
                int index = 1;
                foreach (var s in sources ?? Enumerable.Empty<NeutronSource>())
                {
                    worksheet.Cell(row, 1).Value = index++;
                    worksheet.Cell(row, 2).Value = s.SourceCode;
                    worksheet.Cell(row, 3).Value = s.NeutronSourceType?.Code ?? "-";
                    worksheet.Cell(row, 4).Value = s.EmissionRate;
                    worksheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                    if (s.RelativeExpandedUncertaintyPercent.HasValue)
                    {
                        worksheet.Cell(row, 5).Value = s.RelativeExpandedUncertaintyPercent.Value;
                        worksheet.Cell(row, 5).Style.NumberFormat.Format = "0.0\"%\"";
                    }
                    else
                    {
                        worksheet.Cell(row, 5).Value = "-";
                    }
                    worksheet.Cell(row, 6).Value = s.Location?.LocationName ?? "غير محدد";
                    worksheet.Cell(row, 7).Value = s.ArabicStatus;
                    worksheet.Cell(row, 8).Value = s.CalibrationDate?.ToString("yyyy-MM-dd") ?? "-";
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            });
        }

        /// <summary>إنشاء تقرير جرد المصادر النيترونية بصيغة PDF</summary>
        public async Task GenerateNeutronInventoryReportPdfAsync(IEnumerable<NeutronSource> sources, string filePath, string? reportTitle = null)
        {
            await Task.Run(() =>
            {
                var title = reportTitle ?? "تقرير جرد المصادر النيترونية";
                var list = sources?.ToList() ?? new List<NeutronSource>();

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));
                        page.ContentFromRightToLeft();

                        page.Header().Element(c => ComposeHeaderInventory(c, title));

                        page.Content().PaddingTop(10).Column(col =>
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(25);  // #
                                    columns.RelativeColumn(2);   // Source Code
                                    columns.RelativeColumn(2);   // Reference Type
                                    columns.RelativeColumn(2);   // Emission Rate
                                    columns.RelativeColumn(1.5f);// Uncertainty
                                    columns.RelativeColumn(2);   // Location
                                    columns.RelativeColumn(1.5f);// Status
                                    columns.RelativeColumn(2);   // Calibration Date
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Element(HeaderStyle).Text("#");
                                    h.Cell().Element(HeaderStyle).Text("رقم المصدر");
                                    h.Cell().Element(HeaderStyle).Text("النوع المرجعي");
                                    h.Cell().Element(HeaderStyle).Text("معدل الانبعاث (n/s)");
                                    h.Cell().Element(HeaderStyle).Text("عدم اليقين");
                                    h.Cell().Element(HeaderStyle).Text("الموقع");
                                    h.Cell().Element(HeaderStyle).Text("الحالة");
                                    h.Cell().Element(HeaderStyle).Text("تاريخ المعايرة");

                                    static IContainer HeaderStyle(IContainer c) => c.Background(Colors.Teal.Darken2).PaddingVertical(6).AlignCenter().DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White));
                                });

                                if (!list.Any())
                                {
                                    table.Cell().ColumnSpan(8).Background(Colors.Grey.Lighten4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(10).AlignCenter().Text("لا توجد مصادر نيترونية مسجلة").FontSize(11).FontColor(Colors.Grey.Darken1);
                                }
                                else
                                {
                                    int i = 1;
                                    foreach (var s in list)
                                    {
                                        var bg = i % 2 == 0 ? Colors.White : Colors.Teal.Lighten5;
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(i.ToString());
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(s.SourceCode);
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(s.NeutronSourceType?.Code ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(ScientificNotationParser.FormatScientific(s.EmissionRate));
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(s.RelativeExpandedUncertaintyPercent.HasValue ? $"{s.RelativeExpandedUncertaintyPercent:N1}%" : "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(s.Location?.LocationName ?? "-");
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(s.ArabicStatus);
                                        table.Cell().Element(c => CellStyle(c, bg)).Text(s.CalibrationDate?.ToString("yyyy/MM/dd") ?? "-");
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

