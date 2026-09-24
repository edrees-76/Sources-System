using Sources.Helpers;
using Sources.Interfaces;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبارات وحدة لـ EditingFormTracker (الجولة 199 — حارس الشفاء الذاتي). لا واجهة مستخدم هنا
/// إطلاقاً؛ اختبارات الفتح الفعلي للنوافذ عبر ShowDialog تُتجنَّب عمداً لأنها تحجب تنفيذ
/// الاختبار إلى الأبد بلا تفاعل مستخدم حقيقي (انظر التعليق في DialogHelperAdminPromptTests.cs
/// لخطر مشابه).
/// </summary>
public class EditingFormTrackerTests
{
    private sealed class MockEditable : IEditableViewModel
    {
        public bool IsEditing { get; set; }
        public void CancelEditing() => IsEditing = false;
    }

    [Fact]
    public void IsFormOpen_BeforeMarkOpen_ReturnsFalse()
    {
        var vm = new MockEditable();
        Assert.False(EditingFormTracker.IsFormOpen(vm));
    }

    [Fact]
    public void MarkOpen_ThenIsFormOpen_ReturnsTrue()
    {
        var vm = new MockEditable();
        EditingFormTracker.MarkOpen(vm);
        try
        {
            Assert.True(EditingFormTracker.IsFormOpen(vm));
        }
        finally
        {
            EditingFormTracker.MarkClosed(vm);
        }
    }

    [Fact]
    public void MarkClosed_AfterMarkOpen_ReturnsFalse()
    {
        var vm = new MockEditable();
        EditingFormTracker.MarkOpen(vm);
        EditingFormTracker.MarkClosed(vm);
        Assert.False(EditingFormTracker.IsFormOpen(vm));
    }

    [Fact]
    public void MarkOpen_IsIndependentPerViewModelInstance()
    {
        var vmA = new MockEditable();
        var vmB = new MockEditable();
        EditingFormTracker.MarkOpen(vmA);
        try
        {
            Assert.True(EditingFormTracker.IsFormOpen(vmA));
            Assert.False(EditingFormTracker.IsFormOpen(vmB));
        }
        finally
        {
            EditingFormTracker.MarkClosed(vmA);
        }
    }
}
