using System;
using Sources.Helpers;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبارات نقطة العزل DialogHelper.ShowAdminPrompt (الجولة 199 — الفشل المغلق لطلب صلاحية مدير
/// النظام). لا يُطفأ IsTestMode في أي اختبار هنا (خطر حجب حقيقي موثّق في DialogHelper.cs)؛ مسار
/// الاستثناء يُختبر عبر التابع الداخلي ExecuteAdminPromptWithFailClosedHandling مباشرة (عبر
/// InternalsVisibleTo) بينما يبقى IsTestMode مفعّلاً فتظل ShowError آمنة (LastMessage فقط، بلا
/// نافذة حقيقية).
/// </summary>
public class DialogHelperAdminPromptTests : IDisposable
{
    public DialogHelperAdminPromptTests()
    {
        DialogHelper.TestAdminPromptResult = null;
        DialogHelper.LastMessage = null;
    }

    public void Dispose()
    {
        DialogHelper.TestAdminPromptResult = null;
        DialogHelper.LastMessage = null;
    }

    [Fact]
    public void ShowAdminPrompt_TestMode_NoExplicitResult_IsDeniedByDefault()
    {
        DialogHelper.TestAdminPromptResult = null;
        var invoked = false;

        var result = DialogHelper.ShowAdminPrompt(() => { invoked = true; return true; });

        Assert.False(result);
        Assert.False(invoked, "The real dialog func must never run in test mode without an explicit result.");
    }

    [Fact]
    public void ShowAdminPrompt_TestMode_ExplicitTrue_IsGranted()
    {
        DialogHelper.TestAdminPromptResult = true;
        var result = DialogHelper.ShowAdminPrompt(() => throw new InvalidOperationException("must not be invoked in test mode"));
        Assert.True(result);
    }

    [Fact]
    public void ShowAdminPrompt_TestMode_ExplicitFalse_IsDenied()
    {
        DialogHelper.TestAdminPromptResult = false;
        var result = DialogHelper.ShowAdminPrompt(() => throw new InvalidOperationException("must not be invoked in test mode"));
        Assert.False(result);
    }

    [Fact]
    public void ExecuteAdminPromptWithFailClosedHandling_FuncThrows_IsDenied_AndDoesNotThrow_AndSetsGenericMessage()
    {
        var ex = Record.Exception(() =>
        {
            var result = DialogHelper.ExecuteAdminPromptWithFailClosedHandling(() => throw new InvalidOperationException("boom"));
            Assert.False(result);
        });

        Assert.Null(ex);
        Assert.NotNull(DialogHelper.LastMessage);
    }

    [Fact]
    public void ExecuteAdminPromptWithFailClosedHandling_FuncReturnsTrue_IsGranted()
    {
        var result = DialogHelper.ExecuteAdminPromptWithFailClosedHandling(() => true);
        Assert.True(result);
    }

    [Fact]
    public void ExecuteAdminPromptWithFailClosedHandling_FuncReturnsFalse_IsDenied()
    {
        var result = DialogHelper.ExecuteAdminPromptWithFailClosedHandling(() => false);
        Assert.False(result);
    }
}
