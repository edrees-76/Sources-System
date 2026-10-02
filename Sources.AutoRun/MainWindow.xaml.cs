using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sources.AutoRun.Logic;

namespace Sources.AutoRun;

public partial class MainWindow : Window
{
    private const int ErrorCancelledByUser = 1223; // ERROR_CANCELLED: رفض المستخدم نافذة UAC

    private readonly string _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    private readonly LauncherConfig _config;
    private readonly string? _installerPath;
    private LauncherLanguage _language;

    public MainWindow()
    {
        InitializeComponent();

        _config = LauncherConfig.Load(_baseDirectory);
        _installerPath = PackageLocator.FindInstaller(_baseDirectory);
        _language = LauncherLanguageExtensions.Resolve(
            LoadSavedLanguage(), CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

        LogoImage.Source = LoadLogo();
        ApplyLanguage();
    }

    private static string LanguageFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SourcesAutoRun", "language.txt");

    private static string? LoadSavedLanguage()
    {
        try
        {
            return File.Exists(LanguageFilePath) ? File.ReadAllText(LanguageFilePath) : null;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Trace.TraceWarning("AutoRun: failed to read saved language: " + ex.Message);
            return null;
        }
    }

    private void SaveLanguage()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LanguageFilePath)!);
            File.WriteAllText(LanguageFilePath, _language.Code());
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            // فشل خلفي لا يخص إجراءً طلبه المستخدم: تُحفظ اللغة في الجلسة فقط ويُسجَّل السبب.
            Trace.TraceWarning("AutoRun: failed to save language: " + ex.Message);
        }
    }

    private static ImageSource LoadLogo()
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri("pack://application:,,,/Assets/sources_logo.png");
        image.DecodePixelWidth = 300;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private string T(string key) => LauncherStrings.Get(_language, key);

    private void ApplyLanguage()
    {
        var isArabic = _language == LauncherLanguage.Arabic;
        FlowDirection = isArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(isArabic ? "ar-SA" : "en-US");

        Title = T(LauncherStrings.WindowTitle);
        SubtitleText.Text = T(LauncherStrings.Subtitle);
        LanguageButton.Content = T(LauncherStrings.LanguageToggle);
        InstallTitleText.Text = T(LauncherStrings.InstallTitle);
        InstallDescText.Text = T(LauncherStrings.InstallDescription);
        InstallButton.Content = T(LauncherStrings.InstallButton);
        GuideTitleText.Text = T(LauncherStrings.GuideTitle);
        GuideDescText.Text = T(LauncherStrings.GuideDescription);
        GuideButton.Content = T(LauncherStrings.GuideButton);
        ActivationTitleText.Text = T(LauncherStrings.ActivationTitle);
        ActivationDescText.Text = T(LauncherStrings.ActivationDescription);
        CopyButton.Content = T(LauncherStrings.CopyButton);

        var version = _installerPath == null ? null : PackageLocator.ParseVersion(_installerPath);
        VersionText.Text = version == null
            ? string.Empty
            : LauncherStrings.Format(_language, LauncherStrings.VersionLabel, "v" + version);

        if (_config.ActivationCode == null)
        {
            ActivationBox.Text = T(LauncherStrings.ActivationUnavailable);
            ActivationBox.FontFamily = new FontFamily("Segoe UI");
            ActivationBox.FontSize = 15;
            ActivationBox.FlowDirection = FlowDirection;
            CopyButton.IsEnabled = false;
        }
        else
        {
            ActivationBox.Text = _config.ActivationCode;
        }

        StatusText.Text = string.Empty;
    }

    private void SetStatus(string text, string brushKey)
    {
        StatusText.Foreground = (Brush)FindResource(brushKey);
        StatusText.Text = text;
    }

    private void ShowError(string message)
    {
        var options = _language == LauncherLanguage.Arabic
            ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
            : MessageBoxOptions.None;
        MessageBox.Show(this, message, T(LauncherStrings.ErrorTitle),
            MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, options);
    }

    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        _language = _language.Other();
        SaveLanguage();
        ApplyLanguage();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_config.ActivationCode == null) return;

        try
        {
            Clipboard.SetDataObject(_config.ActivationCode, true);
            SetStatus(T(LauncherStrings.Copied), "SuccessBrush");
        }
        catch (ExternalException ex)
        {
            Trace.TraceWarning("AutoRun: clipboard failure: " + ex.Message);
            ShowError(T(LauncherStrings.ErrClipboard));
        }
    }

    private void GuideButton_Click(object sender, RoutedEventArgs e)
    {
        var path = PackageLocator.GuidePath(_baseDirectory, _language);
        if (!File.Exists(path))
        {
            ShowError(T(LauncherStrings.ErrGuideMissing));
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            Trace.TraceWarning("AutoRun: failed to open guide: " + ex.Message);
            ShowError(T(LauncherStrings.ErrGuideOpen));
        }
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_installerPath == null)
        {
            ShowError(T(LauncherStrings.ErrInstallerMissing));
            return;
        }

        InstallButton.IsEnabled = false;
        SetStatus(T(LauncherStrings.Verifying), "GemBlueBrush");
        try
        {
            // حساب SHA-256 لملف ~80MB قد يستغرق ثوانٍ على قرص/فلاشة بطيئة: خارج خيط الواجهة.
            var status = await Task.Run(() => ChecksumVerifier.Verify(_installerPath));
            if (status != ChecksumStatus.Ok)
            {
                StatusText.Text = string.Empty;
                ShowError(T(ErrorKeyFor(status)));
                return;
            }

            LaunchInstaller(_installerPath);
        }
        finally
        {
            InstallButton.IsEnabled = true;
        }
    }

    private static string ErrorKeyFor(ChecksumStatus status)
    {
        switch (status)
        {
            case ChecksumStatus.FileMissing: return LauncherStrings.ErrInstallerMissing;
            case ChecksumStatus.ChecksumFileMissing: return LauncherStrings.ErrChecksumMissing;
            case ChecksumStatus.ChecksumFileInvalid: return LauncherStrings.ErrChecksumInvalid;
            case ChecksumStatus.Unreadable: return LauncherStrings.ErrInstallerUnreadable;
            default: return LauncherStrings.ErrChecksumMismatch;
        }
    }

    private void LaunchInstaller(string installerPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo(installerPath)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(installerPath)
            });
            // تبقى الواجهة مفتوحة ليتمكن المستخدم من نسخ رقم التفعيل أثناء التثبيت.
            SetStatus(T(LauncherStrings.InstallerStarted), "SuccessBrush");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelledByUser)
        {
            SetStatus(T(LauncherStrings.InstallCancelled), "MutedTextBrush");
        }
        catch (Win32Exception ex)
        {
            Trace.TraceWarning("AutoRun: failed to start installer: " + ex.Message);
            StatusText.Text = string.Empty;
            ShowError(LauncherStrings.Format(_language, LauncherStrings.ErrInstallerLaunch, ex.Message));
        }
    }
}
