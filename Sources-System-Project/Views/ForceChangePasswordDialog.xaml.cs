using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Sources.Services;

namespace Sources.Views;

/// <summary>
/// حوار إلزامي (بلا زر إلغاء وبلا إمكانية إغلاق عبر Alt+F4 أو الزر X) يُجبر المستخدم على
/// تغيير كلمة مرور مدير النظام الافتراضية قبل المتابعة إلى الشاشة الرئيسية.
/// يُغلَق فقط بعد نجاح تعيين كلمة مرور جديدة وصالحة عبر <see cref="ConfirmButton_Click"/>.
/// </summary>
public partial class ForceChangePasswordDialog : Window
{
    private readonly IUserService _userService;
    private readonly System.Guid _userId;
    private bool _passwordChanged;

    /// <summary>يصبح true فقط بعد نجاح تغيير كلمة المرور فعلياً. للاستخدام في الاختبارات
    /// وفي أي سياق لا يعرض النافذة عبر ShowDialog() (حيث لا يمكن الاعتماد على DialogResult).</summary>
    public bool Succeeded { get; private set; }

    public ForceChangePasswordDialog(IUserService userService, System.Guid userId)
    {
        InitializeComponent();
        _userService = userService;
        _userId = userId;

        Loaded += (s, e) => TxtNewPassword.Focus();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        // حوار إلزامي: يُمنع إغلاقه (X، Alt+F4، إلخ) قبل نجاح تغيير كلمة المرور فعلياً.
        if (!_passwordChanged)
        {
            e.Cancel = true;
        }
    }

    private void TxtConfirmPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConfirmButton_Click(sender, e);
        }
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var newPassword = TxtNewPassword.Password;
        var confirmPassword = TxtConfirmPassword.Password;

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            ShowError("يرجى إدخال كلمة مرور جديدة");
            return;
        }

        if (newPassword != confirmPassword)
        {
            ShowError("كلمة المرور وتأكيدها غير متطابقين");
            return;
        }

        // منع الالتفاف على الفرض بإعادة إدخال نفس القيمة الافتراضية
        if (newPassword == "admin")
        {
            ShowError("لا يمكن استخدام كلمة المرور الافتراضية \"admin\" مجدداً. يرجى اختيار كلمة مرور جديدة مختلفة");
            return;
        }

        var (success, message) = _userService.ResetPassword(_userId, newPassword);
        if (!success)
        {
            ShowError(message);
            return;
        }

        _passwordChanged = true;
        Succeeded = true;
        try
        {
            // DialogResult قابل للضبط فقط إذا عُرضت النافذة عبر ShowDialog(). في سياقات
            // اختبارية لا تعرض النافذة فعلياً، يُتجاهَل هذا ويُكتفى بالإغلاق العادي.
            DialogResult = true;
        }
        catch (System.InvalidOperationException)
        {
        }
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
