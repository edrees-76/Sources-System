using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Moq;
using Sources.Services;
using Sources.Views;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// الجولة 164 (ب): يتحقق من رفض حوار فرض تغيير كلمة المرور لأي محاولة لإعادة استخدام
/// كلمة المرور الافتراضية "admin"، وقبول كلمة مرور صالحة جديدة عبر IUserService.ResetPassword.
/// </summary>
public class ForceChangePasswordDialogTests
{
    private static void RunInSta(Action action) => Sources.Tests.Fixtures.WpfStaFixture.RunInSta(action);

    private static void InvokeConfirm(ForceChangePasswordDialog dialog)
    {
        var method = typeof(ForceChangePasswordDialog).GetMethod("ConfirmButton_Click", BindingFlags.NonPublic | BindingFlags.Instance);
        method!.Invoke(dialog, new object?[] { dialog, new RoutedEventArgs() });
    }

    private static void SetPasswords(ForceChangePasswordDialog dialog, string newPassword, string confirmPassword)
    {
        var newBox = (PasswordBox)typeof(ForceChangePasswordDialog).GetField("TxtNewPassword", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)!.GetValue(dialog)!;
        var confirmBox = (PasswordBox)typeof(ForceChangePasswordDialog).GetField("TxtConfirmPassword", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)!.GetValue(dialog)!;
        newBox.Password = newPassword;
        confirmBox.Password = confirmPassword;
    }

    private static bool IsErrorVisible(ForceChangePasswordDialog dialog)
    {
        var errorText = (System.Windows.Controls.TextBlock)typeof(ForceChangePasswordDialog).GetField("ErrorText", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)!.GetValue(dialog)!;
        return errorText.Visibility == Visibility.Visible;
    }

    [Fact]
    public void ConfirmButton_WithLiteralAdminPassword_IsRejectedAndDoesNotCallResetPassword()
    {
        RunInSta(() =>
        {
            var mockUserService = new Mock<IUserService>();
            var userId = Guid.NewGuid();
            var dialog = new ForceChangePasswordDialog(mockUserService.Object, userId);

            SetPasswords(dialog, "admin", "admin");
            InvokeConfirm(dialog);

            mockUserService.Verify(s => s.ResetPassword(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
            Assert.True(IsErrorVisible(dialog));
        });
    }

    [Fact]
    public void ConfirmButton_WithMismatchedConfirmation_IsRejectedAndDoesNotCallResetPassword()
    {
        RunInSta(() =>
        {
            var mockUserService = new Mock<IUserService>();
            var userId = Guid.NewGuid();
            var dialog = new ForceChangePasswordDialog(mockUserService.Object, userId);

            SetPasswords(dialog, "NewStrongPassword1!", "SomethingElse2!");
            InvokeConfirm(dialog);

            mockUserService.Verify(s => s.ResetPassword(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
            Assert.True(IsErrorVisible(dialog));
        });
    }

    [Fact]
    public void ConfirmButton_WithValidNewPassword_CallsResetPasswordAndClosesSuccessfully()
    {
        RunInSta(() =>
        {
            var mockUserService = new Mock<IUserService>();
            var userId = Guid.NewGuid();
            mockUserService
                .Setup(s => s.ResetPassword(userId, "NewStrongPassword1!"))
                .Returns((true, "تم إعادة تعيين كلمة المرور"));

            var dialog = new ForceChangePasswordDialog(mockUserService.Object, userId);
            SetPasswords(dialog, "NewStrongPassword1!", "NewStrongPassword1!");

            InvokeConfirm(dialog);

            mockUserService.Verify(s => s.ResetPassword(userId, "NewStrongPassword1!"), Times.Once);
            Assert.False(IsErrorVisible(dialog));
            Assert.True(dialog.Succeeded);
        });
    }

    [Fact]
    public void ConfirmButton_WhenResetPasswordFails_ShowsErrorAndDoesNotClose()
    {
        RunInSta(() =>
        {
            var mockUserService = new Mock<IUserService>();
            var userId = Guid.NewGuid();
            mockUserService
                .Setup(s => s.ResetPassword(userId, It.IsAny<string>()))
                .Returns((false, "فشل غير متوقع"));

            var dialog = new ForceChangePasswordDialog(mockUserService.Object, userId);
            SetPasswords(dialog, "NewStrongPassword1!", "NewStrongPassword1!");

            InvokeConfirm(dialog);

            Assert.True(IsErrorVisible(dialog));
            Assert.False(dialog.Succeeded);
        });
    }
}
