using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sources.Models;
using Sources.Services;
using Sources.Helpers;
using Sources.Messages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Windows;

namespace Sources.ViewModels;


/// <summary>
/// نموذج لمفتاح الرسم المخصص لضمان التباين
/// </summary>
public class LegendItem
{
    public string Label { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFFFFF";
}

/// <summary>
/// صف عرض مخصص لجدول المصادر في لوحة التحكم يضمن ثبات الرقم التسلسلي بشكل غير قابل للتغير أثناء التمرير
/// </summary>
public class DashboardSourceRow
{
    public int RowNumber { get; set; }
    public Source Source { get; set; } = null!;
    public string SerialNumber => Source.SerialNumber ?? "—";
    public string SourceCode => Source.SourceCode;
    public string DisplayIsotopes => Source.DisplayIsotopes;
    public string LocationName => Source.Location?.LocationName ?? "—";
    public string CurrentActivityDisplay => Source.CurrentActivityDisplay;
    public string ActivityUnitSymbol => Source.CurrentActivityUnit?.UnitSymbol ?? "—";
    public string DisplayDoseRate => Source.DisplayDoseRate;
    public string DoseRateTooltip => Source.DoseRateTooltip;

    /// <summary>أسوأ (أخطر) فئة رقابية من بين نظائر المصدر — القيمة الأصغر هي الأخطر</summary>
    public int WorstCategory
    {
        get
        {
            if (Source.HasDetailedIsotopes && Source.SourceIsotopes?.Any() == true)
                return Source.SourceIsotopes
                    .Where(si => si.Radioisotope != null)
                    .Select(si => si.Radioisotope!.Category)
                    .Where(c => c > 0)
                    .DefaultIfEmpty(5)
                    .Min();
            return Source.Radioisotope?.Category > 0 ? Source.Radioisotope.Category : 5;
        }
    }

