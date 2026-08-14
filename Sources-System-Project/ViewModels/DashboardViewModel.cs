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
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Windows;

namespace Sources.ViewModels;

/// <summary>
/// نموذج صف في جدول المصادر والنشاط الحالي
/// </summary>
public class SourceActivityRow
{
    public string SourceCode { get; set; } = string.Empty;
    public string IsotopeSymbol { get; set; } = string.Empty;
    public string CurrentActivity { get; set; } = string.Empty;
    public string UnitSymbol { get; set; } = string.Empty;
    public string CalibrationDate { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public int StatusCode { get; set; } = 0; // 1: Active, 2: Stored, 3: Warning/Action
}

/// <summary>
/// نموذج لمفتاح الرسم المخصص لضمان التباين
/// </summary>
public class LegendItem
{
    public string Label { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFFFFF";
}

public partial class DashboardViewModel : ObservableObject
{
    private readonly ISourceService _sourceService;
    private readonly IRadioisotopeService _isotopeService;
    private readonly ILocationService _locationService;
    private readonly IDecayCalculationService _decayService;
    private readonly IBorrowService _borrowService;
    private readonly ISystemSettingsService _settingsService;

    // ─── بطاقة 1: عدد المصادر ───
    [ObservableProperty] private int _totalSources;
    [ObservableProperty] private int _activeSourcesCount;
    [ObservableProperty] private int _storedSourcesCount;
    [ObservableProperty] private int _actionRequiredCount;
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

    // ─── منحنى التحلل الزمني ───
    [ObservableProperty] private ISeries[] _activityDecaySeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _decayXAxes = new Axis[] { new Axis() };
    [ObservableProperty] private Axis[] _decayYAxes = new Axis[] { new Axis() };

    // ─── قائمة المصادر المتاحة لمنحنى التحلل ───
    [ObservableProperty] private ObservableCollection<Source> _availableSources = new();
    [ObservableProperty] private Source? _selectedDecaySource;

    // ─── جدول المصادر والنشاط ───
    [ObservableProperty] private ObservableCollection<SourceActivityRow> _sourceActivityTable = new();

    // ─── مصادر لوحة القيادة (نفس كائنات Source كصفحة المصادر) ───
    [ObservableProperty] private ObservableCollection<Source> _dashboardSources = new();

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

    // ألوان متعددة لمنحنيات التحلل
    private static readonly string[] DecayColors = { "#1F5A66", "#C97A4A", "#3FAE7A", "#E0A93E", "#4F7FA3" };

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

            // ═══ بطاقة 1: عدد المصادر المسجلة وحالاتها ═══
            TotalSources = sources.Count;
            // Calculate statuses based on SourceStatus (Assuming "نشط", "مخزن", "Active", "Stored")
            // Also warning if leak test is due or expired
            ActiveSourcesCount = sources.Count(s => s.Status.Contains("نشط") || s.Status.Contains("Active") || s.Status.Contains("قيد الاستخدام") || s.Status.Contains("In Use"));
            StoredSourcesCount = sources.Count(s => s.Status.Contains("مخزن") || s.Status.Contains("Stored"));
            ActionRequiredCount = sources.Count - ActiveSourcesCount - StoredSourcesCount; // Simplification for demo, ideally checks dates

            // ═══ بطاقة 2: إجمالي النشاط بجميع الوحدات ═══
            UpdateTotalActivityItems(sources);

            // ═══ رسم الأعمدة: جميع المصادر مقابل النشاط ═══
            UpdateBarChart(sources);

            // ═══ مخطط دائري: توزيع النظائر (جميع النظائر الموجودة في المصادر) ═══
            UpdatePieChart(sources);

            // ═══ منحنى التحلل: أعلى 5 مصادر + اختيار ═══
            AvailableSources = new ObservableCollection<Source>(
                sources.Where(s => s.Radioisotope != null).ToList());
            UpdateDecayCurves(sources, SelectedDecaySource);

            // ═══ جدول المصادر والنشاط الحالي (مع جميع النظائر) ═══
            UpdateSourceTable(sources);

            // ═══ مصادر لوحة القيادة (كائنات Source مباشرة) ═══
            DashboardSources = new ObservableCollection<Source>(sources);
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

        var axisPaint = GetAxisPaint();
        SourcesByIsotopeSeries = byIsotope.Select(x => new PieSeries<int>
        {
            Values = new[] { x.Count },
            Name = x.Label,
            DataLabelsPaint = axisPaint,
            DataLabelsSize = 12
        } as ISeries).ToArray();

