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
            ApplyAccentColor(SettingsHelper.AccentColor);
            ApplyLanguage(SettingsHelper.Language);

            // بدء خدمة النسخ الاحتياطي التلقائي في الخلفية
            ServiceProvider.GetRequiredService<IAutoBackupService>().Start();

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
        services.AddTransient<Sources.Views.LocationDetailsWindow>();
        services.AddTransient<Sources.Views.SourceDetailsWindow>();
        services.AddTransient<Sources.Views.AlertsView>();
        services.AddTransient<Sources.Views.LeakTestsView>();
        services.AddTransient<Sources.Views.IsotopeLibraryView>();
        services.AddTransient<Sources.Views.DeletionsView>();
        services.AddTransient<MainWindow>();

        // Services
        services.AddSingleton<IUserService>(sp => new UserService(sp.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Sources.Data.AppDbContext>>()));
        services.AddSingleton<ISystemSettingsService, SystemSettingsService>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<IDecayCalculationService, DecayCalculationService>();
        services.AddSingleton<IAlertService, AlertService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IAutoBackupService, AutoBackupService>();
        services.AddSingleton<IReportingService, ReportingService>();
        services.AddSingleton<IIsotopeLibraryService, IsotopeLibraryService>();
        services.AddTransient<ISourceService, SourceService>();
        services.AddTransient<IRadioisotopeService, RadioisotopeService>();
        services.AddTransient<ILocationService, LocationService>();
        services.AddTransient<IIsotopeImportService, IsotopeImportService>();
        services.AddTransient<IBorrowService, BorrowService>();
        services.AddTransient<ILeakTestService, LeakTestService>();
        services.AddTransient<ISystemResetService, SystemResetService>();
        services.AddTransient<IGlobalSearchService, GlobalSearchService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SourcesViewModel>();
        services.AddTransient<RadioisotopesViewModel>();
        services.AddTransient<LocationsViewModel>();
        services.AddTransient<BorrowViewModel>();
        services.AddTransient<LeakTestsViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<AlertsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ActivityCalculatorViewModel>();
        services.AddTransient<IsotopeLibraryViewModel>();
        services.AddTransient<HelpViewModel>();
        services.AddTransient<AboutSystemViewModel>();
        services.AddTransient<DeletionsViewModel>();
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
        try
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
            paletteHelper.SetTheme(theme);
        }
        catch { }

        // تبديل قاموس الثيم المخصص
        try
        {
            var dicts = app.Resources.MergedDictionaries;
            var targetDictName = isDark ? "DarkTheme.xaml" : "LightTheme.xaml";
            var oldDictName = isDark ? "LightTheme.xaml" : "DarkTheme.xaml";
            var uri = new Uri($"pack://application:,,,/Sources;component/Resources/{targetDictName}", UriKind.RelativeOrAbsolute);
            var newDict = new ResourceDictionary { Source = uri };

            for (int i = 0; i < dicts.Count; i++)
            {
                var src = dicts[i].Source?.OriginalString;
                if (src != null && src.Contains(oldDictName))
                {
                    dicts[i] = newDict;
                    break;
                }
            }
        }
        catch { }

        // الحفاظ على لون التمييز المطبق حالياً
        if (app.Resources.Contains("PrimaryColor") && app.Resources["PrimaryColor"] is System.Windows.Media.Color activeColor)
        {
            try
            {
                var paletteHelper = new PaletteHelper();
                var themeAfter = paletteHelper.GetTheme();
                themeAfter.SetPrimaryColor(activeColor);
                themeAfter.SetSecondaryColor(activeColor);
                paletteHelper.SetTheme(themeAfter);
            }
            catch { }
        }
    }

    public static void ApplyAccentColor(string hexColor)
    {
        var app = Current;
        if (app == null) return;

        if (string.IsNullOrWhiteSpace(hexColor))
            hexColor = SettingsHelper.DefaultAccentColor;

        try
        {
            var primaryColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
            
            // حساب تدرجات الألوان المتناسقة (فاتح وداكن)
            var primaryLight = System.Windows.Media.Color.FromArgb(255, 
                (byte)Math.Min(255, primaryColor.R + 30), 
                (byte)Math.Min(255, primaryColor.G + 34), 
                (byte)Math.Min(255, primaryColor.B + 35));
                
            var primaryDark = System.Windows.Media.Color.FromArgb(255, 
                (byte)Math.Max(0, primaryColor.R - 13), 
                (byte)Math.Max(0, primaryColor.G - 34), 
                (byte)Math.Max(0, primaryColor.B - 38));

            // 1. تحديث MaterialDesign Themes
            try
            {
                var paletteHelper = new PaletteHelper();
                var theme = paletteHelper.GetTheme();
                theme.SetPrimaryColor(primaryColor);
                theme.SetSecondaryColor(primaryColor);
                paletteHelper.SetTheme(theme);
            }
            catch { }

            // 2. تحديث الموارد المركزية DynamicResource في Application.Current.Resources
            app.Resources["PrimaryColor"] = primaryColor;
            app.Resources["PrimaryLightColor"] = primaryLight;
            app.Resources["PrimaryDarkColor"] = primaryDark;
            app.Resources["SecondaryColor"] = primaryColor;
            app.Resources["GoldColor"] = primaryColor;
            app.Resources["GoldLightColor"] = primaryLight;
            app.Resources["GoldDarkColor"] = primaryDark;
            app.Resources["AccentColor"] = primaryColor;

            app.Resources["PrimaryBrush"] = new System.Windows.Media.SolidColorBrush(primaryColor);
            app.Resources["PrimaryLightBrush"] = new System.Windows.Media.SolidColorBrush(primaryLight);
            app.Resources["PrimaryDarkBrush"] = new System.Windows.Media.SolidColorBrush(primaryDark);
            app.Resources["SecondaryBrush"] = new System.Windows.Media.SolidColorBrush(primaryColor);
            app.Resources["GoldBrush"] = new System.Windows.Media.SolidColorBrush(primaryColor);
            app.Resources["GoldLightBrush"] = new System.Windows.Media.SolidColorBrush(primaryLight);
            app.Resources["GoldDarkBrush"] = new System.Windows.Media.SolidColorBrush(primaryDark);
            app.Resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(primaryColor);

            var primaryGradient = new System.Windows.Media.LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1),
                GradientStops = new System.Windows.Media.GradientStopCollection
                {
                    new System.Windows.Media.GradientStop(primaryColor, 0),
                    new System.Windows.Media.GradientStop(primaryLight, 1)
                }
            };
            app.Resources["PrimaryGradient"] = primaryGradient;
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Error applying accent color", ex);
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
