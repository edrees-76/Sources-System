using System;
using System.Windows;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MaterialDesignThemes.Wpf;
using Sources.ViewModels;
using Sources.Services;
using Sources.Helpers;

namespace Sources;

public partial class App : Application
{
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) => 
        {
            try {
                var ex = args.ExceptionObject as Exception;
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), ex?.ToString());
            } catch { } // Silently handling errors in the global error handler
        };
    }

    public static IServiceProvider ServiceProvider { get; private set; } = null!;
    private static System.Threading.Mutex? _mutex;

    public static Sources.Data.AppDbContext CreateDbContext()
    {
        return ServiceProvider.GetRequiredService<IDbContextFactory<Sources.Data.AppDbContext>>().CreateDbContext();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // ضبط الثقافة لتكون لغة التطبيق عربية، ولكن لغة ربط البيانات (WPF Bindings) إنجليزية (en-GB)
        // en-GB تضمن قراءة الأرقام بنقطة (1.25) وبنفس الوقت تحتفظ بتنسيق التاريخ (يوم/شهر/سنة)
        var culture = new System.Globalization.CultureInfo("ar-LY");
        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
        
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage("en-GB")));

        // منع تشغيل نسخة ثانية
        _mutex = new System.Threading.Mutex(true, "{Sources-RST-2026-UNIQUE-MUTEX}", out bool createdNew);
        if (!createdNew)
        {
            DialogHelper.ShowWarning("البرنامج قيد التشغيل بالفعل.", "تنبيه");
            Shutdown();
            return;
        }

        // معالجة الأخطاء العامة
        AppDomain.CurrentDomain.UnhandledException += (s, args) => HandleGlobalException(args.ExceptionObject as Exception);
        this.DispatcherUnhandledException += (s, ev) => { HandleGlobalException(ev.Exception); ev.Handled = true; };
        TaskScheduler.UnobservedTaskException += (s, ev) => { HandleGlobalException(ev.Exception); ev.SetObserved(); };

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        try
        {
            using (var db = CreateDbContext())
            {
                db.InitializeDatabase();
            }

            // تطبيق الإعدادات المحفوظة
            ApplyTheme(SettingsHelper.IsDarkMode);
            ApplyLanguage(SettingsHelper.Language);

            LoggerService.LogInfo("تم بدء تشغيل منظومة مصادر بنجاح");
            base.OnStartup(e);

            var splash = ServiceProvider.GetRequiredService<Sources.Views.SplashWindow>();
            splash.Show();
        }
        catch (Exception ex)
        {
            LoggerService.LogError("فشل بدء التشغيل", ex);
            DialogHelper.ShowError($"فشل بدء التشغيل: {ex.Message}", "خطأ");
            Shutdown();
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Database
        services.AddDbContextFactory<Sources.Data.AppDbContext>();

        // Views
        services.AddTransient<Sources.Views.SplashWindow>();
        services.AddTransient<Sources.Views.LoginWindow>();
        services.AddTransient<Sources.Views.ScreensaverWindow>();
        services.AddTransient<MainWindow>();

        // Services
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<ISystemSettingsService, SystemSettingsService>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<IDecayCalculationService, DecayCalculationService>();
        services.AddSingleton<IAlertService, AlertService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IReportingService, ReportingService>();
        services.AddTransient<ISourceService, SourceService>();
        services.AddTransient<IRadioisotopeService, RadioisotopeService>();
        services.AddTransient<ILocationService, LocationService>();
        services.AddTransient<IIsotopeImportService, IsotopeImportService>();
        services.AddTransient<IBorrowService, BorrowService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SourcesViewModel>();
        services.AddTransient<RadioisotopesViewModel>();
        services.AddTransient<LocationsViewModel>();
        services.AddTransient<BorrowViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ActivityCalculatorViewModel>();
        services.AddTransient<HelpViewModel>();
        services.AddTransient<AboutSystemViewModel>();
    }

    public static void ApplyLanguage(string cultureCode)
    {
        var app = Current;
        if (app == null) return;

        // تحديث الثقافة للعمليات الخلفية (التاريخ والأرقام)
        var culture = new System.Globalization.CultureInfo(cultureCode == "ar" ? "ar-LY" : "en-US");
        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;

        // تبديل قاموس النصوص
        var dicts = app.Resources.MergedDictionaries;
        var targetDictName = cultureCode == "ar" ? "Strings.ar.xaml" : "Strings.en.xaml";
        var oldDictName = cultureCode == "ar" ? "Strings.en.xaml" : "Strings.ar.xaml";
        var newDict = new ResourceDictionary { Source = new Uri($"/Resources/{targetDictName}", UriKind.RelativeOrAbsolute) };

        bool found = false;
        for (int i = 0; i < dicts.Count; i++)
        {
            var src = dicts[i].Source?.OriginalString;
            if (src != null && src.Contains("Strings."))
            {
                dicts[i] = newDict;
                found = true;
                break;
            }
        }
        if (!found) dicts.Add(newDict);

        // تحديث اتجاه الواجهة في النافذة الرئيسية
        if (app.MainWindow != null)
        {
            var newFlowDirection = cultureCode == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            app.MainWindow.FlowDirection = newFlowDirection;
            app.MainWindow.Language = System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag);

            // إصلاح مشكلة WPF: إعادة تعيين المحتوى لضمان تحديث محاذاة القائمة الجانبية (Grid/DockPanel)
            var content = app.MainWindow.Content;
            app.MainWindow.Content = null;
            app.MainWindow.Content = content;
        }
    }

    public static void ApplyTheme(bool isDark)
    {
        var app = Current;
        if (app == null) return;

        // تحديث MaterialDesign BaseTheme
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
        paletteHelper.SetTheme(theme);

        // تبديل قاموس الثيم المخصص
        var dicts = app.Resources.MergedDictionaries;
        var targetDictName = isDark ? "DarkTheme.xaml" : "LightTheme.xaml";
        var oldDictName = isDark ? "LightTheme.xaml" : "DarkTheme.xaml";
        var newDict = new ResourceDictionary { Source = new Uri($"/Resources/{targetDictName}", UriKind.RelativeOrAbsolute) };

        for (int i = 0; i < dicts.Count; i++)
        {
            var src = dicts[i].Source?.OriginalString;
            if (src != null && src.Contains(oldDictName))
            {
                dicts[i] = newDict;
                return;
            }
        }
    }

    private void HandleGlobalException(Exception? ex)
    {
        if (ex == null) return;
        LoggerService.LogError("[Global] خطأ غير متوقع", ex);

        // التغاضي عن أخطاء إغلاق النوافذ أثناء خروج المنظومة
        if (ex is InvalidOperationException && ex.Message.Contains("closing")) return;

        try
        {
            DialogHelper.ShowError($"حدث خطأ غير متوقع:\n{ex.Message}", "خطأ في المنظومة");
        }
        catch { }
    }
}
