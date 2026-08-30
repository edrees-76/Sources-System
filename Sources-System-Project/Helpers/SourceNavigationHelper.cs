using System;
using System.Windows;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Sources.Views;

namespace Sources.Helpers;

/// <summary>
/// مساعد للتنقل وفتح نوافذ تفاصيل المصادر المشعة والنيترونية
/// </summary>
public static class SourceNavigationHelper
{
    public static Action<Source>? CustomOpenAction { get; set; }

    /// <summary>فتح نافذة تفاصيل مصدر مشع</summary>
    public static void OpenSourceDetails(object? parameter, Guid? sourceId = null)
    {
        Source? source = parameter switch
        {
            Source s => s,
            DeletedSourceRow dsr => dsr.Source,
            DashboardSourceRow dsr => dsr.Source,
            AlertRow ar => ar.Source ?? (ar.SourceId.HasValue ? GetSourceById(ar.SourceId.Value) : null),
            LocationSourceRow lsr => lsr.Source,
            BorrowRequestRow brr => brr.Source ?? (brr.Request.SourceId != Guid.Empty ? GetSourceById(brr.Request.SourceId) : null),
            LeakTestRecord ltr => ltr.Source ?? (ltr.SourceId != Guid.Empty ? GetSourceById(ltr.SourceId) : null),
            ReportInventoryRow rir => rir.Source,
            ReportBorrowingRow rbr => rbr.Source ?? (rbr.Request.SourceId != Guid.Empty ? GetSourceById(rbr.Request.SourceId) : null),
            ReportActivityRow rar => rar.Source,
            ReportLowActivityRow rlar => rlar.Source,
            ReportLowActivityAlertRow rlaar => rlaar.Source,
            string code => GetSourceByCode(code),
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

    /// <summary>فتح نافذة تفاصيل مصدر نيتروني</summary>
    public static void OpenNeutronSourceDetails(object? parameter, Guid? sourceId = null)
    {
        NeutronSource? source = parameter switch
        {
            NeutronSource ns => ns,
            ReportNeutronInventoryRow rnir => rnir.Source,
            LocationNeutronSourceRow lnsr => lnsr.NeutronSource,
            Guid id => GetNeutronSourceById(id),
            _ => sourceId.HasValue ? GetNeutronSourceById(sourceId.Value) : null
        };

        if (source == null && sourceId.HasValue)
        {
            source = GetNeutronSourceById(sourceId.Value);
        }

        if (source == null)
        {
            DialogHelper.ShowWarning(
                TranslationHelper.GetString("MsgNeutronSourceNotFound") ?? "المصدر النيتروني غير موجود أو تم حذفه من المنظومة.",
                TranslationHelper.GetString("TitleNeutronSourceDetails") ?? "تفاصيل المصدر النيتروني");
            return;
        }

        if (DialogHelper.IsTestMode) return;

        var app = Application.Current;
        if (app == null) return;

        if (app.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(() => OpenNeutronSourceDetails(source));
            return;
        }

        try
        {
            var userService = App.ServiceProvider?.GetService(typeof(IUserService)) as IUserService;
            var viewModel = new NeutronSourceDetailsViewModel(source, userService);
            var window = new NeutronSourceDetailsWindow(viewModel);

            if (app.MainWindow != null && app.MainWindow.IsVisible && app.MainWindow != window)
            {
                window.Owner = app.MainWindow;
            }

            window.ShowDialog();
        }
        catch (Exception ex)
        {
            LoggerService.LogError("SourceNavigationHelper: Failed to open NeutronSourceDetailsWindow", ex);
        }
    }

    /// <summary>جلب مصدر نيتروني بالمعرف</summary>
    private static NeutronSource? GetNeutronSourceById(Guid id)
    {
        try
        {
            var service = App.ServiceProvider?.GetService(typeof(INeutronSourceService)) as INeutronSourceService;
            return service?.GetById(id);
        }
        catch (Exception ex)
        {
            LoggerService.LogError("SourceNavigationHelper: Failed to get neutron source by ID", ex);
            return null;
        }
    }

    /// <summary>جلب مصدر مشع بالمعرف</summary>
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

    /// <summary>جلب مصدر مشع بالكود</summary>
    private static Source? GetSourceByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        try
        {
            var service = App.ServiceProvider?.GetService(typeof(ISourceService)) as ISourceService;
            return service?.GetAllSources().FirstOrDefault(s => s.SourceCode.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            LoggerService.LogError("SourceNavigationHelper: Failed to get source by code", ex);
            return null;
        }
    }
}
