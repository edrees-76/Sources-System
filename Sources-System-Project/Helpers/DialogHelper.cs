using Sources.Views;
using System.Windows;

namespace Sources.Helpers
{
    public static class DialogHelper
    {
        public static void ShowInfo(string message, string? title = null, string? imagePath = null)
        {
            if (Application.Current?.Dispatcher == null) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var finalTitle = title ?? TranslationHelper.GetString("AlertTitle");
                var dialog = new AlertDialog(message, finalTitle, "Info", imagePath: imagePath);
                if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                dialog.ShowDialog();
            });
        }

        public static void ShowWarning(string message, string? title = null)
        {
            if (Application.Current?.Dispatcher == null) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var finalTitle = title ?? TranslationHelper.GetString("AlertWarning");
                var dialog = new AlertDialog(message, finalTitle, "Warning");
                if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                dialog.ShowDialog();
            });
        }

        public static void ShowError(string message, string? title = null)
        {
            if (Application.Current?.Dispatcher == null) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var finalTitle = title ?? TranslationHelper.GetString("AlertError");
                var dialog = new AlertDialog(message, finalTitle, "Error");
                if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                dialog.ShowDialog();
            });
        }

        public static bool ShowConfirmation(string message, string? title = null)
        {
            if (Application.Current?.Dispatcher == null) return true;
            bool result = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var finalTitle = title ?? TranslationHelper.GetString("AlertConfirmation");
                var dialog = new AlertDialog(message, finalTitle, "Question", isQuestion: true);
                if (Application.Current.MainWindow != null && Application.Current.MainWindow != dialog)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                dialog.ShowDialog();
                result = dialog.Result == AlertDialog.AlertResult.Yes;
            });
            return result;
        }
    }
}
