using System;
using System.Windows;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Sources.Views;

namespace Sources.Helpers;

public static class SourceNavigationHelper
{
    public static Action<Source>? CustomOpenAction { get; set; }

    public static void OpenSourceDetails(object? parameter, Guid? sourceId = null)
    {
        Source? source = parameter switch
        {
            Source s => s,
            DeletedSourceRow dsr => dsr.Source,
            DashboardSourceRow dsr => dsr.Source,
            AlertRow ar => ar.Source ?? (ar.SourceId.HasValue ? GetSourceById(ar.SourceId.Value) : null),
            Guid id => GetSourceById(id),
            _ => sourceId.HasValue ? GetSourceById(sourceId.Value) : null
        };

        if (source == null && sourceId.HasValue)
        {
            source = GetSourceById(sourceId.Value);
        }

        // إذا كانت خصائص المصدر غير محملة بالكامل (مثل النظائر المتعددة أو الموقع)، نجلب الكائن الكامل بالمعرف
        if (source != null && (source.Radioisotope == null || (source.HasDetailedIsotopes && (source.SourceIsotopes == null || source.SourceIsotopes.Count == 0))))
        {
            var fullSource = GetSourceById(source.Id);
            if (fullSource != null)
            {
                source = fullSource;
            }
        }

        if (source == null)
        {
            DialogHelper.ShowWarning(
                TranslationHelper.GetString("MsgSourceNotFound") ?? "المصدر غير موجود أو تم حذفه من المنظومة.",
                TranslationHelper.GetString("TitleSourceDetails") ?? "تفاصيل المصدر");
            return;
        }

        if (CustomOpenAction != null)
        {
            CustomOpenAction(source);
            return;
        }

        if (DialogHelper.IsTestMode) return;

        var app = Application.Current;
        if (app == null) return;

        if (app.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(() => OpenSourceDetails(source));
            return;
        }

        try
        {
            var viewModel = new SourceDetailsViewModel(source);
            var window = new SourceDetailsWindow(viewModel);

            if (app.MainWindow != null && app.MainWindow.IsVisible && app.MainWindow != window)
            {
                window.Owner = app.MainWindow;
            }

            window.ShowDialog();
        }
        catch (Exception ex)
        {
            LoggerService.LogError("SourceNavigationHelper: Failed to open SourceDetailsWindow", ex);
        }
    }

    private static Source? GetSourceById(Guid id)
    {
        try
        {
            var service = App.ServiceProvider?.GetService(typeof(ISourceService)) as ISourceService;
            return service?.GetSourceById(id);
        }
        catch (Exception ex)
        {
            LoggerService.LogError("SourceNavigationHelper: Failed to get source by ID", ex);
            return null;
        }
    }
}
