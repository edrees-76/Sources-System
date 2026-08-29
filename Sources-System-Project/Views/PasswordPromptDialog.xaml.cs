using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Sources.Helpers;
using Sources.Services;

namespace Sources.Views
{
    public partial class PasswordPromptDialog : Window
    {
        public bool Result { get; private set; } = false;

        /// <summary>
        /// خاصية مخصصة للاختبارات الآلية للتحكم بنتيجة النافذة دون إظهار واجهة المستخدم
        /// </summary>
        public static bool? CustomPromptResult { get; set; }

        public PasswordPromptDialog(string? title = null, string? prompt = null)
        {
            InitializeComponent();

            if (!string.IsNullOrWhiteSpace(title))
            {
                TitleText.Text = title;
            }

            if (!string.IsNullOrWhiteSpace(prompt))
            {
                PromptText.Text = prompt;
            }

            Loaded += (s, e) =>
            {
                TxtPassword.Focus();
            };
        }

        /// <summary>
        /// التحقق المنطقي النقي من كلمة مرور مدير النظام (للاستخدام المباشر في الخدمات والاختبارات)
        /// </summary>
        public static (bool Success, string Message) ValidateAdminPassword(IUserService? userService, string? password)
        {
            var currentUser = userService?.CurrentUser;
            if (currentUser == null)
            {
                return (false, TranslationHelper.GetString("MsgErrNoCurrentUser") ?? "لا يوجد مستخدم مسجل حالياً في الجلسة");
            }

            bool isAdmin = currentUser.IsAdmin || (currentUser.Role?.RoleName == "مدير النظام");
            if (!isAdmin)
            {
                return (false, TranslationHelper.GetString("MsgErrAdminOnly") ?? "غير مصرح: هذه العملية مخصصة لمدير النظام فقط");
            }

            if (string.IsNullOrEmpty(password) || !PasswordHelper.VerifyPassword(password, currentUser.PasswordHash))
            {
                return (false, TranslationHelper.GetString("MsgErrIncorrectAdminPassword") ?? "كلمة المرور غير صحيحة");
            }

            return (true, "تم التحقق بنجاح");
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var userService = App.ServiceProvider?.GetService(typeof(IUserService)) as IUserService;
            var enteredPassword = TxtPassword.Password;

            var (success, message) = ValidateAdminPassword(userService, enteredPassword);
            if (!success)
            {
                DialogHelper.ShowWarning(message, TitleText.Text);
                TxtPassword.Password = string.Empty;
                TxtPassword.Focus();
                return;
            }

            Result = true;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            DialogResult = false;
            Close();
        }

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmButton_Click(sender, e);
            }
        }

        /// <summary>
        /// طلب التحقق من هوية مدير النظام بشكل متزامن
        /// </summary>
        public static bool RequestAdminAccess(string? title = null, string? prompt = null)
        {
            if (CustomPromptResult.HasValue)
            {
                return CustomPromptResult.Value;
            }

            if (DialogHelper.IsTestMode || Application.Current?.Dispatcher == null)
            {
                return true;
            }

            bool granted = false;
            if (Application.Current.Dispatcher.CheckAccess())
            {
                var dialog = new PasswordPromptDialog(title, prompt);
                if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                dialog.ShowDialog();
                granted = dialog.Result;
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new PasswordPromptDialog(title, prompt);
                    if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                    {
                        dialog.Owner = Application.Current.MainWindow;
                    }
                    dialog.ShowDialog();
                    granted = dialog.Result;
                });
            }

            return granted;
        }

        /// <summary>
        /// طلب التحقق من هوية مدير النظام بشكل غير متزامن
        /// </summary>
        public static Task<bool> RequestAdminAccessAsync(string? title = null, string? prompt = null)
        {
            return Task.FromResult(RequestAdminAccess(title, prompt));
        }
    }
}
