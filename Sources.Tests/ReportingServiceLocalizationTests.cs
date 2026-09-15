using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ClosedXML.Excel;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// الجولة 158: يتحقق أن تصدير PDF/Excel (ReportingService.cs) يتبع فعلياً لغة الواجهة النشطة
/// (SettingsHelper.Language)، وليس فقط أن التوليد لا يرمي استثناء. لكل نوع تقرير: نص معروف
/// (اسم ورقة/رأس عمود) يجب أن يختلف فعلياً بين ar وen ويطابق القيمة المتوقعة من كل قاموس.
/// يعيد اختبار كل حالة SettingsHelper.Language الأصلية في finally لتفادي تسريب الحالة.
/// </summary>
public class ReportingServiceLocalizationTests : IDisposable
{
    private readonly string _testDir;
    private readonly ReportingService _sut;

    public ReportingServiceLocalizationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "Sources_ReportingServiceLocalizationTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _sut = new ReportingService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in temp directory
        }
    }

    private string GetTempFilePath(string extension) =>
        Path.Combine(_testDir, $"{Guid.NewGuid():N}.{extension.TrimStart('.')}");

    /// <summary>
    /// يبدّل قاموس Strings.ar.xaml النشط إلى Strings.en.xaml (أو العكس)، مطابقاً تماماً لآلية
    /// App.ApplyLanguage الإنتاجية ولنمط Round156TranslationTests، مع ضبط SettingsHelper.Language.
    /// يُستدعى التبديل على الخيط الحيّد (STA) بينما يُنفَّذ التوليد الفعلي (async) خارجه لتفادي
    /// أي احتمال تعليق (deadlock) بين Dispatcher.Invoke وTask.Wait المتزامن.
    /// </summary>
    private static async Task WithLanguageAsync(bool english, Func<Task> action)
    {
        var originalLanguage = SettingsHelper.Language;

        if (english)
        {
            WpfStaFixture.RunInSta(() =>
            {
                var dicts = Application.Current.Resources.MergedDictionaries;
                var arabicDictIndex = -1;
                for (int i = 0; i < dicts.Count; i++)
                {
                    var src = dicts[i].Source?.OriginalString;
                    if (src != null && src.Contains("Strings.ar.xaml"))
                    {
                        arabicDictIndex = i;
                        break;
                    }
                }
                Assert.True(arabicDictIndex >= 0, "Strings.ar.xaml dictionary must already be loaded by WpfStaFixture.");

                dicts[arabicDictIndex] = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Strings.en.xaml", UriKind.Absolute)
                };
            });
        }

        SettingsHelper.Language = english ? "en" : "ar";

        try
        {
            await action();
        }
        finally
        {
            SettingsHelper.Language = originalLanguage;

            if (english)
            {
                WpfStaFixture.RunInSta(() =>
                {
                    var dicts = Application.Current.Resources.MergedDictionaries;
                    for (int i = 0; i < dicts.Count; i++)
                    {
                        var src = dicts[i].Source?.OriginalString;
                        if (src != null && src.Contains("Strings.en.xaml"))
                        {
                            dicts[i] = new ResourceDictionary
                            {
                                Source = new Uri("pack://application:,,,/Sources;component/Resources/Strings.ar.xaml", UriKind.Absolute)
                            };
                            break;
                        }
                    }
                });
            }
        }
    }

    [Fact]
    public async Task GenerateLocationsReportExcelAsync_FollowsActiveLanguage()
    {
        var locations = new List<Location>
        {
            new() { LocationName = "مختبر الأبحاث", LocationType = "Lab", Building = "مبنى أ", Room = "101", ResponsiblePerson = "أحمد" }
        };

        await WithLanguageAsync(false, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateLocationsReportExcelAsync(locations, filePath);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("المواقع والمخازن", ws.Name);
            Assert.Equal("اسم الموقع", ws.Cell(1, 2).GetString());
            Assert.True(ws.RightToLeft);
        });

        await WithLanguageAsync(true, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateLocationsReportExcelAsync(locations, filePath);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("Locations & Storage", ws.Name);
            Assert.Equal("Location Name", ws.Cell(1, 2).GetString());
            Assert.False(ws.RightToLeft);
        });
    }

    [Fact]
    public async Task GenerateInventoryReportExcelAsync_FollowsActiveLanguage()
    {
        var sources = new List<Source> { new() { SourceCode = "SRC-L10N-01", Status = "InUse" } };

        await WithLanguageAsync(false, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateInventoryReportExcelAsync(sources, filePath, "جرد المصادر");
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("رقم المصدر", ws.Cell(1, 2).GetString());
            Assert.True(ws.RightToLeft);
        });

        await WithLanguageAsync(true, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            // reportTitle الممرَّر من الـViewModel لا يُترجَم أبداً (يبقى كما مُرِّر)
            await _sut.GenerateInventoryReportExcelAsync(sources, filePath, "جرد المصادر");
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("جرد المصادر", ws.Name);
            Assert.Equal("Source Code", ws.Cell(1, 2).GetString());
            Assert.False(ws.RightToLeft);
        });
    }

    [Fact]
    public async Task GenerateBorrowHistoryExcelAsync_FollowsActiveLanguage()
    {
        var requests = new List<BorrowRequest> { new() { BorrowerName = "أحمد", Status = "Pending" } };

        await WithLanguageAsync(false, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateBorrowHistoryExcelAsync(requests, filePath);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("سجل الاستعارات", ws.Name);
            Assert.Equal("المستعير", ws.Cell(1, 3).GetString());
            Assert.True(ws.RightToLeft);
        });

        await WithLanguageAsync(true, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateBorrowHistoryExcelAsync(requests, filePath);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("Borrow History", ws.Name);
            Assert.Equal("Borrower", ws.Cell(1, 3).GetString());
            Assert.False(ws.RightToLeft);
        });
    }

    [Fact]
    public async Task GenerateLowActivityAlertReportExcelAsync_FollowsActiveLanguage()
    {
        var sources = new List<Source> { new() { SourceCode = "SRC-L10N-02", Status = "InUse" } };

        await WithLanguageAsync(false, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateLowActivityAlertReportExcelAsync(sources, filePath);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("تنبيهات انخفاض النشاط", ws.Name);
            Assert.Equal("الخطورة", ws.Cell(1, 4).GetString());
            Assert.True(ws.RightToLeft);
        });

        await WithLanguageAsync(true, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateLowActivityAlertReportExcelAsync(sources, filePath);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("Low Activity Alerts", ws.Name);
            Assert.Equal("Severity", ws.Cell(1, 4).GetString());
            Assert.False(ws.RightToLeft);
        });
    }

    [Fact]
    public async Task GenerateGeneralReportExcelAsync_FollowsActiveLanguage()
    {
        await WithLanguageAsync(false, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateGeneralReportExcelAsync(null!, null!, null!, null!, filePath);
            using var wb = new XLWorkbook(filePath);
            Assert.Equal("جرد المصادر", wb.Worksheets.Worksheet(1).Name);
            Assert.Equal("سجل الاستعارات", wb.Worksheets.Worksheet(2).Name);
            Assert.Equal("المصادر منخفضة النشاط", wb.Worksheets.Worksheet(3).Name);
            Assert.Equal("تنبيهات انخفاض النشاط", wb.Worksheets.Worksheet(4).Name);
        });

        await WithLanguageAsync(true, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateGeneralReportExcelAsync(null!, null!, null!, null!, filePath);
            using var wb = new XLWorkbook(filePath);
            Assert.Equal("Sources Inventory", wb.Worksheets.Worksheet(1).Name);
            Assert.Equal("Borrow History", wb.Worksheets.Worksheet(2).Name);
            Assert.Equal("Low Activity Sources", wb.Worksheets.Worksheet(3).Name);
            Assert.Equal("Low Activity Alerts", wb.Worksheets.Worksheet(4).Name);
        });
    }

    [Fact]
    public async Task GenerateUsersReportExcelAsync_FollowsActiveLanguage()
    {
        var users = new List<User> { new() { FullName = "مستخدم", Username = "u1" } };

        await WithLanguageAsync(false, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateUsersReportExcelAsync(users, filePath);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("المستخدمين والكوادر", ws.Name);
            Assert.Equal("الاسم الكامل", ws.Cell(1, 2).GetString());
            Assert.True(ws.RightToLeft);
        });

        await WithLanguageAsync(true, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateUsersReportExcelAsync(users, filePath);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("Users & Staff", ws.Name);
            Assert.Equal("Full Name", ws.Cell(1, 2).GetString());
            Assert.False(ws.RightToLeft);
        });
    }

    [Fact]
    public async Task GenerateAuditLogsExcelAsync_FollowsActiveLanguage()
    {
        var logs = new List<AuditLog> { new() { Action = "Create", ActionDate = DateTime.Now } };

        await WithLanguageAsync(false, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateAuditLogsExcelAsync(logs, filePath);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("سجل التدقيق والنشاطات", ws.Name);
            Assert.Equal("المستخدم", ws.Cell(1, 2).GetString());
            Assert.True(ws.RightToLeft);
        });

        await WithLanguageAsync(true, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateAuditLogsExcelAsync(logs, filePath);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("Audit & Activity Log", ws.Name);
            Assert.Equal("User", ws.Cell(1, 2).GetString());
            Assert.False(ws.RightToLeft);
        });
    }

    [Fact]
    public async Task GenerateLeakTestsReportExcelAsync_FollowsActiveLanguage()
    {
        var records = new List<LeakTestRecord>();

        await WithLanguageAsync(false, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateLeakTestsReportExcelAsync(records, filePath, null!);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("اختبارات التسرب", ws.Name);
            Assert.Equal("منظومة مصادر — تقرير اختبارات التسرب الدوري", ws.Cell(1, 1).GetString());
            Assert.Equal("كود المصدر", ws.Cell(4, 2).GetString());
            Assert.True(ws.RightToLeft);
        });

        await WithLanguageAsync(true, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateLeakTestsReportExcelAsync(records, filePath, null!);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("Leak Tests", ws.Name);
            Assert.Equal("Sources System — Periodic Leak Test Report", ws.Cell(1, 1).GetString());
            Assert.Equal("Source Code", ws.Cell(4, 2).GetString());
            Assert.False(ws.RightToLeft);
        });
    }

    [Fact]
    public async Task GenerateFailedLeakTestsReportExcelAsync_FollowsActiveLanguage()
    {
        var records = new List<LeakTestRecord>();

        await WithLanguageAsync(false, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateFailedLeakTestsReportExcelAsync(records, filePath);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("المصادر الفاشلة في فحص التسرب", ws.Name);
            Assert.Equal("منظومة مصادر — تقرير المصادر الفاشلة في فحص التسرب", ws.Cell(1, 1).GetString());
            Assert.Equal("كود المصدر", ws.Cell(4, 2).GetString());
            Assert.True(ws.RightToLeft);
        });

        await WithLanguageAsync(true, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateFailedLeakTestsReportExcelAsync(records, filePath);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("Sources That Failed Leak Test", ws.Name);
            Assert.Equal("Sources System — Report of Sources That Failed Leak Test", ws.Cell(1, 1).GetString());
            Assert.Equal("Source Code", ws.Cell(4, 2).GetString());
            Assert.False(ws.RightToLeft);
        });
    }

    [Fact]
    public async Task GenerateNeutronInventoryReportExcelAsync_FollowsActiveLanguage()
    {
        var sources = new List<NeutronSource>();

        await WithLanguageAsync(false, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateNeutronInventoryReportExcelAsync(sources, filePath);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("جرد المصادر النيترونية", ws.Name);
            Assert.Equal("النوع المرجعي", ws.Cell(1, 3).GetString());
            Assert.True(ws.RightToLeft);
        });

        await WithLanguageAsync(true, async () =>
        {
            var filePath = GetTempFilePath("xlsx");
            await _sut.GenerateNeutronInventoryReportExcelAsync(sources, filePath);
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            Assert.Equal("Neutron Sources Inventory", ws.Name);
            Assert.Equal("Reference Type", ws.Cell(1, 3).GetString());
            Assert.False(ws.RightToLeft);
        });
    }
}