        // تحديث مفتاح الرسم المخصص
        var legend = new ObservableCollection<LegendItem>();
        for (int i = 0; i < SourcesByIsotopeSeries.Length; i++)
        {
            var s = SourcesByIsotopeSeries[i] as PieSeries<int>;
            if (s != null)
            {
                legend.Add(new LegendItem 
                { 
                    Label = s.Name ?? "", 
                    Color = (s.Fill as SolidColorPaint)?.Color.ToString() ?? "#1F5A66" 
                });
            }
        }
        PieLegendItems = legend;
    }

    // ───────────── منحنى التحلل الزمني — محاور بالإنجليزية + تواريخ ─────────────
    private void UpdateDecayCurves(List<Source>? allSources, Source? selectedSource)
    {
        var axisPaint = GetAxisPaint();
        var axisLinePaint = new SolidColorPaint(new SKColor(180, 180, 180, 100)) { StrokeThickness = 1 };
        var seriesList = new List<ISeries>();
        var dateLabels = new List<string>();

        // Labeler helper to convert log10 values back to readable format
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

            List<Source> sourcesToRender = new();

            // Debug: trace selection logic
            if (selectedSource != null)
            {
                Console.Error.WriteLine($"[DecayCurve] Selected: {selectedSource.SourceCode}, " +
                    $"Radioisotope={selectedSource.Radioisotope?.Symbol ?? "NULL"}, " +
                    $"InitialActivityUnit={(selectedSource.InitialActivityUnit != null ? "OK" : "NULL")}, " +
                    $"InitialActivityValue={selectedSource.InitialActivityValue}");
            }

            if (selectedSource?.Radioisotope != null && selectedSource.InitialActivityUnit != null)
            {
                sourcesToRender.Add(selectedSource);
            }
            else if (selectedSource != null)
            {
                // The selected source is missing navigation properties — try to find the full version from DB
                var fullSource = sources.FirstOrDefault(s => s.SourceCode == selectedSource.SourceCode);
                if (fullSource?.Radioisotope != null && fullSource.InitialActivityUnit != null)
                {
                    sourcesToRender.Add(fullSource);
                    Console.Error.WriteLine($"[DecayCurve] Recovered full source for: {fullSource.SourceCode}");
                }
                else
                {
                    Console.Error.WriteLine($"[DecayCurve] Could not recover source: {selectedSource.SourceCode}");
                }
            }
            else
            {
                sourcesToRender = sources
                    .Where(s => s.Radioisotope != null && s.InitialActivityUnit != null)
                    .OrderByDescending(s =>
                    {
                        var unit = s.CurrentActivityUnit;
                        return unit != null ? s.CurrentActivityValue * unit.ConversionToBq : s.CurrentActivityValue;
                    })
                    .Take(5)
                    .ToList();
            }

            if (sourcesToRender.Any())
            {
                // توحيد النطاق الزمني لجميع النطاقات المعروضة
                DateTime startDate = sourcesToRender.Min(s => s.CalibrationDate);

                // البحث عن أطول نصف عمر لاحتساب نهاية المنحنى (5 أنصاف أعمار في المستقبل)
                double maxHalfLifeSeconds = 0;
                foreach (var s in sourcesToRender)
                {
                    double val = s.Radioisotope!.HalfLife;
                    string unit = s.Radioisotope.HalfLifeUnit?.ToLower() ?? "years";
                    double seconds = unit switch
                    {
                        "seconds" => val,
                        "minutes" => val * 60,
                        "hours" => val * 3600,
                        "days" => val * 86400,
                        "years" => val * 365.25 * 86400,
                        _ => val * 365.25 * 86400
                    };
                    if (seconds > maxHalfLifeSeconds) maxHalfLifeSeconds = seconds;
                }

                DateTime endDate;
                try
                {
                    double secondsToAdd = maxHalfLifeSeconds * 5;
                    // Cap seconds to avoid overflowing DateTime.MaxValue
                    double maxSecondsAllowed = (DateTime.MaxValue - DateTime.Now).TotalSeconds;
                    if (secondsToAdd > maxSecondsAllowed) secondsToAdd = maxSecondsAllowed - 86400; // Leave 1 day buffer
                    
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
                    // Fallback to max possible date if any math overflow occurs
                    endDate = DateTime.MaxValue.AddDays(-1);
                }

                bool labelsSet = false;

                for (int i = 0; i < sourcesToRender.Count; i++)
                {
                    try
                    {
                        var source = sourcesToRender[i];
                        var initialBq = source.InitialActivityValue * source.InitialActivityUnit!.ConversionToBq;
                        
                        var curve = _decayService.GenerateUnifiedDecayCurve(
                            initialBq, source.Radioisotope!.HalfLife, source.Radioisotope.HalfLifeUnit,
                            source.CalibrationDate, startDate, endDate, 50);

                        if (!labelsSet && curve.Any())
                        {
                            dateLabels = curve.Select(c => c.Time.ToString("M-yyyy")).ToList();
                            labelsSet = true;
                        }

                        var values = curve.Select(c => c.Activity > 0 ? Math.Log10(c.Activity) : 0).ToArray();
                        var color = selectedSource != null ? SKColor.Parse("#4A86E8") : SKColor.Parse(DecayColors[i % DecayColors.Length]);
                        var thickness = selectedSource != null ? 4f : 2.5f;

                        seriesList.Add(new LineSeries<double>
                        {
                            Values = values,
                            Name = source.SourceCode,
                            Fill = null,
                            GeometrySize = 0,
                            GeometryFill = null,
                            GeometryStroke = null,
                            Stroke = new SolidColorPaint(color, thickness)
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[DecayCurve] Error for source {sourcesToRender[i].SourceCode}: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DecayCurve] General error: {ex.Message}\n{ex.StackTrace}");
        }

        // Always set the chart properties — even if data generation failed
        ActivityDecaySeries = seriesList.ToArray();

        // تحديث مفتاح الرسم المخصص لمنحنى التحلل
        var decayLegend = new ObservableCollection<LegendItem>();
        foreach (var s in seriesList)
        {
            var ls = s as LineSeries<double>;
            if (ls != null)
            {
                decayLegend.Add(new LegendItem 
                { 
                    Label = ls.Name ?? "", 
                    Color = (ls.Stroke as SolidColorPaint)?.Color.ToString() ?? "#1F5A66"
                });
            }
        }
        DecayLegendItems = decayLegend;
        
        DecayXAxes = new Axis[] 
        { 
            new Axis 
            { 
                TextSize = 11,
                Labels = dateLabels.Count > 0 ? dateLabels.ToArray() : null,
                LabelsPaint = axisPaint,
                TicksPaint = null,
                MinStep = 1,
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
    }
    // ───────────── جدول المصادر والنشاط (مع جميع النظائر) ─────────────
    private void UpdateSourceTable(List<Source> sources)
    {
        var rows = sources.Select(s => 
        {
            // Determine status formatting
            string status = s.Status ?? "Unknown";
            int code = 3; // Action required by default
            if (status.Contains("نشط") || status.Contains("Active") || status.Contains("استخدام") || status.Contains("Use"))
                code = 1;
            else if (status.Contains("مخزن") || status.Contains("Stored"))
                code = 2;

            return new SourceActivityRow
            {
                SourceCode = s.SourceCode,
                // استخدام DisplayIsotopes لعرض جميع النظائر (بما في ذلك الخلائط)
                IsotopeSymbol = !string.IsNullOrEmpty(s.DisplayIsotopes) 
                    ? s.DisplayIsotopes 
                    : (s.Radioisotope?.Symbol ?? "-"),
                CurrentActivity = FormatActivityShort(s.CurrentActivityValue),
                UnitSymbol = s.CurrentActivityUnit?.UnitSymbol ?? "Bq",
                CalibrationDate = s.CalibrationDate.ToString("dd/MM/yyyy"),
                StatusDisplay = IsArabic 
                    ? (code == 1 ? "نشط" : (code == 2 ? "مخزن" : status))
                    : (code == 1 ? "Active" : (code == 2 ? "Stored" : status)),
                StatusCode = code
            };
        }).ToList();
        SourceActivityTable = new ObservableCollection<SourceActivityRow>(rows);
    }

    // ───────────── أدوات التنسيق ─────────────
    private string FormatActivityValue(double value, string unitSymbol)
    {
        if (value == 0) return $"0 {unitSymbol}";
        if (Math.Abs(value) >= 1e9) return $"{value:E3} {unitSymbol}";
        if (Math.Abs(value) >= 1e6) return $"{value:N0} {unitSymbol}";
        if (Math.Abs(value) >= 1000) return $"{value:N2} {unitSymbol}";
        if (Math.Abs(value) >= 1) return $"{value:N4} {unitSymbol}";
        return $"{value:E3} {unitSymbol}";
    }

    private string FormatActivityShort(double value)
    {
        if (value == 0) return "0";
        if (Math.Abs(value) >= 1e7 || Math.Abs(value) < 0.0001) return value.ToString("E2");
        return (value % 1 == 0) ? value.ToString("#,##0") : value.ToString("#,##0.00");
    }
}
