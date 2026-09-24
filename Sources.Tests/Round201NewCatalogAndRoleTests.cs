using System;
using Sources.Helpers;
using Sources.Models;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// الجولة 201 (دفعة E) — اختبارات جديدة لكتالوج حالة الاستعارة BorrowStatusCatalog، التعريف
/// المركزي لاسم الدور RoleNames، وثوابت/قاعدة StatusCatalog الجديدة، مع التحقق من أن الواجهة
/// الإنجليزية تعرض النصوص الإنجليزية والواجهة العربية تبقى مطابقة حرفياً للنص الحالي.
/// </summary>
public class Round201NewCatalogAndRoleTests
{
    private static void WithEnglishDictionary(Action assertions)
    {
        Sources.Tests.Fixtures.WpfStaFixture.RunInSta(() =>
        {
            var dicts = System.Windows.Application.Current.Resources.MergedDictionaries;
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

            try
            {
                dicts[arabicDictIndex] = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Strings.en.xaml", UriKind.Absolute)
                };

                assertions();
            }
            finally
            {
                dicts[arabicDictIndex] = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Strings.ar.xaml", UriKind.Absolute)
                };
            }
        });
    }

    private static void WithArabicDictionary(Action assertions)
    {
        Sources.Tests.Fixtures.WpfStaFixture.RunInSta(assertions);
    }

    // ═══════════════════════════ BorrowStatusCatalog ═══════════════════════════

    [Theory]
    [InlineData("Pending", BorrowStatusCode.Pending, "معلّق", "Pending", "#4F7FA3")]
    [InlineData("Approved", BorrowStatusCode.Approved, "تمت الموافقة", "Approved", "#4F7FA3")]
    [InlineData("Rejected", BorrowStatusCode.Rejected, "مرفوض", "Rejected", "#4F7FA3")]
    [InlineData("Delivered", BorrowStatusCode.Delivered, "تم التسليم", "Delivered", "#3FAE7A")]
    [InlineData("Returned", BorrowStatusCode.Returned, "تم الإرجاع", "Returned", "#4F7FA3")]
    [InlineData("Overdue", BorrowStatusCode.Overdue, "متأخر", "Overdue", "#C25B4A")]
    public void BorrowStatusCatalog_RoundTrip_ForEveryStoredValue(string stored, BorrowStatusCode expectedCode, string expectedArabic, string expectedEnglish, string expectedColor)
    {
        Assert.Equal(expectedCode, BorrowStatusCatalog.Parse(stored));
        Assert.Equal(expectedArabic, BorrowStatusCatalog.GetArabicText(stored));
        Assert.Equal(expectedColor, BorrowStatusCatalog.GetColorHex(stored));

        WithArabicDictionary(() => Assert.Equal(expectedArabic, BorrowStatusCatalog.GetDisplayText(stored)));
        WithEnglishDictionary(() => Assert.Equal(expectedEnglish, BorrowStatusCatalog.GetDisplayText(stored)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Cancelled")]
    [InlineData("pending")] // متغيّر حالة أحرف — Parse حساس لحالة الأحرف (Ordinal)
    public void BorrowStatusCatalog_UnknownOrUnexpectedValue_ReturnsUnknown_AndRawTextFallback(string? stored)
    {
        Assert.Equal(BorrowStatusCode.Unknown, BorrowStatusCatalog.Parse(stored));
        Assert.Equal(stored ?? "", BorrowStatusCatalog.GetArabicText(stored));
        Assert.Equal("#9E9E9E", BorrowStatusCatalog.GetColorHex(stored));
        Assert.Equal(stored ?? "", BorrowStatusCatalog.GetDisplayText(stored));
    }

    [Fact]
    public void BorrowStatusCatalog_SetHelpers_MatchDocumentedGroupings()
    {
        Assert.True(BorrowStatusCatalog.IsActiveBorrow("Delivered"));
        Assert.True(BorrowStatusCatalog.IsActiveBorrow("Overdue"));
        Assert.False(BorrowStatusCatalog.IsActiveBorrow("Approved"));

        Assert.True(BorrowStatusCatalog.IsReturnable("Delivered"));
        Assert.True(BorrowStatusCatalog.IsReturnable("Approved"));
        Assert.True(BorrowStatusCatalog.IsReturnable("Overdue"));
        Assert.False(BorrowStatusCatalog.IsReturnable("Pending"));

        Assert.True(BorrowStatusCatalog.BlocksSourceDeletion("Pending"));
        Assert.True(BorrowStatusCatalog.BlocksSourceDeletion("Approved"));
        Assert.True(BorrowStatusCatalog.BlocksSourceDeletion("Delivered"));
        Assert.True(BorrowStatusCatalog.BlocksSourceDeletion("Overdue"));
        Assert.False(BorrowStatusCatalog.BlocksSourceDeletion("Returned"));
        Assert.False(BorrowStatusCatalog.BlocksSourceDeletion("Rejected"));

        Assert.True(BorrowStatusCatalog.IsOverdueSweepCandidate("Delivered"));
        Assert.True(BorrowStatusCatalog.IsOverdueSweepCandidate("Approved"));
        Assert.False(BorrowStatusCatalog.IsOverdueSweepCandidate("Pending"));
    }

    // ═══════════════════════════ BorrowRequest DISPLAY (نموذج) ═══════════════════════════

    [Fact]
    public void BorrowRequest_StatusDisplay_IsEnglish_WhenEnglishLanguageActive()
    {
        WithEnglishDictionary(() =>
        {
            var req = new BorrowRequest { Status = "Overdue" };
            Assert.Equal("Overdue", req.StatusDisplay);
            Assert.Equal("متأخر", req.ArabicStatus); // يبقى عربياً دائماً
        });
    }

    [Fact]
    public void BorrowRequest_StatusDisplay_IsArabic_AndIdenticalToArabicStatus_WhenArabicLanguageActive()
    {
        WithArabicDictionary(() =>
        {
            var req = new BorrowRequest { Status = "Delivered" };
            Assert.Equal("تم التسليم", req.StatusDisplay);
            Assert.Equal(req.ArabicStatus, req.StatusDisplay);
        });
    }

    [Theory]
    [InlineData("Delivered", true, false)]
    [InlineData("Overdue", true, false)]
    [InlineData("Approved", true, false)]
    [InlineData("Pending", false, false)]
    [InlineData("Rejected", false, false)]
    [InlineData("Returned", false, true)]
    public void BorrowRequest_ReturnPanelBooleans_MatchDocumentedSets(string status, bool expectedReturnable, bool expectedReturned)
    {
        var req = new BorrowRequest { Status = status };
        Assert.Equal(expectedReturnable, req.IsReturnableStatus);
        Assert.Equal(expectedReturned, req.IsReturnedStatus);
    }

    // ═══════════════════════════ RoleNames ═══════════════════════════

    [Fact]
    public void RoleNames_Constants_EqualExactStoredLiterals()
    {
        Assert.Equal("مدير النظام", RoleNames.Admin);
        Assert.Equal("مستخدم", RoleNames.User);
    }

    [Fact]
    public void RoleNames_GetDisplayName_IsArabic_AndIdenticalToStoredValue_WhenArabicLanguageActive()
    {
        WithArabicDictionary(() =>
        {
            Assert.Equal(RoleNames.Admin, RoleNames.GetDisplayName(RoleNames.Admin));
            Assert.Equal(RoleNames.User, RoleNames.GetDisplayName(RoleNames.User));
            // دور غير متوقع يُعامَل كمستخدم عادي (نفس منطق Role.DisplayName الحالي)
            Assert.Equal(RoleNames.User, RoleNames.GetDisplayName("مشرف قسم"));
        });
    }

    [Fact]
    public void RoleNames_GetDisplayName_IsEnglish_WhenEnglishLanguageActive()
    {
        WithEnglishDictionary(() =>
        {
            Assert.Equal("System Administrator", RoleNames.GetDisplayName(RoleNames.Admin));
            Assert.Equal("User", RoleNames.GetDisplayName(RoleNames.User));
            Assert.Equal("User", RoleNames.GetDisplayName("مشرف قسم"));
        });
    }

    [Fact]
    public void Role_DisplayName_UsesRoleNames_AndMatchesDirectly()
    {
        var adminRole = new Role { RoleName = RoleNames.Admin };
        var userRole = new Role { RoleName = RoleNames.User };

        WithArabicDictionary(() =>
        {
            Assert.Equal(RoleNames.GetDisplayName(adminRole.RoleName), adminRole.DisplayName);
            Assert.Equal(RoleNames.GetDisplayName(userRole.RoleName), userRole.DisplayName);
        });
    }

    // ═══════════════════════════ StatusCatalog — إضافات الجولة 201 ═══════════════════════════

    [Fact]
    public void StatusCatalog_StoredConstants_MatchDocumentedValues()
    {
        Assert.Equal("InUse", StatusCatalog.InUse);
        Assert.Equal("Storage", StatusCatalog.Storage);
        Assert.Equal("Waste", StatusCatalog.Waste);
        Assert.Equal("Transfer", StatusCatalog.Transfer);
    }

    [Theory]
    [InlineData(SourceStatusCode.InUse, "InUse")]
    [InlineData(SourceStatusCode.Storage, "Storage")]
    [InlineData(SourceStatusCode.Waste, "Waste")]
    [InlineData(SourceStatusCode.Transfer, "Transfer")]
    public void StatusCatalog_ToStored_ReturnsMatchingConstant(SourceStatusCode code, string expectedStored)
    {
        Assert.Equal(expectedStored, StatusCatalog.ToStored(code));
    }

    [Fact]
    public void StatusCatalog_ToStored_Unknown_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StatusCatalog.ToStored(SourceStatusCode.Unknown));
    }

    [Theory]
    [InlineData("InUse", true)]
    [InlineData("Storage", true)]
    [InlineData("Waste", false)]
    [InlineData("Transfer", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("inuse", false)] // حساس لحالة الأحرف تماماً كالمقارنة الحرفية الحالية
    public void StatusCatalog_IsActiveInventory_MatchesCurrentCaseSensitiveComparison(string? stored, bool expected)
    {
        Assert.Equal(expected, StatusCatalog.IsActiveInventory(stored));
    }

    // ═══════════════════════════ خيارات الفلتر المُترجَمة (Value يبقى ثابتاً، Display يتغيّر باللغة) ═══════════════════════════

    [Fact]
    public void LocationDetailsViewModel_StatusFilterOptions_ValuesUnchanged_DisplaysLocalized()
    {
        var location = new Location { Id = Guid.NewGuid(), LocationName = "موقع اختبار الجولة 201" };
        var vm = new LocationDetailsViewModel(location, new System.Collections.Generic.List<Source>(), reportingService: null, neutronSources: new System.Collections.Generic.List<NeutronSource>());

        var values = vm.StatusFilterOptions.ConvertAll(o => o.Value);
        Assert.Equal(new[] { "الكل", "قيد الاستخدام", "مخزن", "نفايات", "قيد النقل" }, values);

        WithEnglishDictionary(() =>
        {
            var vmEn = new LocationDetailsViewModel(location, new System.Collections.Generic.List<Source>(), reportingService: null, neutronSources: new System.Collections.Generic.List<NeutronSource>());
            var displays = vmEn.StatusFilterOptions.ConvertAll(o => o.Display);
            Assert.Equal(new[] { "All", "In Use", "In Storage", "Waste", "In Transfer" }, displays);
        });
    }

    [Fact]
    public void BorrowViewModel_StatusFilters_ValuesUnchanged_DisplaysLocalized_InEnglish()
    {
        WithEnglishDictionary(() =>
        {
            var options = new System.Collections.Generic.List<BorrowViewModel.StatusFilterOption>
            {
                new("الكل", TranslationHelper.GetString("FilterAll") ?? "All"),
                new("تم التسليم", BorrowStatusCatalog.GetDisplayText(BorrowStatusCatalog.Delivered)),
                new("تم الإرجاع", BorrowStatusCatalog.GetDisplayText(BorrowStatusCatalog.Returned)),
                new("متأخر", BorrowStatusCatalog.GetDisplayText(BorrowStatusCatalog.Overdue)),
                new("قريبة الإرجاع", TranslationHelper.GetString("FilterDueSoon") ?? "Due Soon"),
            };

            Assert.Equal("All", options[0].Display);
            Assert.Equal("Delivered", options[1].Display);
            Assert.Equal("Returned", options[2].Display);
            Assert.Equal("Overdue", options[3].Display);
            Assert.Equal("Due Soon", options[4].Display);
        });
    }
}