    public string CategoryColor => DashboardViewModel.GetCategoryColor(WorstCategory);
}

/// <summary>
/// صف مصدر منخفض النشاط في الجدول المضغوط بلوحة القيادة
/// </summary>
public class LowActivitySourceRow
{
    public string SourceCode { get; set; } = string.Empty;
    public string IsotopeSymbol { get; set; } = string.Empty;
    public double HalfLivesElapsed { get; set; }
    public string HalfLivesDisplay { get; set; } = string.Empty; // "5.3 T½"
    public string Severity { get; set; } = "Warning"; // "Warning" | "Critical"
    public string SeverityColor { get; set; } = "#E0A93E";          // foreground hex
    public string SeverityBadgeBackground { get; set; } = "#1AE0A93E"; // badge bg (10% alpha)
    public string SeverityLabel { get; set; } = string.Empty;
}

/// <summary>
/// ملخص بيانات الاستعارات للبطاقة في لوحة القيادة
/// </summary>
public class DashboardBorrowSummary
{
    public int OverdueCount { get; set; }
    public int DueSoonCount { get; set; }
    public int ActiveCount { get; set; }
    public bool HasAny => OverdueCount > 0 || DueSoonCount > 0 || ActiveCount > 0;
}

/// <summary>
/// نموذج لمؤشر الفئة الرقابية
/// </summary>
public class CategoryBadgeInfo
{
    public int Category { get; set; }
    public string Color { get; set; } = "#B0BEC5";
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// صف في Side Panel لعرض الكل (نظائر أو مواقع)
/// </summary>
public class DistributionRow
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Percent { get; set; } = "0%";
}

/// <summary>
/// نطاق النشاط (Bin) في الـ Histogram
/// </summary>
public class ActivityBinInfo
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public double LowerBound { get; set; }
    public double UpperBound { get; set; }
}

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private static readonly string[] ChartPalette = new[]
    {
        "#1F5A66", // Petrol Blue (Primary)
        "#C97A4A", // Terracotta (Accent)
        "#3FAE7A", // Emerald (Success)
        "#E0A93E", // Gold (Warning)
        "#4F7FA3", // Steel Blue (Info)
        "#8E44AD", // Purple
        "#D35400", // Rust
        "#16A085"  // Teal
    };

    private readonly ISourceService _sourceService;
    private readonly IRadioisotopeService _isotopeService;
    private readonly ILocationService _locationService;
    private readonly IDecayCalculationService _decayService;
    private readonly IBorrowService _borrowService;
    private readonly ISystemSettingsService _settingsService;
    private readonly IAlertService? _alertService;
    private readonly IGlobalSearchService _globalSearchService;

    // ─── البحث الموحّد في لوحة القيادة (Global Search) ───
    [ObservableProperty] private string _globalSearchQuery = string.Empty;
    [ObservableProperty] private bool _isGlobalSearchResultsOpen;
    [ObservableProperty] private bool _isGlobalSearching;
    [ObservableProperty] private int _totalGlobalSearchResultsCount;
    [ObservableProperty] private ObservableCollection<GlobalSearchResultGroup> _globalSearchResultGroups = new();
    [ObservableProperty] private GlobalSearchResultItem? _selectedGlobalSearchResultItem;
    private System.Threading.CancellationTokenSource? _globalSearchCts;

    // ─── ساعة وتاريخ الداشبورد المباشرة ───
    [ObservableProperty] private string _currentDateDisplay = string.Empty;
    [ObservableProperty] private string _currentTimeDisplay = string.Empty;
    private System.Windows.Threading.DispatcherTimer? _clockTimer;

    // ─── بطاقة 1: عدد المصادر ───
    [ObservableProperty] private int _totalSources;
    [ObservableProperty] private bool _isArabic;
    [ObservableProperty] private Thickness _axisOverlayMargin;
    [ObservableProperty] private Thickness _axisOverlayThickness;
    [ObservableProperty] private int _yAxisLabelColumn;
    [ObservableProperty] private int _chartColumn;

    // ─── بطاقة 2: إجمالي النشاط بجميع الوحدات ───
    [ObservableProperty] private string _totalActivityDisplay = string.Empty;
    [ObservableProperty] private string _activityChangePercent = string.Empty;
    [ObservableProperty] private string _activityChangeColor = "#4CAF50"; // Green for positive, Red for negative
    [ObservableProperty] private string _activityChangeIcon = "ArrowTopRight"; // ArrowTopRight or ArrowBottomRight
    [ObservableProperty] private ObservableCollection<TotalActivityItem> _totalActivityItems = new();

    // ─── رسم Histogram: توزيع نطاقات النشاط (البند 1) ───
    [ObservableProperty] private ISeries[] _activityHistogramSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _histogramXAxes = new Axis[] { new Axis() };
    [ObservableProperty] private Axis[] _histogramYAxes = new Axis[] { new Axis() };
    private Dictionary<int, List<Source>> _histogramBinSources = new();

    // ─── مخطط شريطي أفقي: توزيع النظائر (Top-10 + Others) ───
    [ObservableProperty] private ISeries[] _sourcesByIsotopeSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _isotopeXAxes = new Axis[] { new Axis() };
    [ObservableProperty] private Axis[] _isotopeYAxes = new Axis[] { new Axis() };
    [ObservableProperty] private bool _hasEnoughIsotopeData;

    // ─── مخطط شريطي أفقي: توزيع المواقع (Top-10 + Others) ───
    [ObservableProperty] private ISeries[] _sourcesByLocationSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _locationXAxes = new Axis[] { new Axis() };
    [ObservableProperty] private Axis[] _locationYAxes = new Axis[] { new Axis() };
    [ObservableProperty] private bool _hasEnoughLocationData;
    [ObservableProperty] private ObservableCollection<LegendItem> _locationLegendItems = new();

    // بيانات مخزنة لقوائم "عرض الكل"
    private List<(string Label, int Count, double Percent)> _allIsotopeData = new();
    private List<(string Label, int Count, double Percent)> _allLocationData = new();

    // ─── منحنى التحلل الزمني ───
    [ObservableProperty] private ISeries[] _activityDecaySeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _decayXAxes = new Axis[] { new Axis() };
    [ObservableProperty] private Axis[] _decayYAxes = new Axis[] { new Axis() };

    // ─── قائمة المصادر المتاحة لمنحنى التحلل ───
    [ObservableProperty] private ObservableCollection<Source> _availableSources = new();
    [ObservableProperty] private Source? _selectedDecaySource;

    // ─── مصادر لوحة القيادة — الصفحة المعروضة حالياً ───
    [ObservableProperty] private ObservableCollection<DashboardSourceRow> _dashboardSources = new();

    // ─── Side Panel (البند 4) ───
    [ObservableProperty] private bool _isSidePanelOpen;
    [ObservableProperty] private string _sidePanelTitle = string.Empty;
    [ObservableProperty] private ObservableCollection<DashboardSourceRow> _sidePanelSources = new();
    [ObservableProperty] private ObservableCollection<DistributionRow> _sidePanelDistribution = new();
    [ObservableProperty] private bool _sidePanelShowSources; // true → DataGrid, false → Distribution table
    
    // ─── بحث + فلاتر + Pagination (البند 3) ───
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedIsotopeFilter = string.Empty;
    [ObservableProperty] private string _selectedLocationFilter = string.Empty;
    [ObservableProperty] private string _selectedStatusFilter = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _availableIsotopes = new();
    [ObservableProperty] private ObservableCollection<string> _availableLocations = new();
    [ObservableProperty] private ObservableCollection<string> _availableStatuses = new();
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _pageSize = 50;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _totalFilteredCount;
    [ObservableProperty] private string _pageInfo = "1 / 1";
    private List<DashboardSourceRow> _allSourceRows = new();
    private System.Timers.Timer? _searchDebounceTimer;

    // ─── تلوين الفئة الرقابية (البند 6) ───
    [ObservableProperty] private ObservableCollection<CategoryBadgeInfo> _categoryLegend = new();

    // ─── الجزء 1: بطاقة تنبيهات انخفاض النشاط ───
    [ObservableProperty] private int _lowActivityCriticalCount;
    [ObservableProperty] private int _lowActivityWarningCount;
    [ObservableProperty] private bool _hasLowActivityAlerts;   // false → رسالة "ضمن الحدود الآمنة"

    // ─── الجزء 1b: بطاقة تنبيهات اختبارات التسرب ───
    [ObservableProperty] private int _leakTestCriticalCount;
    [ObservableProperty] private int _leakTestWarningCount;
    [ObservableProperty] private bool _hasLeakTestAlerts;

    // ─── الجزء 2: بطاقة ملخص الاستعارات ───
    [ObservableProperty] private DashboardBorrowSummary _borrowSummary = new();

    // ─── الجزء 3: جدول مصادر منخفضة النشاط ───
    [ObservableProperty] private ObservableCollection<LowActivitySourceRow> _lowActivitySources = new();
    [ObservableProperty] private bool _hasMoreLowActivitySources;  // true → عرض "عرض الكل"
    [ObservableProperty] private int _totalLowActivityCount;

    // ─── دهان نصوص التلميحات ───
    public SolidColorPaint ChartTextPaint => GetAxisPaint();
    public SolidColorPaint ChartTooltipBackgroundPaint => GetTooltipBackgroundPaint();

    // ─── تلميحات الرسوم البيانية الذكية (Auto-Flip & Clamping) ───
    public IChartTooltip<SkiaSharpDrawingContext> IsotopeChartTooltip => new AutoFlipChartTooltip
    {
        FontPaint = ChartTextPaint,
        BackgroundPaint = ChartTooltipBackgroundPaint,
        TextSize = 12
    };

    public IChartTooltip<SkiaSharpDrawingContext> LocationChartTooltip => new AutoFlipChartTooltip
    {
        FontPaint = ChartTextPaint,
        BackgroundPaint = ChartTooltipBackgroundPaint,
        TextSize = 12
    };

    public IChartTooltip<SkiaSharpDrawingContext> DefaultChartTooltip => new AutoFlipChartTooltip
    {
        FontPaint = ChartTextPaint,
        BackgroundPaint = ChartTooltipBackgroundPaint,
        TextSize = 12
    };

    // ─── مجموعات مفاتيح الرسم المخصصة ───
    [ObservableProperty] private ObservableCollection<LegendItem> _pieLegendItems = new();
    [ObservableProperty] private ObservableCollection<LegendItem> _decayLegendItems = new();

    // فرشاة المحاور (لـ WPF Overlay)
    [ObservableProperty] private System.Windows.Media.Brush _axisBrush = System.Windows.Media.Brushes.Transparent;

    // هامش الرسم الموحد لضمان انطباق الخطوط اليدوية (L-shape) مع محاور الرسم
    public LiveChartsCore.Measure.Margin ChartDrawMargin { get; } = new(50, 20, 20, 50);
    [ObservableProperty] private LiveChartsCore.Measure.Margin _barDrawMargin = new(70, 20, 30, 50);
    [ObservableProperty] private LiveChartsCore.Measure.Margin _isotopeDrawMargin = new(75, 20, 30, 40);
    [ObservableProperty] private LiveChartsCore.Measure.Margin _locationDrawMargin = new(135, 20, 30, 40);

    // إطار الرسم - نجعله شفافاً تماماً لأننا سنرسم المحاور يدوياً بشكل L في الـ XAML
    [ObservableProperty] private DrawMarginFrame? _decayDrawMarginFrame = new DrawMarginFrame { Stroke = null };
    [ObservableProperty] private DrawMarginFrame? _barDrawMarginFrame = new DrawMarginFrame { Stroke = null };
    [ObservableProperty] private DrawMarginFrame? _isotopeDrawMarginFrame = new DrawMarginFrame { Stroke = null };
    [ObservableProperty] private DrawMarginFrame? _locationDrawMarginFrame = new DrawMarginFrame { Stroke = null };

    // ألوان متعددة لمنحنيات التحلل من لوحة الألوان المعتمدة (Colors.xaml)
    private static readonly string[] DecayStrokeColors = { "#1F5A66", "#C97A4A", "#3FAE7A", "#4F7FA3", "#8E44AD" };
    private static readonly string[] DecayFillColors = { "#1A1F5A66", "#1AC97A4A", "#1A3FAE7A", "#1A4F7FA3", "#1A8E44AD" };

    // ─── ألوان الفئات الرقابية (البند 6) ───
    public static string GetCategoryColor(int category) => category switch
    {
        1 => "#8B0000",  // أحمر غامق
        2 => "#D84315",  // برتقالي محمر
        3 => "#FB8C00",  // برتقالي فاتح
        4 => "#FFD600",  // أصفر
        5 => "#B0BEC5",  // رمادي فاتح
        _ => "#B0BEC5"
    };

    // ─── نطاقات الـ Histogram الثابتة (البند 1) ───
    public static readonly (double Lower, double Upper, string Label)[] HistogramBins = new[]
    {
        (0.0,    1e3,  "< 10³"),
        (1e3,    1e6,  "10³ – 10⁶"),
        (1e6,    1e9,  "10⁶ – 10⁹"),
        (1e9,    1e12, "10⁹ – 10¹²"),
        (1e12,   1e15, "10¹² – 10¹⁵"),
        (1e15,   double.MaxValue, "> 10¹⁵")
    };

    // دهان النصوص في الرسوم البيانية - أزرق للوضع الفاتح، أبيض للوضع الداكن
    private static SolidColorPaint GetAxisPaint()
    {
        var isDark = SettingsHelper.IsDarkMode;
        var color = isDark ? new SKColor(220, 220, 220) : new SKColor(70, 70, 70);
        return new SolidColorPaint(color);
    }

    private static SolidColorPaint GetTooltipBackgroundPaint()
    {
        var isDark = SettingsHelper.IsDarkMode;
        // خلفية داكنة في الوضع الداكن وخلفية فاتحة في الفاتح
        var color = isDark ? new SKColor(45, 45, 45) : new SKColor(250, 250, 250);
        return new SolidColorPaint(color);
    }

    // دهان خطوط المحاور - أزرق للوضع الفاتح، أبيض للوضع الداكن
    private static SolidColorPaint GetAxisLinePaint()
    {
        var color = SettingsHelper.IsDarkMode
            ? SKColors.White
            : SKColor.Parse("#1976D2");
        return new SolidColorPaint(color) { StrokeThickness = 1.5f };
    }

    // دهان أرقام القطاعات في الرسوم الدائرية - لون أبيض ناصع عالي التباين وخط عريض
    private static SolidColorPaint GetPieDataLabelsPaint()
    {
        return new SolidColorPaint(SKColors.White)
        {
            SKTypeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };
    }

    private void InitDrawMarginFrames()
    {
        IsArabic = SettingsHelper.Language == "ar";

        // تحديث لون الفرشاة لـ WPF Overlay
        AxisBrush = SettingsHelper.IsDarkMode 
            ? System.Windows.Media.Brushes.White 
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(25, 118, 210)); // #1976D2

        // توحيد شكل الرسوم البيانية لجميع اللغات لتكون مثل الإنجليزية تماماً (المحور الصادي يساراً دائماً)
        // كما طلب المستخدم في الصورة المرفقة
        AxisOverlayMargin = new Thickness(52, 20, 20, 52); 
        AxisOverlayThickness = new Thickness(1.5, 0, 0, 1.5); // (Left, Bottom)
        YAxisLabelColumn = 0; // التسمية جهة اليسار دائماً
        ChartColumn = 1;      // الرسم جهة اليمين دائماً

        DecayDrawMarginFrame = new DrawMarginFrame
        {
            Fill = new SolidColorPaint(SKColors.Transparent),
            Stroke = null // إلغاء الإطار المربع
        };
        BarDrawMarginFrame = new DrawMarginFrame
        {
            Fill = new SolidColorPaint(SKColors.Transparent),
            Stroke = null // إلغاء الإطار المربع
        };
        IsotopeDrawMarginFrame = new DrawMarginFrame
        {
            Fill = new SolidColorPaint(SKColors.Transparent),
            Stroke = null
        };
        LocationDrawMarginFrame = new DrawMarginFrame
        {
            Fill = new SolidColorPaint(SKColors.Transparent),
            Stroke = null
        };

        // تعبئة Legend الفئات الرقابية (البند 6)
        CategoryLegend = new ObservableCollection<CategoryBadgeInfo>(new[]
        {
            new CategoryBadgeInfo { Category = 1, Color = GetCategoryColor(1), Label = TranslationHelper.GetString("Category1Label") ?? "فئة 1 — خطر عالي جداً" },
            new CategoryBadgeInfo { Category = 2, Color = GetCategoryColor(2), Label = TranslationHelper.GetString("Category2Label") ?? "فئة 2 — خطر عالي" },
            new CategoryBadgeInfo { Category = 3, Color = GetCategoryColor(3), Label = TranslationHelper.GetString("Category3Label") ?? "فئة 3 — خطر متوسط" },
            new CategoryBadgeInfo { Category = 4, Color = GetCategoryColor(4), Label = TranslationHelper.GetString("Category4Label") ?? "فئة 4 — خطر منخفض" },
            new CategoryBadgeInfo { Category = 5, Color = GetCategoryColor(5), Label = TranslationHelper.GetString("Category5Label") ?? "فئة 5 — خطر طفيف" },
        });
    }

    public DashboardViewModel(
        ISourceService sourceService,
        IRadioisotopeService isotopeService,
        ILocationService locationService,
        IDecayCalculationService decayService,
        IBorrowService borrowService,
        ISystemSettingsService settingsService,
        IAlertService? alertService = null,
        IGlobalSearchService? globalSearchService = null)
    {
        _sourceService = sourceService;
        _isotopeService = isotopeService;
        _locationService = locationService;
        _decayService = decayService;
        _borrowService = borrowService;
        _settingsService = settingsService;
        _alertService = alertService ?? (App.ServiceProvider?.GetService(typeof(IAlertService)) as IAlertService);
        _globalSearchService = globalSearchService ?? (App.ServiceProvider?.GetService(typeof(IGlobalSearchService)) as IGlobalSearchService)!;

        InitDrawMarginFrames();
        InitFilterOptions();
        StartClock();
        _ = LoadDataAsync();
    }

    private void InitFilterOptions()
    {
        AvailableStatuses = new ObservableCollection<string>(new[]
        {
            "", "InUse", "Storage", "Waste", "Transfer"
        });
    }

    partial void OnSelectedDecaySourceChanged(Source? value)
    {
        UpdateDecayCurves(null, value);
    }

    // ─── Debounced search ───
    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = new System.Timers.Timer(300);
        _searchDebounceTimer.Elapsed += (_, _) =>
        {
            _searchDebounceTimer?.Stop();
            RunOnUI(ApplyFiltersAndPagination);
        };
        _searchDebounceTimer.AutoReset = false;
        _searchDebounceTimer.Start();
    }

    partial void OnSelectedIsotopeFilterChanged(string value) => RunOnUI(ApplyFiltersAndPagination);
    partial void OnSelectedLocationFilterChanged(string value) => RunOnUI(ApplyFiltersAndPagination);
    partial void OnSelectedStatusFilterChanged(string value) => RunOnUI(ApplyFiltersAndPagination);

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            var sources = await Task.Run(() => _sourceService.GetAllSources());

            // ═══ تعبئة الجدول الرئيسي والبطاقة الأولى فوراً على خيط الواجهة ═══
            RunOnUI(() =>
            {
                TotalSources = sources.Count;
                _allSourceRows = sources.OrderBy(s => s.SourceCode).Select((s, index) => new DashboardSourceRow
                {
                    RowNumber = index + 1,
                    Source = s
                }).ToList();

                // تعبئة قوائم الفلاتر الديناميكية
                var isotopes = sources
                    .SelectMany(s => s.DisplayIsotopesList)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
                isotopes.Insert(0, ""); // "الكل"
                AvailableIsotopes = new ObservableCollection<string>(isotopes);

                var locations = sources
                    .Where(s => s.Location != null)
                    .Select(s => s.Location!.LocationName)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
                locations.Insert(0, ""); // "الكل"
                AvailableLocations = new ObservableCollection<string>(locations);

                CurrentPage = 1;
                ApplyFiltersAndPagination();

                AvailableSources = new ObservableCollection<Source>(
                    sources.Where(s =>
                        (s.Radioisotope != null && s.InitialActivityUnit != null) ||
                        (s.HasDetailedIsotopes && s.SourceIsotopes != null && s.SourceIsotopes.Any(si => si.Radioisotope != null))
                    ).OrderBy(s => s.SourceCode).ToList());
            });

            // ═══ بطاقة 2: إجمالي النشاط بجميع الوحدات (try/catch منفصل) ═══
            try
            {
                UpdateTotalActivityItems(sources);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("DashboardViewModel: UpdateTotalActivityItems failed", ex);
            }

            // ═══ رسم Histogram: نطاقات النشاط (البند 1) ═══
            try
            {
                UpdateActivityHistogram(sources);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("DashboardViewModel: UpdateActivityHistogram failed", ex);
            }

            // ═══ شريطي أفقي: توزيع النظائر Top-10 + Others (البند 2) ═══
            try
            {
                UpdateIsotopeChart(sources);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("DashboardViewModel: UpdateIsotopeChart failed", ex);
            }

            // ═══ شريطي أفقي: توزيع المواقع Top-10 + Others (البند 2) ═══
            try
            {
                UpdateLocationChart(sources);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("DashboardViewModel: UpdateLocationChart failed", ex);
            }

            // ═══ منحنى التحلل: أعلى 5 مصادر + اختيار (try/catch منفصل) ═══
            try
            {
                UpdateDecayCurves(sources, SelectedDecaySource);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("DashboardViewModel: UpdateDecayCurves failed", ex);
            }

            // ═══ الجزء 1: بطاقة تنبيهات انخفاض النشاط (try/catch منفصل) ═══
            try
            {
                UpdateLowActivityAlertCard(sources);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("DashboardViewModel: UpdateLowActivityAlertCard failed", ex);
            }

            // ═══ الجزء 1b: بطاقة تنبيهات اختبارات التسرب (try/catch منفصل) ═══
            try
            {
                UpdateLeakTestAlertCard();
            }
            catch (Exception ex)
            {
                LoggerService.LogError("DashboardViewModel: UpdateLeakTestAlertCard failed", ex);
            }

            // ═══ الجزء 2: بطاقة ملخص الاستعارات (try/catch منفصل) ═══
            try
            {
                UpdateBorrowSummaryCard();
            }
            catch (Exception ex)
            {
                LoggerService.LogError("DashboardViewModel: UpdateBorrowSummaryCard failed", ex);
            }

            // ═══ الجزء 3: جدول مصادر منخفضة النشاط (try/catch منفصل) ═══
            try
            {
                UpdateLowActivityTable(sources);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("DashboardViewModel: UpdateLowActivityTable failed", ex);
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError(TranslationHelper.GetString("MsgErrDashboardLoad") ?? "خطأ في تحميل لوحة التحكم", ex);
        }
    }

    private static void RunOnUI(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    // ───────────── بطاقة إجمالي النشاط بجميع الوحدات ─────────────
    private void UpdateTotalActivityItems(List<Source> sources)
    {
        try
        {
            double totalBq = 0;
            double previousDayBq = 0;

            foreach (var s in sources)
            {
                var unit = s.CurrentActivityUnit;
                double currentBq = unit != null ? s.CurrentActivityValue * unit.ConversionToBq : s.CurrentActivityValue;
                totalBq += currentBq;

                // Simulate previous day calculation using decay formula for 1 day
                if (s.Radioisotope != null)
                {
                    double halfLife = s.Radioisotope.HalfLife;
                    string hlUnit = s.Radioisotope.HalfLifeUnit?.ToLower() ?? "years";
                    double hlDays = hlUnit switch
                    {
                        "seconds" => halfLife / 86400,
                        "minutes" => halfLife / 1440,
                        "hours" => halfLife / 24,
                        "days" => halfLife,
                        "years" => halfLife * 365.25,
                        _ => halfLife * 365.25
                    };
                    double lambda = Math.Log(2) / hlDays;
                    double yesterdayBq = currentBq * Math.Exp(lambda);
                    previousDayBq += yesterdayBq;
                }
                else
                {
                    previousDayBq += currentBq;
                }
            }

            // Set main display value
            RunOnUI(() =>
            {
                TotalActivityDisplay = $"{totalBq:N0} Bq";

                // Calculate percentage change
                if (previousDayBq > 0)
                {
                    double change = ((totalBq - previousDayBq) / previousDayBq) * 100;
                    if (change > 0)
                    {
                        ActivityChangePercent = $"+{change:F2}%";
                        ActivityChangeColor = "#4CAF50";
                        ActivityChangeIcon = "ArrowTopRight";
                    }
                    else
                    {
                        ActivityChangePercent = $"{Math.Abs(change):F2}%";
                        ActivityChangeColor = "#F44336";
                        ActivityChangeIcon = "ArrowBottomRight";
                    }
                }
                else
                {
                    ActivityChangePercent = "0.00%";
                    ActivityChangeColor = "{DynamicResource TextSecondary}";
                    ActivityChangeIcon = "Minus";
                }
            });

            using var db = App.CreateDbContext();
            var activityUnits = db.ActivityUnits.OrderBy(u => u.DisplayOrder).ToList();

            var items = new List<TotalActivityItem>();
            foreach (var unit in activityUnits)
            {
                double converted = totalBq / unit.ConversionToBq;
                items.Add(new TotalActivityItem
                {
                    UnitSymbol = unit.UnitSymbol,
                    Value = converted,
                    DisplayValue = FormatActivityValue(converted, unit.UnitSymbol)
                });
            }

            RunOnUI(() => TotalActivityItems = new ObservableCollection<TotalActivityItem>(items));
        }
        catch (Exception ex)
        {
            LoggerService.LogError("DashboardViewModel: Failed to update total activity items", ex);
        }
    }

    // ───────────── دوال تنسيق النشاط بالصيغة العلمية ─────────────
    public static string FormatScientificBq(double bq)
    {
        if (bq <= 0) return "0 Bq";
        if (bq < 1000) return $"{bq:N0} Bq";
        int exponent = (int)Math.Floor(Math.Log10(bq));
        double mantissa = bq / Math.Pow(10, exponent);
        string expStr = ToSuperscript(exponent);
        return $"{mantissa:F2}×10{expStr} Bq";
    }

    private static string ToSuperscript(int number)
    {
        var str = number.ToString();
        var sb = new System.Text.StringBuilder();
        foreach (var c in str)
        {
            sb.Append(c switch
            {
                '0' => '⁰',
                '1' => '¹',
                '2' => '²',
                '3' => '³',
                '4' => '⁴',
                '5' => '⁵',
                '6' => '⁶',
                '7' => '⁷',
                '8' => '⁸',
                '9' => '⁹',
                '-' => '⁻',
                _ => c
            });
        }
        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // البند 1: Histogram — توزيع المصادر على نطاقات نشاط لوغاريتمية
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// دالة بحتة قابلة للاختبار: تصنيف قائمة نشاطات (Bq) على نطاقات Histogram
    /// </summary>
    public static int[] ComputeHistogramBins(IEnumerable<double> activityBqValues)
    {
        var counts = new int[HistogramBins.Length];
        foreach (var bq in activityBqValues)
        {
            if (bq <= 0) continue;
            for (int i = 0; i < HistogramBins.Length; i++)
            {
                if (bq >= HistogramBins[i].Lower && bq < HistogramBins[i].Upper)
                {
                    counts[i]++;
                    break;
                }
            }
        }
        return counts;
    }

    private void UpdateActivityHistogram(List<Source> sources)
    {
        var activeSources = sources.Where(s => s.CurrentActivityValue > 0).ToList();

        // حساب النشاط بالـ Bq لكل مصدر وتصنيفه
        _histogramBinSources = new Dictionary<int, List<Source>>();
        for (int i = 0; i < HistogramBins.Length; i++)
            _histogramBinSources[i] = new List<Source>();

        foreach (var s in activeSources)
        {
            var unit = s.CurrentActivityUnit;
            double bq = unit != null ? s.CurrentActivityValue * unit.ConversionToBq : s.CurrentActivityValue;
            for (int i = 0; i < HistogramBins.Length; i++)
            {
                if (bq >= HistogramBins[i].Lower && bq < HistogramBins[i].Upper)
                {
                    _histogramBinSources[i].Add(s);
                    break;
                }
            }
        }

        var counts = _histogramBinSources.OrderBy(kv => kv.Key).Select(kv => kv.Value.Count).ToArray();
        var labels = HistogramBins.Select(b => b.Label).ToArray();

        var axisPaint = GetAxisPaint();
        var axisLinePaint = new SolidColorPaint(new SKColor(180, 180, 180, 70)) { StrokeThickness = 1 };
        var dataLabelsPaint = GetAxisPaint();
        var primaryPaint = new SolidColorPaint(SKColor.Parse("#1F5A66"));
        var accentPaint = new SolidColorPaint(SKColor.Parse("#4F7FA3"));

        var columnSeries = new ColumnSeries<int>
        {
            Values = counts,
            Name = string.Empty,
            Fill = primaryPaint,
            Stroke = null,
            MaxBarWidth = 50,
            Padding = 8,
            DataLabelsPaint = dataLabelsPaint,
            DataLabelsSize = 13,
            DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
            DataLabelsFormatter = point => point.Model > 0 ? point.Model.ToString() : "",
            YToolTipLabelFormatter = point =>
            {
                int idx = point.Index;
                if (idx >= 0 && idx < HistogramBins.Length)
                {
                    string range = HistogramBins[idx].Label;
                    return ArabicReshaper.ReshapeAndReverse($"{range}: {point.Model} مصدر");
                }
                return point.Model.ToString();
            }
        };

        columnSeries.PointMeasured += point =>
        {
            if (point.Visual != null)
            {
                // تلوين الأعمدة ذات الأعداد الأعلى بلون أغمق
                point.Visual.Fill = point.Model > 0 ? primaryPaint : accentPaint;
            }
        };

        // التقاط الضغط على العمود مباشرة من الـ Series كطبقة أمان إضافية مع معالج XAML
        columnSeries.ChartPointPointerDown += (chart, point) =>
        {
            if (point == null) return;
            int idx = point.Index;
            RunOnUI(() => OpenHistogramDrillDown(idx));
        };

        int maxCount = counts.Any() ? counts.Max() : 10;

        RunOnUI(() =>
        {
            BarDrawMargin = new LiveChartsCore.Measure.Margin(60, 30, 30, 50);
            ActivityHistogramSeries = new ISeries[] { columnSeries };

            HistogramXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = labels,
                    TextSize = 11,
                    LabelsRotation = 0,
                    LabelsPaint = axisPaint,
                    SeparatorsPaint = null,
                    MinStep = 1,
                    ForceStepToMin = true
                }
            };

            HistogramYAxes = new Axis[]
            {
                new Axis
                {
                    TextSize = 11,
                    LabelsPaint = axisPaint,
                    SeparatorsPaint = axisLinePaint,
                    MinLimit = 0,
                    MinStep = 1,
                    Labeler = v => ((int)v).ToString()
                }
            };
        });
    }

    /// <summary>
    /// فتح Side Panel بتفاصيل المصادر في نطاق Histogram محدد (البند 1 + 4)
    /// </summary>
    [RelayCommand]
    public void OpenHistogramDrillDown(int binIndex)
    {
        if (!_histogramBinSources.ContainsKey(binIndex)) return;
        var sourcesInBin = _histogramBinSources[binIndex];

        var rows = sourcesInBin.Select((s, i) => new DashboardSourceRow
        {
            RowNumber = i + 1,
            Source = s
        }).ToList();

        SidePanelTitle = $"{TranslationHelper.GetString("DrilldownTitle") ?? "المصادر في النطاق"} ({HistogramBins[binIndex].Label} Bq)";
        SidePanelSources = new ObservableCollection<DashboardSourceRow>(rows);
        SidePanelShowSources = true;
        IsSidePanelOpen = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // البند 2: Top-10 + Others — توزيع النظائر والمواقع
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// دالة بحتة قابلة للاختبار: تحويل قائمة (اسم, عدد) إلى Top-10 + Others
    /// </summary>
    public static List<(string Label, int Count)> ComputeTopNPlusOthers(
        IEnumerable<(string Label, int Count)> items, int topN = 10, string othersLabel = "أخرى")
    {
        var sorted = items.OrderByDescending(x => x.Count).ToList();
        var top = sorted.Take(topN).ToList();
        var rest = sorted.Skip(topN).ToList();

        if (rest.Any())
        {
            int othersCount = rest.Sum(x => x.Count);
            top.Add((othersLabel, othersCount));
        }

        return top;
    }

    private void UpdateIsotopeChart(List<Source> sources)
    {
        // جمع جميع النظائر من المصادر (بما في ذلك المصادر متعددة النظائر)
        var isotopeNames = new List<string>();
        foreach (var s in sources)
        {
            if (!string.IsNullOrEmpty(s.DisplayIsotopes))
            {
                var parts = s.DisplayIsotopes.Split(new[] { ',', '+', '/' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var cleaned = part.Replace("\u202A", "").Replace("\u202C", "").Trim();
                    if (!string.IsNullOrWhiteSpace(cleaned))
                        isotopeNames.Add(cleaned);
                }
            }
            else if (s.Radioisotope != null)
            {
                isotopeNames.Add(s.Radioisotope.Symbol);
            }
        }

        var byIsotope = isotopeNames
            .GroupBy(name => name)
            .Select(g => (Label: g.Key, Count: g.Count()))
            .ToList();

        int totalIsotopesCount = byIsotope.Sum(x => x.Count);

        // تخزين البيانات الكاملة لـ "عرض الكل"
        _allIsotopeData = byIsotope
            .OrderByDescending(x => x.Count)
            .Select(x => (x.Label, x.Count, Percent: totalIsotopesCount > 0 ? x.Count * 100.0 / totalIsotopesCount : 0.0))
            .ToList();

        // Top-10 + Others
        string othersLabel = TranslationHelper.GetString("LabelOthers") ?? "أخرى";
        var topPlusOthers = ComputeTopNPlusOthers(byIsotope, 10, othersLabel);

        HasEnoughIsotopeData = topPlusOthers.Count >= 2;

        if (HasEnoughIsotopeData)
        {
            // عكس الترتيب لكي يظهر العنصر الأعلى تكراراً في أعلى المحور الرأسي
            var orderedForChart = topPlusOthers.AsEnumerable().Reverse().ToList();

            var rawLabels = orderedForChart.Select(x => x.Label).ToArray();
            var shapedLabels = rawLabels.Select(l => ArabicReshaper.ReshapeAndReverse(l)).ToArray();
            var values = orderedForChart.Select(x => x.Count).ToArray();

            var axisPaint = GetAxisPaint();
            var axisLinePaint = new SolidColorPaint(new SKColor(180, 180, 180, 70)) { StrokeThickness = 1 };
            var topHighlightPaint = new SolidColorPaint(SKColor.Parse("#1F5A66")); // بترولي داكن لأعلى 3 نظائر
            var normalPaint = new SolidColorPaint(SKColor.Parse("#4F7FA3"));       // أزرق هادئ موحد
            var othersPaint = new SolidColorPaint(SKColor.Parse("#C97A4A"));       // تراكوتا لـ "أخرى"
            var dataLabelsPaint = GetAxisPaint();

            var rowSeries = new RowSeries<int>
            {
                Values = values,
                Name = string.Empty,
                Fill = normalPaint,
                Stroke = null,
                MaxBarWidth = 18,
                Padding = 2,
                DataLabelsPaint = dataLabelsPaint,
                DataLabelsSize = 11,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                DataLabelsFormatter = point => $"{point.Model}",
                XToolTipLabelFormatter = point =>
                {
                    int idx = point.Index;
                    string name = idx >= 0 && idx < rawLabels.Length ? rawLabels[idx] : "";
                    int count = point.Model;
                    double percent = totalIsotopesCount > 0 ? (count * 100.0 / totalIsotopesCount) : 0;
                    return ArabicReshaper.ReshapeAndReverse($"{name}: {count} مصدر ({percent:F1}%)");
                }
            };

            rowSeries.PointMeasured += point =>
            {
                if (point.Visual != null)
                {
                    // التحقق مما إذا كان العنصر هو "أخرى" (أول عنصر في المصفوفة المعكوسة = آخر في الأصلية)
                    int originalIndex = values.Length - 1 - point.Index;
                    bool isOthers = originalIndex == topPlusOthers.Count - 1 && topPlusOthers.Last().Label == othersLabel;
                    bool isTop3 = point.Index >= values.Length - 3 && !isOthers;

                    if (isOthers) point.Visual.Fill = othersPaint;
                    else if (isTop3) point.Visual.Fill = topHighlightPaint;
                    else point.Visual.Fill = normalPaint;
                }
            };

            RunOnUI(() =>
            {
                IsotopeDrawMargin = new LiveChartsCore.Measure.Margin(95, 35, 30, 40);
                SourcesByIsotopeSeries = new ISeries[] { rowSeries };

                // البند 5: IsInverted = true لـ RTL
                IsotopeXAxes = new Axis[]
                {
                    new Axis
                    {
                        TextSize = 10,
                        LabelsPaint = axisPaint,
                        SeparatorsPaint = axisLinePaint,
                        MinStep = 1,
                        IsInverted = true
                    }
                };

                IsotopeYAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = shapedLabels,
                        TextSize = 11,
                        LabelsPaint = axisPaint,
                        SeparatorsPaint = null,
                        MinStep = 1,
                        ForceStepToMin = true
                    }
                };
            });
        }
        else
        {
            RunOnUI(() => SourcesByIsotopeSeries = Array.Empty<ISeries>());
        }
    }

    private void UpdateLocationChart(List<Source> sources)
    {
        int totalValidSources = sources.Count(s => s.Location != null);
        var locationGroups = sources
            .Where(s => s.Location != null)
            .GroupBy(s => s.Location!.LocationName)
            .Select(g => (Label: g.Key, Count: g.Count()))
            .ToList();

        // تخزين البيانات الكاملة لـ "عرض الكل"
        _allLocationData = locationGroups
            .OrderByDescending(x => x.Count)
            .Select(x => (x.Label, x.Count, Percent: totalValidSources > 0 ? x.Count * 100.0 / totalValidSources : 0.0))
            .ToList();

        // Top-10 + Others
        string othersLabel = TranslationHelper.GetString("LabelOthers") ?? "أخرى";
        var topPlusOthers = ComputeTopNPlusOthers(locationGroups, 10, othersLabel);

        HasEnoughLocationData = topPlusOthers.Count >= 2;

        if (HasEnoughLocationData)
        {
            // عكس الترتيب لكي يظهر الموقع الأكبر في الأعلى
            var orderedForChart = topPlusOthers.AsEnumerable().Reverse().ToList();

            var rawLabels = orderedForChart.Select(x => x.Label).ToArray();
            var axisLabels = rawLabels.Select(l =>
            {
                // عرض اسم الموقع كاملاً بدون اقتطاع قسري عند 20 حرفاً
                string display = l;
                if (display.Length > 40)
                {
                    display = display.Substring(0, 38) + "...";
                }
                return ArabicReshaper.ReshapeAndReverse(display);
            }).ToArray();

            var values = orderedForChart.Select(x => x.Count).ToArray();

            // حساب الهامش الأيسر ديناميكياً لضمان عدم اقتطاع أسماء المواقع العربية الطويلة
            int maxLen = rawLabels.Any() ? rawLabels.Max(l => l.Length) : 10;
            int dynamicLeftMargin = Math.Min(300, Math.Max(180, (int)(maxLen * 7.5 + 40)));

            var axisPaint = GetAxisPaint();
            var axisLinePaint = new SolidColorPaint(new SKColor(180, 180, 180, 70)) { StrokeThickness = 1 };
            var topHighlightPaint = new SolidColorPaint(SKColor.Parse("#1F5A66")); // بترولي داكن لأعلى 3 مواقع
            var normalPaint = new SolidColorPaint(SKColor.Parse("#4F7FA3"));       // أزرق هادئ موحد
            var othersPaint = new SolidColorPaint(SKColor.Parse("#C97A4A"));       // تراكوتا لـ "أخرى"
            var dataLabelsPaint = GetAxisPaint();

            var rowSeries = new RowSeries<int>
            {
                Values = values,
                Name = string.Empty,
                Fill = normalPaint,
                Stroke = null,
                MaxBarWidth = 18,
                Padding = 2,
                DataLabelsPaint = dataLabelsPaint,
                DataLabelsSize = 11,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                DataLabelsFormatter = point =>
                {
                    int count = point.Model;
                    double percent = totalValidSources > 0 ? (count * 100.0 / totalValidSources) : 0;
                    return $"{count} ({percent:F1}%)";
                },
                XToolTipLabelFormatter = point =>
                {
                    int count = point.Model;
                    double percent = totalValidSources > 0 ? (count * 100.0 / totalValidSources) : 0;
                    int idx = point.Index;
                    string rawName = idx >= 0 && idx < rawLabels.Length ? rawLabels[idx] : "";
                    return ArabicReshaper.ReshapeAndReverse($"{rawName}: {count} مصدر ({percent:F1}%)");
                }
            };

            rowSeries.PointMeasured += point =>
            {
                if (point.Visual != null)
                {
                    int originalIndex = values.Length - 1 - point.Index;
                    bool isOthers = originalIndex == topPlusOthers.Count - 1 && topPlusOthers.Last().Label == othersLabel;
                    bool isTop3 = point.Index >= values.Length - 3 && !isOthers;

                    if (isOthers) point.Visual.Fill = othersPaint;
                    else if (isTop3) point.Visual.Fill = topHighlightPaint;
                    else point.Visual.Fill = normalPaint;
                }
            };

            RunOnUI(() =>
            {
                LocationDrawMargin = new LiveChartsCore.Measure.Margin(dynamicLeftMargin, 35, 30, 40);
                SourcesByLocationSeries = new ISeries[] { rowSeries };

                // البند 5: IsInverted = true لـ RTL
                LocationXAxes = new Axis[]
                {
                    new Axis
                    {
                        TextSize = 10,
                        LabelsPaint = axisPaint,
                        SeparatorsPaint = axisLinePaint,
                        MinStep = 1,
                        IsInverted = true
                    }
                };

                LocationYAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = axisLabels,
                        TextSize = 11,
                        LabelsPaint = axisPaint,
                        SeparatorsPaint = null,
                        MinStep = 1,
                        ForceStepToMin = true
                    }
                };
            });
        }
        else
        {
            RunOnUI(() => SourcesByLocationSeries = Array.Empty<ISeries>());
        }
    }

    /// <summary>فتح Side Panel بجميع النظائر</summary>
    [RelayCommand]
    public void ShowAllIsotopes()
    {
        SidePanelTitle = TranslationHelper.GetString("AllIsotopesTitle") ?? "جميع النظائر المشعة";
        SidePanelDistribution = new ObservableCollection<DistributionRow>(
            _allIsotopeData.Select(x => new DistributionRow
            {
                Label = x.Label,
                Count = x.Count,
                Percent = $"{x.Percent:F1}%"
            }));
        SidePanelShowSources = false;
        IsSidePanelOpen = true;
    }

    /// <summary>فتح Side Panel بجميع المواقع</summary>
    [RelayCommand]
    public void ShowAllLocations()
    {
        SidePanelTitle = TranslationHelper.GetString("AllLocationsTitle") ?? "جميع مواقع التخزين";
        SidePanelDistribution = new ObservableCollection<DistributionRow>(
            _allLocationData.Select(x => new DistributionRow
            {
                Label = x.Label,
                Count = x.Count,
                Percent = $"{x.Percent:F1}%"
            }));
        SidePanelShowSources = false;
        IsSidePanelOpen = true;
    }

    /// <summary>إغلاق Side Panel</summary>
    [RelayCommand]
    public void CloseSidePanel()
    {
        IsSidePanelOpen = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // البند 3: بحث + فلاتر + Pagination
    // ═══════════════════════════════════════════════════════════════════════

    public void ApplyFiltersAndPagination()
    {
        var filtered = _allSourceRows.AsEnumerable();

        // بحث نصي حر (SourceCode + SerialNumber)
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim().ToLower();
            filtered = filtered.Where(r =>
                r.SourceCode.ToLower().Contains(term) ||
                (r.Source.SerialNumber?.ToLower().Contains(term) == true));
        }

        // فلتر النظير
        if (!string.IsNullOrEmpty(SelectedIsotopeFilter))
        {
            filtered = filtered.Where(r =>
                r.Source.DisplayIsotopesList.Any(iso =>
                    iso.Equals(SelectedIsotopeFilter, StringComparison.OrdinalIgnoreCase)));
        }

        // فلتر الموقع
        if (!string.IsNullOrEmpty(SelectedLocationFilter))
        {
            filtered = filtered.Where(r =>
                r.Source.Location?.LocationName == SelectedLocationFilter);
        }

        // فلتر الحالة
        if (!string.IsNullOrEmpty(SelectedStatusFilter))
        {
            filtered = filtered.Where(r =>
                r.Source.Status == SelectedStatusFilter);
        }

        var filteredList = filtered.ToList();
        TotalFilteredCount = filteredList.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling((double)filteredList.Count / PageSize));

        if (CurrentPage > TotalPages)
            CurrentPage = TotalPages;
        if (CurrentPage < 1)
            CurrentPage = 1;

        // تطبيق الترقيم المستمر عبر الصفحات
        int skip = (CurrentPage - 1) * PageSize;
        var pageItems = filteredList.Skip(skip).Take(PageSize).ToList();

        // إعادة ترقيم الصفوف بشكل مستمر
        for (int i = 0; i < pageItems.Count; i++)
        {
            pageItems[i].RowNumber = skip + i + 1;
        }

        DashboardSources = new ObservableCollection<DashboardSourceRow>(pageItems);
        PageInfo = $"{CurrentPage} / {TotalPages}";
        FirstPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        LastPageCommand.NotifyCanExecuteChanged();
    }

    private bool CanGoToPreviousPage => CurrentPage > 1;
    private bool CanGoToNextPage => CurrentPage < TotalPages;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    public void FirstPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage = 1;
            ApplyFiltersAndPagination();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    public void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            ApplyFiltersAndPagination();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    public void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            ApplyFiltersAndPagination();
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    public void LastPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage = TotalPages;
            ApplyFiltersAndPagination();
        }
    }

    [RelayCommand]
    public void ResetFilters()
    {
        SearchText = string.Empty;
        SelectedIsotopeFilter = string.Empty;
        SelectedLocationFilter = string.Empty;
        SelectedStatusFilter = string.Empty;
        CurrentPage = 1;
        ApplyFiltersAndPagination();
    }

    [RelayCommand]
    public void ChangePageSize(string newSizeStr)
    {
        if (int.TryParse(newSizeStr, out int newSize) && newSize > 0)
        {
            PageSize = newSize;
            CurrentPage = 1;
            ApplyFiltersAndPagination();
        }
    }

    // ─── إدارة الساعة المباشرة وتنظيف الموارد ───
    public void StartClock()
    {
        UpdateClock();
        if (_clockTimer == null)
        {
            _clockTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();
        }
    }

    public void UpdateClock()
    {
        var now = DateTime.Now;
        var culture = System.Threading.Thread.CurrentThread.CurrentUICulture;
        bool isArabic = culture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase);

        if (isArabic)
        {
            CurrentDateDisplay = now.ToString("dddd، d MMMM yyyy", culture);
            CurrentTimeDisplay = now.ToString("hh:mm:ss tt", culture)
                                    .Replace("AM", "ص")
                                    .Replace("PM", "م");
        }
        else
        {
            CurrentDateDisplay = now.ToString("dddd, MMMM d, yyyy", culture);
            CurrentTimeDisplay = now.ToString("hh:mm:ss tt", culture);
        }
    }

    public void Dispose()
    {
        _clockTimer?.Stop();
        _clockTimer = null;
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = null;
        _globalSearchCts?.Cancel();
        _globalSearchCts?.Dispose();
        _globalSearchCts = null;
    }

    // ───────────── منحنى التحلل الزمني — وضع المقارنة (أ) ووضع المصدر المفرد (ب) ─────────────
    private void UpdateDecayCurves(List<Source>? allSources, Source? selectedSource)
    {
        var axisPaint = GetAxisPaint();
        var axisLinePaint = GetAxisLinePaint();
        var seriesList = new List<ISeries>();

        // Labeler helper to convert log10 values back to readable format for Top 5 comparison
        Func<double, string> logLabeler = value =>
        {
            double real = Math.Pow(10, value);
            if (real < 1) return "0";
            if (real < 1_000) return real.ToString("N0");
            if (real < 1_000_000) return (real / 1_000).ToString("N1") + "K";
            if (real < 1_000_000_000) return (real / 1_000_000).ToString("N1") + "M";
            return (real / 1_000_000_000).ToString("N1") + "G";
        };

        try
        {
            var sources = allSources ?? _sourceService.GetAllSources();

            if (selectedSource != null)
            {
                // ═══════════════════════════════════════════════════════════
                // الوضع (ب) — اختيار مصدر محدد: نسخة طبق الأصل من الحاسبة
                // ═══════════════════════════════════════════════════════════
                var targetSource = sources.FirstOrDefault(s => s.Id == selectedSource.Id || s.SourceCode == selectedSource.SourceCode) ?? selectedSource;

                var rawCurve = _decayService.GetSourceCompositeDecayCurve(targetSource, 60);

                if (rawCurve.Count > 0)
                {
                    // اختيار الوحدة الأنسب تلقائياً بناءً على النشاط الأقصى في المنحنى
                    double maxBq = rawCurve.Max(pt => pt.ActivityBq);
                    string chosenUnit;
                    if (maxBq >= 1e12) chosenUnit = "TBq";
                    else if (maxBq >= 1e9) chosenUnit = "GBq";
                    else if (maxBq >= 1e6) chosenUnit = "MBq";
                    else if (maxBq >= 1e3) chosenUnit = "kBq";
                    else chosenUnit = "Bq";

                    var curvePoints = rawCurve.Select(pt => new DateTimePoint(
                        pt.Time, _decayService.ConvertFromBq(pt.ActivityBq, chosenUnit)
                    )).ToList();

                    // حساب النشاط الحالي بالـ Bq ثم تحويله للوحدة المختارة
                    double curUnitConv = targetSource.CurrentActivityUnit?.ConversionToBq ?? 1;
                    double currentBq = targetSource.CurrentActivityValue * curUnitConv;
                    double currentConverted = _decayService.ConvertFromBq(currentBq, chosenUnit);

                    var currentPoint = new List<DateTimePoint> { new DateTimePoint(DateTime.Now, currentConverted) };

                    var strokeColor = SKColor.Parse("#1F5A66"); // Petroleum Blue
                    var fillColor = SKColor.Parse("#1A1F5A66"); // 10% Alpha Fill
                    var pointColor = SKColor.Parse("#C25B4A");  // Danger/Terracotta Red

                    seriesList.Add(new LineSeries<DateTimePoint>
                    {
                        Name = "Decay Curve",
                        Values = curvePoints,
                        Stroke = new SolidColorPaint(strokeColor) { StrokeThickness = 3 },
                        Fill = new SolidColorPaint(fillColor),
                        GeometrySize = 0,
                        LineSmoothness = 0.65,
                        XToolTipLabelFormatter = point => point.Model?.DateTime.ToString("yyyy/MM/dd") ?? "",
                        YToolTipLabelFormatter = point => $"{(point.Model?.Value ?? 0):N2} {chosenUnit}"
                    });

                    seriesList.Add(new ScatterSeries<DateTimePoint>
                    {
                        Name = "Calculated Point",
                        Values = currentPoint,
                        Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                        Fill = new SolidColorPaint(pointColor),
                        GeometrySize = 14,
                        XToolTipLabelFormatter = point => point.Model?.DateTime.ToString("yyyy/MM/dd") ?? "",
                        YToolTipLabelFormatter = point => $"{(point.Model?.Value ?? 0):N2} {chosenUnit}"
                    });



                    DecayXAxes = new Axis[]
                    {
                        new DateTimeAxis(TimeSpan.FromDays(1), date => date.ToString("yyyy/MM/dd"))
                        {
                            TextSize = 11,
                            LabelsPaint = axisPaint,
                            SeparatorsPaint = axisLinePaint
                        }
                    };

                    DecayYAxes = new Axis[]
                    {
                        new Axis
                        {
                            TextSize = 11,
                            Labeler = val => $"{val:N2} {chosenUnit}",
                            LabelsPaint = axisPaint,
                            SeparatorsPaint = axisLinePaint,
                            Position = LiveChartsCore.Measure.AxisPosition.Start
                        }
                    };

                    DecayLegendItems = new ObservableCollection<LegendItem>
                    {
                        new LegendItem { Label = TranslationHelper.GetString("CalcChartLegendCurve") ?? "منحنى الاضمحلال", Color = "#1F5A66" },
                        new LegendItem { Label = TranslationHelper.GetString("CalcChartLegendCurrent") ?? "النقطة المحسوبة", Color = "#C25B4A" }
                    };
                }
            }
            else
            {
                // ═══════════════════════════════════════════════════════════
                // الوضع (أ) — لا يوجد اختيار: مقارنة "أعلى 5 مصادر"
                // ═══════════════════════════════════════════════════════════
                var sourcesToRender = sources
                    .Where(s => (s.Radioisotope != null && s.InitialActivityUnit != null) ||
                                (s.HasDetailedIsotopes && s.SourceIsotopes != null && s.SourceIsotopes.Any(si => si.Radioisotope != null)))
                    .OrderByDescending(s =>
                    {
                        var unit = s.CurrentActivityUnit;
                        return unit != null ? s.CurrentActivityValue * unit.ConversionToBq : s.CurrentActivityValue;
                    })
                    .Take(5)
                    .ToList();

                if (sourcesToRender.Any())
                {
                    // توحيد النطاق الزمني لجميع النطاقات المعروضة
                    DateTime startDate = sourcesToRender.Min(s =>
                    {
                        if (s.HasDetailedIsotopes && s.SourceIsotopes != null && s.SourceIsotopes.Any(si => si.Radioisotope != null))
                            return s.SourceIsotopes.Where(si => si.Radioisotope != null).Min(si => si.CalibrationDate ?? (s.CalibrationDate != default ? s.CalibrationDate : DateTime.Today));
                        return s.CalibrationDate != default ? s.CalibrationDate : DateTime.Today;
                    });
                    if (startDate == default) startDate = DateTime.Today;

                    // البحث عن أطول نصف عمر لاحتساب نهاية المنحنى (5 أنصاف أعمار في المستقبل)
                    double maxHalfLifeSeconds = 0;
                    foreach (var s in sourcesToRender)
                    {
                        if (s.HasDetailedIsotopes && s.SourceIsotopes != null && s.SourceIsotopes.Any(si => si.Radioisotope != null))
                        {
                            foreach (var si in s.SourceIsotopes.Where(si => si.Radioisotope != null))
                            {
                                double sec = AlertService.ConvertToSeconds(si.Radioisotope!.HalfLife, si.Radioisotope.HalfLifeUnit);
                                if (sec > maxHalfLifeSeconds) maxHalfLifeSeconds = sec;
                            }
                        }
                        else if (s.Radioisotope != null)
                        {
                            double sec = AlertService.ConvertToSeconds(s.Radioisotope.HalfLife, s.Radioisotope.HalfLifeUnit);
                            if (sec > maxHalfLifeSeconds) maxHalfLifeSeconds = sec;
                        }
                    }
                    if (maxHalfLifeSeconds <= 0) maxHalfLifeSeconds = 86400;

                    DateTime endDate;
                    try
                    {
                        double secondsToAdd = maxHalfLifeSeconds * 5;
                        double maxSecondsAllowed = (DateTime.MaxValue - DateTime.Now).TotalSeconds;
                        if (secondsToAdd > maxSecondsAllowed) secondsToAdd = maxSecondsAllowed - 86400;
                        endDate = DateTime.Now.AddSeconds(secondsToAdd);

                        if ((endDate - startDate).TotalSeconds < secondsToAdd)
                        {
                            double maxStartSecondsAllowed = (DateTime.MaxValue - startDate).TotalSeconds;
                            if (secondsToAdd > maxStartSecondsAllowed) secondsToAdd = maxStartSecondsAllowed - 86400;
                            endDate = startDate.AddSeconds(secondsToAdd);
                        }
                    }
                    catch
                    {
                        endDate = DateTime.MaxValue.AddDays(-1);
                    }

                    for (int i = 0; i < sourcesToRender.Count; i++)
                    {
                        try
                        {
                            var source = sourcesToRender[i];
                            List<(DateTime Time, double Activity)> curve;

                            if (source.HasDetailedIsotopes && source.SourceIsotopes != null && source.SourceIsotopes.Any(si => si.Radioisotope != null))
                            {
                                curve = new List<(DateTime Time, double Activity)>();
                                double totalSec = (endDate - startDate).TotalSeconds;
                                if (totalSec <= 0) totalSec = 1;
                                double interval = totalSec / 50;

                                for (int step = 0; step <= 50; step++)
                                {
                                    var t = startDate.AddSeconds(step * interval);
                                    double totalAct = 0;
                                    foreach (var si in source.SourceIsotopes.Where(si => si.Radioisotope != null))
                                    {
                                        var calib = si.CalibrationDate ?? (source.CalibrationDate != default ? source.CalibrationDate : startDate);
                                        double unitConv = si.ActivityUnit?.ConversionToBq ?? source.InitialActivityUnit?.ConversionToBq ?? 1;
                                        double initBq = (si.InitialActivityValue ?? 0) * unitConv;
                                        double hlSec = AlertService.ConvertToSeconds(si.Radioisotope!.HalfLife, si.Radioisotope.HalfLifeUnit);
                                        double el = (t - calib).TotalSeconds;
                                        if (el <= 0) totalAct += initBq;
                                        else totalAct += initBq * Math.Pow(0.5, el / hlSec);
                                    }
                                    curve.Add((t, totalAct));
                                }
                            }
                            else
                            {
                                var initialBq = source.InitialActivityValue * (source.InitialActivityUnit?.ConversionToBq ?? 1);
                                curve = _decayService.GenerateUnifiedDecayCurve(
                                    initialBq, source.Radioisotope!.HalfLife, source.Radioisotope.HalfLifeUnit,
                                    source.CalibrationDate, startDate, endDate, 50);
                            }

                            var points = curve.Select(c => new DateTimePoint(c.Time, c.Activity > 0 ? Math.Log10(c.Activity) : 0)).ToList();
                            var strokeColor = SKColor.Parse(DecayStrokeColors[i % DecayStrokeColors.Length]);
                            var fillColor = SKColor.Parse(DecayFillColors[i % DecayFillColors.Length]);

                            seriesList.Add(new LineSeries<DateTimePoint>
                            {
                                Values = points,
                                Name = source.SourceCode,
                                Stroke = new SolidColorPaint(strokeColor) { StrokeThickness = 3 },
                                Fill = new SolidColorPaint(fillColor),
                                GeometrySize = 0,
                                LineSmoothness = 0.65
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"[DecayCurve] Error for source {sourcesToRender[i].SourceCode}: {ex.Message}");
                        }
                    }

                    DecayXAxes = new Axis[]
                    {
                        new DateTimeAxis(TimeSpan.FromDays(1), date => date.ToString("yyyy/MM/dd"))
                        {
                            TextSize = 11,
                            LabelsPaint = axisPaint,
                            SeparatorsPaint = axisLinePaint
                        }
                    };

                    DecayYAxes = new Axis[]
                    {
                        new Axis
                        {
                            TextSize = 11,
                            Labeler = logLabeler,
                            LabelsPaint = axisPaint,
                            SeparatorsPaint = axisLinePaint,
                            Position = LiveChartsCore.Measure.AxisPosition.Start,
                            MinStep = 1
                        }
                    };

                    var decayLegend = new ObservableCollection<LegendItem>();
                    for (int i = 0; i < seriesList.Count; i++)
                    {
                        decayLegend.Add(new LegendItem
                        {
                            Label = seriesList[i].Name ?? "",
                            Color = DecayStrokeColors[i % DecayStrokeColors.Length]
                        });
                    }
                    DecayLegendItems = decayLegend;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DecayCurve] General error: {ex.Message}\n{ex.StackTrace}");
        }

        // Always set the chart properties — even if data generation failed
        ActivityDecaySeries = seriesList.ToArray();
    }


    // ───────────── أدوات التنسيق ─────────────
    private static string FormatActivityValue(double value, string unitSymbol)
    {
        if (value == 0) return $"0 {unitSymbol}";
        if (Math.Abs(value) >= 1e9) return $"{value:E3} {unitSymbol}";
        if (Math.Abs(value) >= 1e6) return $"{value:N0} {unitSymbol}";
        if (Math.Abs(value) >= 1000) return $"{value:N2} {unitSymbol}";
        if (Math.Abs(value) >= 1) return $"{value:N4} {unitSymbol}";
        return $"{value:E3} {unitSymbol}";
    }

    // ───────────── الجزء 1: بطاقة تنبيهات انخفاض النشاط ─────────────
    private void UpdateLowActivityAlertCard(List<Source> sources)
    {
        int criticalCount = 0;
        int warningCount = 0;

        foreach (var source in sources.Where(s =>
            s.Status == "InUse" || s.Status == "Storage"))
        {
            var (maxHalfLives, _) = AlertService.CalculateMaxHalfLivesElapsed(source);
            if (maxHalfLives >= 6.0) criticalCount++;
            else if (maxHalfLives >= 5.0) warningCount++;
        }

        LowActivityCriticalCount = criticalCount;
        LowActivityWarningCount = warningCount;
        HasLowActivityAlerts = criticalCount > 0 || warningCount > 0;
    }

    // ───────────── الجزء 1b: بطاقة تنبيهات اختبارات التسرب ─────────────
    private void UpdateLeakTestAlertCard()
    {
        try
        {
            if (_alertService == null) return;
            var activeAlerts = _alertService.GetAllAlerts(includeDismissed: false);

            int warning = activeAlerts.Count(a => a.AlertType == "LeakTestDue" && a.Severity == "Warning");
            int critical = activeAlerts.Count(a => a.AlertType == "LeakTestOverdue" && a.Severity == "Critical");

            LeakTestWarningCount = warning;
            LeakTestCriticalCount = critical;
            HasLeakTestAlerts = warning > 0 || critical > 0;
        }
        catch (Exception ex)
        {
            LoggerService.LogError("DashboardViewModel: Failed to update leak test alerts", ex);
        }
    }

    // ───────────── الجزء 2: بطاقة ملخص الاستعارات ─────────────
    private void UpdateBorrowSummaryCard()
    {
        try
        {
            _borrowService.CheckAndUpdateOverdue();
            var allRequests = _borrowService.GetAll();

            int overdue = allRequests.Count(r => r.Status == "Overdue");
            int active  = allRequests.Count(r => r.Status == "Delivered" || r.Status == "Overdue");
            int dueSoon = _borrowService.GetDueSoonCount(allRequests);

            BorrowSummary = new DashboardBorrowSummary
            {
                OverdueCount = overdue,
                DueSoonCount  = dueSoon,
                ActiveCount   = active
            };
        }
        catch
        {
            BorrowSummary = new DashboardBorrowSummary();
        }
    }

    // ───────────── الجزء 3: جدول مصادر منخفضة النشاط ─────────────
    private void UpdateLowActivityTable(List<Source> sources)
    {
        var rows = new List<LowActivitySourceRow>();

        foreach (var source in sources.Where(s =>
            s.Status == "InUse" || s.Status == "Storage"))
        {
            var (maxHalfLives, worstIsotope) = AlertService.CalculateMaxHalfLivesElapsed(source);
            if (maxHalfLives < 5.0) continue;

            string symbol = !string.IsNullOrEmpty(worstIsotope) 
                ? worstIsotope 
                : (source.DisplayIsotopes ?? source.Radioisotope?.Symbol ?? "-");
            bool isCritical = maxHalfLives >= 6.0;

            rows.Add(new LowActivitySourceRow
            {
                SourceCode       = source.SourceCode,
                IsotopeSymbol    = symbol,
                HalfLivesElapsed = maxHalfLives,
                HalfLivesDisplay = $"{maxHalfLives:F1} T½",
                Severity         = isCritical ? "Critical" : "Warning",
                SeverityColor    = isCritical ? "#C25B4A" : "#E0A93E",
                SeverityBadgeBackground = isCritical ? "#1AC25B4A" : "#1AE0A93E",
                SeverityLabel    = isCritical
                    ? (IsArabic ? "حرج" : "Critical")
                    : (IsArabic ? "تحذير" : "Warning")
            });
        }

        // ترتيب تنازلي حسب الأشد خطورة
        rows = rows.OrderByDescending(r => r.HalfLivesElapsed).ToList();
        TotalLowActivityCount = rows.Count;
        HasMoreLowActivitySources = rows.Count > 5;

        LowActivitySources = new ObservableCollection<LowActivitySourceRow>(rows.Take(5));
    }

    // ───────────── أوامر التنقل من لوحة القيادة ─────────────
    [RelayCommand]
    private void NavigateToLowActivityReport()
    {
        // الحصول على MainViewModel وتحديد نافذة التقارير وفتح تقرير المصادر المنخفضة
        if (App.ServiceProvider?.GetService(typeof(MainViewModel)) is MainViewModel main)
        {
            main.NavigateTo("Reports");
            if (main.CurrentView is ReportsViewModel reportsVm)
            {
                reportsVm.SelectReportCommand.Execute("LowActivityReport");
            }
        }
    }

    [RelayCommand]
    private void NavigateToLeakTests()
    {
        if (App.ServiceProvider?.GetService(typeof(MainViewModel)) is MainViewModel main)
        {
            main.NavigateTo("LeakTests");
        }
    }

    [RelayCommand]
    private void NavigateToBorrowing()
    {
        if (App.ServiceProvider.GetService(typeof(MainViewModel)) is MainViewModel main)
        {
            main.NavigateTo("Borrowing");
        }
    }

    // ───────────── أوامر الاختصارات السريعة ─────────────
    [RelayCommand]
    private void QuickAddSource()
    {
        if (App.ServiceProvider.GetService(typeof(MainViewModel)) is MainViewModel main)
        {
            main.NavigateTo("Sources");
            if (main.CurrentView is SourcesViewModel sourcesVm)
            {
                sourcesVm.AddNewCommand.Execute(null);
            }
        }
    }

    [RelayCommand]
    private void QuickBorrowSource()
    {
        if (App.ServiceProvider.GetService(typeof(MainViewModel)) is MainViewModel main)
        {
            main.NavigateTo("Borrowing");
            if (main.CurrentView is BorrowViewModel borrowVm)
            {
                borrowVm.OpenCreateDialogCommand.Execute(null);
            }
        }
    }

    // ───────────── عرض تفاصيل المصدر (نافذة منبثقة) ─────────────
    [RelayCommand]
    private void ViewSourceDetails(object? parameter)
    {
        SourceNavigationHelper.OpenSourceDetails(parameter);
    }

    // ─── منطق البحث الموحّد (Global Search Logic - Phase B) ───
    partial void OnGlobalSearchQueryChanged(string value)
    {
        _globalSearchCts?.Cancel();

        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 2)
        {
            GlobalSearchResultGroups.Clear();
            TotalGlobalSearchResultsCount = 0;
            IsGlobalSearchResultsOpen = false;
            IsGlobalSearching = false;
            SelectedGlobalSearchResultItem = null;
            return;
        }

        _globalSearchCts = new System.Threading.CancellationTokenSource();
        var token = _globalSearchCts.Token;
        var query = value.Trim();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                if (token.IsCancellationRequested) return;

                RunOnUI(() => IsGlobalSearching = true);

                var groups = await _globalSearchService.SearchAsync(query, token);
                if (token.IsCancellationRequested) return;

                RunOnUI(() =>
                {
                    GlobalSearchResultGroups = new ObservableCollection<GlobalSearchResultGroup>(groups);
                    TotalGlobalSearchResultsCount = groups.Sum(g => g.Items.Count);
                    IsGlobalSearchResultsOpen = groups.Count > 0;
                    SelectedGlobalSearchResultItem = groups.FirstOrDefault()?.Items.FirstOrDefault();
                    UpdateSelectionFlags();
                    IsGlobalSearching = false;
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LoggerService.LogError("DashboardViewModel: GlobalSearch error", ex);
                RunOnUI(() => IsGlobalSearching = false);
            }
        }, token);
    }

    partial void OnSelectedGlobalSearchResultItemChanged(GlobalSearchResultItem? value)
    {
        UpdateSelectionFlags();
    }

    private void UpdateSelectionFlags()
    {
        foreach (var group in GlobalSearchResultGroups)
        {
            foreach (var itm in group.Items)
            {
                itm.IsSelected = (itm == SelectedGlobalSearchResultItem);
            }
        }
    }

    [RelayCommand]
    public async Task ExecuteGlobalSearchNowAsync()
    {
        _globalSearchCts?.Cancel();
        var query = GlobalSearchQuery?.Trim() ?? string.Empty;

        if (query.Length < 2)
        {
            GlobalSearchResultGroups.Clear();
            TotalGlobalSearchResultsCount = 0;
            IsGlobalSearchResultsOpen = false;
            IsGlobalSearching = false;
            SelectedGlobalSearchResultItem = null;
            return;
        }

        try
        {
            IsGlobalSearching = true;
            var groups = await _globalSearchService.SearchAsync(query);
            GlobalSearchResultGroups = new ObservableCollection<GlobalSearchResultGroup>(groups);
            TotalGlobalSearchResultsCount = groups.Sum(g => g.Items.Count);
            IsGlobalSearchResultsOpen = groups.Count > 0;
            SelectedGlobalSearchResultItem = groups.FirstOrDefault()?.Items.FirstOrDefault();
            UpdateSelectionFlags();
        }
        catch (Exception ex)
        {
            LoggerService.LogError("DashboardViewModel: ExecuteGlobalSearchNowAsync failed", ex);
        }
        finally
        {
            IsGlobalSearching = false;
        }
    }

    [RelayCommand]
    public async Task ConfirmGlobalSearchResultAsync()
    {
        if (IsGlobalSearchResultsOpen && SelectedGlobalSearchResultItem != null)
        {
            SelectGlobalSearchResult(SelectedGlobalSearchResultItem);
        }
        else
        {
            await ExecuteGlobalSearchNowAsync();
        }
    }

    [RelayCommand]
    public void SelectNextSearchResult()
    {
        if (!IsGlobalSearchResultsOpen || GlobalSearchResultGroups.Count == 0) return;
        var allItems = GlobalSearchResultGroups.SelectMany(g => g.Items).ToList();
        if (allItems.Count == 0) return;

        int currentIndex = SelectedGlobalSearchResultItem != null ? allItems.IndexOf(SelectedGlobalSearchResultItem) : -1;
        int nextIndex = (currentIndex + 1) % allItems.Count;
        SelectedGlobalSearchResultItem = allItems[nextIndex];
    }

    [RelayCommand]
    public void SelectPreviousSearchResult()
    {
        if (!IsGlobalSearchResultsOpen || GlobalSearchResultGroups.Count == 0) return;
        var allItems = GlobalSearchResultGroups.SelectMany(g => g.Items).ToList();
        if (allItems.Count == 0) return;

        int currentIndex = SelectedGlobalSearchResultItem != null ? allItems.IndexOf(SelectedGlobalSearchResultItem) : 0;
        int prevIndex = (currentIndex - 1 + allItems.Count) % allItems.Count;
        SelectedGlobalSearchResultItem = allItems[prevIndex];
    }

    [RelayCommand]
    public void ClearGlobalSearch()
    {
        _globalSearchCts?.Cancel();
        GlobalSearchQuery = string.Empty;
        GlobalSearchResultGroups.Clear();
        TotalGlobalSearchResultsCount = 0;
        IsGlobalSearchResultsOpen = false;
        IsGlobalSearching = false;
        SelectedGlobalSearchResultItem = null;
    }

    [RelayCommand]
    public void CloseGlobalSearchResults()
    {
        IsGlobalSearchResultsOpen = false;
    }

    [RelayCommand]
    public void SelectGlobalSearchResult(GlobalSearchResultItem? item)
    {
        if (item == null) return;

        // 1. إغلاق القائمة وتفريغ البحث
        IsGlobalSearchResultsOpen = false;
        GlobalSearchQuery = string.Empty;
        GlobalSearchResultGroups.Clear();
        TotalGlobalSearchResultsCount = 0;
        SelectedGlobalSearchResultItem = null;

        // 2. الانتقال إلى الشاشة المستهدفة عبر MainViewModel
        if (App.ServiceProvider?.GetService(typeof(MainViewModel)) is MainViewModel main)
        {
            main.NavigateTo(item.TargetView);
        }

        // 3. إرسال رسالة لتحديد/فتح تفاصيل العنصر فوراً داخل الشاشة المستهدفة
        WeakReferenceMessenger.Default.Send(new NavigateToSearchResultMessage(item.Category, item.Id));
    }
}
