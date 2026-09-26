using System;
using System.Windows;
using System.Windows.Input;
using Sources.Helpers;
using Sources.Services;

namespace Sources.Views;

/// <summary>
/// حوار اختياري يتيح للمستخدم المسجَّل تغيير كلمة مروره بنفسه بعد إدخال كلمة المرور الحالية (الجولة 209).
/// التحقق الفعلي (الطول الأدنى، صحة كلمة المرور الحالية، الاختلاف عنها) في <see cref="IUserService.ChangeOwnPassword"/>.
/// </summary>
public partial class ChangeOwnPasswordDialog : Window
{
    private readonly IUserService _userService;

    public bool Succeeded { get; private set; }

    public ChangeOwnPasswordDialog(IUserService userService)
    {
        InitializeComponent();
        _userService = userService;
        Loaded += (s, e) => TxtCurrentPassword.Focus();
    }

    /// <summary>
    /// فحص نقي قبل استدعاء الخدمة: كلمة المرور الجديدة غير فارغة ومطابقة لتأكيدها.
    /// يُعيد null عند السلامة، أو نص الخطأ.
    /// </summary>
    public static string? ValidateInput(string? newPassword, string? confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            return TranslationHelper.GetString("MsgErrEnterNewPassword") ?? "يرجى إدخال كلمة مرور جديدة";
        if (newPassword != confirmPassword)
            return TranslationHelper.GetString("MsgErrPasswordConfirmMismatch") ?? "كلمة المرور وتأكيدها غير متطابقين";
        return null;
    }

    /// <summary>عرض الحوار للمستخدم الحالي. يُعيد true فقط بعد نجاح التغيير فعلياً.</summary>
    public static bool Request()
    {
        if (DialogHelper.IsTestMode || Application.Current?.Dispatcher == null) return false;

        var userService = App.ServiceProvider?.GetService(typeof(IUserService)) as IUserService;
        if (userService?.CurrentUser == null) return false;

        var dialog = new ChangeOwnPasswordDialog(userService);
        if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            dialog.Owner = Application.Current.MainWindow;
        }
        dialog.ShowDialog();
        return dialog.Succeeded;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var error = ValidateInput(TxtNewPassword.Password, TxtConfirmPassword.Password);
        if (error != null)
        {
            ShowError(error);
            return;
        }

        var (success, message) = _userService.ChangeOwnPassword(TxtCurrentPassword.Password, TxtNewPassword.Password);
        if (!success)
        {
            ShowError(message);
            return;
        }

        Succeeded = true;
        DialogHelper.ShowInfo(message, TitleText.Text);
        DialogResult = true;
        Close();
    }

    private void TxtConfirmPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConfirmButton_Click(sender, e);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
