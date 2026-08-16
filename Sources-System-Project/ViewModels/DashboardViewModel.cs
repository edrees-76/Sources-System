using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Models;
using Sources.Services;
using Sources.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
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

public partial class DashboardViewModel : ObservableObject
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

    // ─── رسم الأعمدة: المصادر مقابل النشاط ───
    [ObservableProperty] private ISeries[] _sourceActivityBarSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _barXAxes = new Axis[] { new Axis() };
    [ObservableProperty] private Axis[] _barYAxes = new Axis[] { new Axis() };

    // ─── مخطط دائري: توزيع النظائر ───
    [ObservableProperty] private ISeries[] _sourcesByIsotopeSeries = Array.Empty<ISeries>();
    [ObservableProperty] private bool _hasEnoughIsotopeData;

    // ─── مخطط دائري: توزيع المواقع ───
    [ObservableProperty] private ISeries[] _sourcesByLocationSeries = Array.Empty<ISeries>();
    [ObservableProperty] private bool _hasEnoughLocationData;
    [ObservableProperty] private ObservableCollection<LegendItem> _locationLegendItems = new();

    // ─── منحنى التحلل الزمني ───
    [ObservableProperty] private ISeries[] _activityDecaySeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _decayXAxes = new Axis[] { new Axis() };
    [ObservableProperty] private Axis[] _decayYAxes = new Axis[] { new Axis() };

    // ─── قائمة المصادر المتاحة لمنحنى التحلل ───
    [ObservableProperty] private ObservableCollection<Source> _availableSources = new();
    [ObservableProperty] private Source? _selectedDecaySource;


    // ─── مصادر لوحة القيادة (نفس كائنات Source كصفحة المصادر) ───
    [ObservableProperty] private ObservableCollection<Source> _dashboardSources = new();

    // ─── الجزء 1: بطاقة تنبيهات انخفاض النشاط ───
    [ObservableProperty] private int _lowActivityCriticalCount;
    [ObservableProperty] private int _lowActivityWarningCount;
    [ObservableProperty] private bool _hasLowActivityAlerts;   // false → رسالة "ضمن الحدود الآمنة"

    // ─── الجزء 2: بطاقة ملخص الاستعارات ───
    [ObservableProperty] private DashboardBorrowSummary _borrowSummary = new();

    // ─── الجزء 3: جدول مصادر منخفضة النشاط ───
    [ObservableProperty] private ObservableCollection<LowActivitySourceRow> _lowActivitySources = new();
    [ObservableProperty] private bool _hasMoreLowActivitySources;  // true → عرض "عرض الكل"
    [ObservableProperty] private int _totalLowActivityCount;

    // ─── دهان نصوص التلميحات ───
    public SolidColorPaint ChartTextPaint => GetAxisPaint();
    public SolidColorPaint ChartTooltipBackgroundPaint => GetTooltipBackgroundPaint();

    // ─── مجموعات مفاتيح الرسم المخصصة ───
    [ObservableProperty] private ObservableCollection<LegendItem> _pieLegendItems = new();
    [ObservableProperty] private ObservableCollection<LegendItem> _decayLegendItems = new();

    // فرشاة المحاور (لـ WPF Overlay)
    [ObservableProperty] private System.Windows.Media.Brush _axisBrush = System.Windows.Media.Brushes.Transparent;

    // هامش الرسم الموحد لضمان انطباق الخطوط اليدوية (L-shape) مع محاور الرسم
    public LiveChartsCore.Measure.Margin ChartDrawMargin { get; } = new(50, 20, 20, 50);

    // إطار الرسم - نجعله شفافاً تماماً لأننا سنرسم المحاور يدوياً بشكل L في الـ XAML
    [ObservableProperty] private DrawMarginFrame? _decayDrawMarginFrame = new DrawMarginFrame { Stroke = null };
    [ObservableProperty] private DrawMarginFrame? _barDrawMarginFrame = new DrawMarginFrame { Stroke = null };

    // ألوان متعددة لمنحنيات التحلل من لوحة الألوان المعتمدة (Colors.xaml)
    private static readonly string[] DecayStrokeColors = { "#1F5A66", "#C97A4A", "#3FAE7A", "#4F7FA3", "#8E44AD" };
    private static readonly string[] DecayFillColors = { "#1A1F5A66", "#1AC97A4A", "#1A3FAE7A", "#1A4F7FA3", "#1A8E44AD" };

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
    }

    public DashboardViewModel(
        ISourceService sourceService,
        IRadioisotopeService isotopeService,
        ILocationService locationService,
        IDecayCalculationService decayService,
        IBorrowService borrowService,
        ISystemSettingsService settingsService)
    {
        _sourceService = sourceService;
        _isotopeService = isotopeService;
        _locationService = locationService;
        _decayService = decayService;
        _borrowService = borrowService;
        _settingsService = settingsService;

        InitDrawMarginFrames();
        _ = LoadDataAsync();
    }

    partial void OnSelectedDecaySourceChanged(Source? value)
    {
        UpdateDecayCurves(null, value);
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            await Task.Run(() => _sourceService.UpdateAllCurrentActivities());

            var sources = _sourceService.GetAllSources();

            // ═══ بطاقة 1: عدد المصادر المسجلة ═══
            TotalSources = sources.Count;

            // ═══ بطاقة 2: إجمالي النشاط بجميع الوحدات ═══
            UpdateTotalActivityItems(sources);

            // ═══ رسم الأعمدة: جميع المصادر مقابل النشاط ═══
            UpdateBarChart(sources);

            // ═══ مخطط دائري: توزيع النظائر (جميع النظائر الموجودة في المصادر) ═══
            UpdatePieChart(sources);

            // ═══ مخطط دائري: توزيع المواقع ═══
            UpdateLocationChart(sources);

            // ═══ منحنى التحلل: أعلى 5 مصادر + اختيار ═══
            AvailableSources = new ObservableCollection<Source>(
                sources.Where(s =>
                    (s.Radioisotope != null && s.InitialActivityUnit != null) ||
                    (s.HasDetailedIsotopes && s.SourceIsotopes != null && s.SourceIsotopes.Any(si => si.Radioisotope != null))
                ).ToList());
            UpdateDecayCurves(sources, SelectedDecaySource);

            // ═══ مصادر لوحة القيادة (كائنات Source مباشرة) ═══
            DashboardSources = new ObservableCollection<Source>(sources);

            // ═══ الجزء 1: بطاقة تنبيهات انخفاض النشاط ═══
            UpdateLowActivityAlertCard(sources);

            // ═══ الجزء 2: بطاقة ملخص الاستعارات ═══
            UpdateBorrowSummaryCard();

            // ═══ الجزء 3: جدول مصادر منخفضة النشاط ═══
            UpdateLowActivityTable(sources);
        }
        catch (Exception ex)
        {
            LoggerService.LogError(TranslationHelper.GetString("MsgErrDashboardLoad"), ex);
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
                    // Reverse decay for 1 day: A_yesterday = A_today / e^-lambda*1 = A_today * e^lambda
                    double yesterdayBq = currentBq * Math.Exp(lambda);
                    previousDayBq += yesterdayBq;
                }
                else
                {
                    previousDayBq += currentBq;
                }
            }

            // Set main display value
            TotalActivityDisplay = $"{totalBq:N0} Bq";

            // Calculate percentage change
            if (previousDayBq > 0)
            {
                double change = ((totalBq - previousDayBq) / previousDayBq) * 100;
                // Since total activity *always* decreases due to decay, change will naturally be negative.
                // However visually, if they just added a source, it would go up.
                // For demonstration, we'll format it:
                if (change > 0)
                {
                    ActivityChangePercent = $"+{change:F2}%";
                    ActivityChangeColor = "#4CAF50"; // Green
                    ActivityChangeIcon = "ArrowTopRight";
                }
                else
                {
                    ActivityChangePercent = $"{Math.Abs(change):F2}%"; // Show absolute value with down arrow
                    ActivityChangeColor = "#F44336"; // Red
                    ActivityChangeIcon = "ArrowBottomRight";
                }
            }
            else
            {
                ActivityChangePercent = "0.00%";
                ActivityChangeColor = "{DynamicResource TextSecondary}";
                ActivityChangeIcon = "Minus";
            }

            using var db = App.CreateDbContext();
            var activityUnits = db.ActivityUnits.OrderBy(u => u.UnitName).ToList();

            var items = new ObservableCollection<TotalActivityItem>();
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
            TotalActivityItems = items;
        }
        catch { }
    }

    // ───────────── رسم الأعمدة (جميع المصادر) — محاور بالإنجليزية ─────────────
    private void UpdateBarChart(List<Source> sources)
    {
        var activeSources = sources
            .Where(s => s.CurrentActivityValue > 0)
            .OrderByDescending(s =>
            {
                var unit = s.CurrentActivityUnit;
                return unit != null ? s.CurrentActivityValue * unit.ConversionToBq : s.CurrentActivityValue;
            })
            .ToList();

        var axisPaint = GetAxisPaint();

        if (!activeSources.Any())
        {
            BarXAxes = new Axis[] { new Axis { TextSize = 10, LabelsPaint = axisPaint } };
            BarYAxes = new Axis[] { new Axis { TextSize = 11, LabelsPaint = axisPaint } };
            return;
        }

        var labels = activeSources.Select(s => s.SourceCode).ToArray();
        var values = activeSources.Select(s =>
        {
            var unit = s.CurrentActivityUnit;
            double bq = unit != null ? s.CurrentActivityValue * unit.ConversionToBq : s.CurrentActivityValue;
            return bq > 0 ? Math.Log10(bq) : 0; // Manual log transform for Y-axis
        }).ToArray();

        SourceActivityBarSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = values,
                Name = "Activity (Bq)",
                Fill = new SolidColorPaint(SKColor.Parse("#1F5A66")),
                Stroke = null,
                MaxBarWidth = 30,
                Padding = 4
            }
        };

        var axisLinePaint = new SolidColorPaint(new SKColor(180, 180, 180, 100)) { StrokeThickness = 1 };

        BarXAxes = new Axis[]
        {
            new Axis
            {
                Labels = labels,
                TextSize = 10,
                LabelsRotation = activeSources.Count > 8 ? -45 : 0,
                LabelsPaint = axisPaint,
                SeparatorsPaint = axisLinePaint
            }
        };
        BarYAxes = new Axis[]
        {
            new Axis
            {
                TextSize = 11,
                Labeler = value =>
                {
                    double real = Math.Pow(10, value);
                    if (real < 1) return "0";
                    if (real < 1_000) return real.ToString("N0");
                    if (real < 1_000_000) return (real / 1_000).ToString("N1") + "K";
                    if (real < 1_000_000_000) return (real / 1_000_000).ToString("N1") + "M";
                    return (real / 1_000_000_000).ToString("N1") + "G";
                },
                LabelsPaint = axisPaint,
                SeparatorsPaint = axisLinePaint,
                Position = LiveChartsCore.Measure.AxisPosition.Start,
                MinStep = 1
            }
        };
    }

    // ───────────── مخطط دائري (جميع النظائر الموجودة فعلياً في المصادر) ─────────────
    private void UpdatePieChart(List<Source> sources)
    {
        // جمع جميع النظائر من المصادر (بما في ذلك المصادر متعددة النظائر)
        var isotopeNames = new List<string>();
        foreach (var s in sources)
        {
            if (!string.IsNullOrEmpty(s.DisplayIsotopes))
            {
                // DisplayIsotopes قد يحتوي على عدة نظائر مفصولة بفاصلة أو +
                var parts = s.DisplayIsotopes.Split(new[] { ',', '+', '/' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    isotopeNames.Add(part.Trim());
                }
            }
            else if (s.Radioisotope != null)
            {
                isotopeNames.Add(s.Radioisotope.Symbol);
            }
        }

        var byIsotope = isotopeNames
            .GroupBy(name => name)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        // فحص كفاية البيانات: يجب أن يوجد نوعان على الأقل لإظهار رسم مفيد
        HasEnoughIsotopeData = byIsotope.Count >= 2;

        if (HasEnoughIsotopeData)
        {
            var dataLabelsPaint = GetPieDataLabelsPaint();
            SourcesByIsotopeSeries = byIsotope.Select((x, idx) => new PieSeries<int>
            {
                Values = new[] { x.Count },
                Name = x.Label,
                Fill = new SolidColorPaint(SKColor.Parse(ChartPalette[idx % ChartPalette.Length])),
                InnerRadius = 40,
                DataLabelsPaint = dataLabelsPaint,
                DataLabelsSize = 13,
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle
            } as ISeries).ToArray();

            // تحديث مفتاح الرسم المخصص
            var legend = new ObservableCollection<LegendItem>();
            for (int i = 0; i < SourcesByIsotopeSeries.Length; i++)
            {
                if (SourcesByIsotopeSeries[i] is PieSeries<int> s)
                {
                    legend.Add(new LegendItem 
                    { 
                        Label = s.Name ?? string.Empty, 
                        Color = ChartPalette[i % ChartPalette.Length]
                    });
                }
            }
            PieLegendItems = legend;
        }
        else
        {
            SourcesByIsotopeSeries = Array.Empty<ISeries>();
            PieLegendItems.Clear();
        }
    }

    // ───────────── مخطط دائري: توزيع المصادر حسب الموقع ─────────────
    private void UpdateLocationChart(List<Source> sources)
    {
        var locations = sources
            .Where(s => s.Location != null)
            .GroupBy(s => s.Location!.LocationName)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        // فحص كفاية البيانات: يجب أن يوجد موقعان على الأقل لإظهار رسم مفيد
        HasEnoughLocationData = locations.Count >= 2;

        if (HasEnoughLocationData)
        {
            var dataLabelsPaint = GetPieDataLabelsPaint();
            SourcesByLocationSeries = locations.Select((x, idx) => (ISeries)new PieSeries<int>
            {
                Values = new[] { x.Count },
                Name = x.Label,
                Fill = new SolidColorPaint(SKColor.Parse(ChartPalette[idx % ChartPalette.Length])),
                InnerRadius = 40,
                DataLabelsPaint = dataLabelsPaint,
                DataLabelsSize = 13,
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle
            }).ToArray();

            var legend = new ObservableCollection<LegendItem>();
            for (int i = 0; i < SourcesByLocationSeries.Length; i++)
            {
                if (SourcesByLocationSeries[i] is PieSeries<int> s)
                {
                    legend.Add(new LegendItem 
                    { 
                        Label = s.Name ?? string.Empty, 
                        Color = ChartPalette[i % ChartPalette.Length]
                    });
                }
            }
            LocationLegendItems = legend;
        }
        else
        {
            SourcesByLocationSeries = Array.Empty<ISeries>();
            LocationLegendItems.Clear();
        }
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
                        new LegendItem { Label = TranslationHelper.GetString("CalcChartLegendCurve"), Color = "#1F5A66" },
                        new LegendItem { Label = TranslationHelper.GetString("CalcChartLegendCurrent"), Color = "#C25B4A" }
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
                                double sec = ConvertHalfLifeToSeconds(si.Radioisotope!.HalfLife, si.Radioisotope.HalfLifeUnit);
                                if (sec > maxHalfLifeSeconds) maxHalfLifeSeconds = sec;
                            }
                        }
                        else if (s.Radioisotope != null)
                        {
                            double sec = ConvertHalfLifeToSeconds(s.Radioisotope.HalfLife, s.Radioisotope.HalfLifeUnit);
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
                                        double hlSec = ConvertHalfLifeToSeconds(si.Radioisotope!.HalfLife, si.Radioisotope.HalfLifeUnit);
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
            double maxHalfLives = CalculateMaxHalfLivesElapsed(source);
            if (maxHalfLives >= 6.0) criticalCount++;
            else if (maxHalfLives >= 5.0) warningCount++;
        }

        LowActivityCriticalCount = criticalCount;
        LowActivityWarningCount = warningCount;
        HasLowActivityAlerts = criticalCount > 0 || warningCount > 0;
    }

    // ───────────── الجزء 2: بطاقة ملخص الاستعارات ─────────────
    private void UpdateBorrowSummaryCard()
    {
        try
        {
            var allRequests = _borrowService.GetAll();
            var now = DateTime.Now;
            var dueSoonThreshold = now.AddDays(7);

            int overdue = allRequests.Count(r => r.Status == "Overdue");
            int active  = allRequests.Count(r => r.Status == "Delivered" || r.Status == "Overdue");
            int dueSoon = allRequests.Count(r =>
                r.Status == "Delivered" &&
                r.ExpectedReturnDate >= now &&
                r.ExpectedReturnDate <= dueSoonThreshold);

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
            double maxHalfLives = CalculateMaxHalfLivesElapsed(source);
            if (maxHalfLives < 5.0) continue;

            string symbol = source.DisplayIsotopes ?? source.Radioisotope?.Symbol ?? "-";
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

    // ───────────── دالة مساعدة: احتساب أعلى عدد فترات نصف عمر منقضية ─────────────
    private static double CalculateMaxHalfLivesElapsed(Source source)
    {
        double max = -1;

        if (source.HasDetailedIsotopes &&
            source.SourceIsotopes != null &&
            source.SourceIsotopes.Any(si => si.Radioisotope != null))
        {
            foreach (var si in source.SourceIsotopes.Where(si => si.Radioisotope != null))
            {
                var isotope  = si.Radioisotope!;
                var calibDate = si.CalibrationDate ?? source.CalibrationDate;
                if (calibDate == default) continue;

                double halfLifeSec = ConvertHalfLifeToSeconds(isotope.HalfLife, isotope.HalfLifeUnit);
                if (halfLifeSec <= 0) continue;

                double elapsed = Math.Max(0, (DateTime.Now - calibDate).TotalSeconds);
                double hl = elapsed / halfLifeSec;
                if (hl > max) max = hl;
            }
        }
        else if (source.Radioisotope != null && source.CalibrationDate != default)
        {
            double halfLifeSec = ConvertHalfLifeToSeconds(
                source.Radioisotope.HalfLife, source.Radioisotope.HalfLifeUnit);
            if (halfLifeSec > 0)
            {
                double elapsed = Math.Max(0, (DateTime.Now - source.CalibrationDate).TotalSeconds);
                max = elapsed / halfLifeSec;
            }
        }

        return max;
    }

    private static double ConvertHalfLifeToSeconds(double value, string? unit) =>
        unit?.ToLower() switch
        {
            "seconds" => value,
            "minutes" => value * 60,
            "hours"   => value * 3600,
            "days"    => value * 86400,
            "years"   => value * 365.25 * 86400,
            _         => value * 365.25 * 86400
        };

    // ───────────── أوامر التنقل من لوحة القيادة ─────────────
    [RelayCommand]
    private void NavigateToLowActivityReport()
    {
        // الحصول على MainViewModel وتحديد نافذة التقارير وفتح تقرير المصادر المنخفضة
        if (App.ServiceProvider.GetService(typeof(MainViewModel)) is MainViewModel main)
        {
            main.NavigateTo("Reports");
            if (main.CurrentView is ReportsViewModel reportsVm)
            {
                reportsVm.SelectReportCommand.Execute("LowActivityReport");
            }
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
}
