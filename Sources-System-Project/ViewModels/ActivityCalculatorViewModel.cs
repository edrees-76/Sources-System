using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Sources.ViewModels;

public partial class ActivityCalculatorViewModel : ObservableObject
{
    private readonly IRadioisotopeService _isotopeService;
    private readonly IDecayCalculationService _decayService;

    // ─── أوضاع الحاسبة ───
    // 0: حساب النشاط الحالي/المستقبلي، 1: حساب الزمن لنشاط مستهدف
    [ObservableProperty] private int _selectedModeIndex = 0;
    [ObservableProperty] private bool _isActivityMode = true;
    [ObservableProperty] private bool _isTimeToTargetMode = false;

    // ─── قائمة النظائر من قاعدة البيانات ───
    [ObservableProperty] private ObservableCollection<Radioisotope> _isotopes = new();
    [ObservableProperty] private Radioisotope? _selectedIsotope;

    // ─── إعدادات الإدخال ───
    [ObservableProperty] private bool _isFromDatabase = true;
    [ObservableProperty] private bool _isManualInput = false;

    // ─── المدخلات المشتركة ───
    [ObservableProperty] private string _initialActivityText = string.Empty;
    [ObservableProperty] private string _initialActivityUnit = "MBq";
    [ObservableProperty] private string _halfLifeValueText = string.Empty;
    [ObservableProperty] private string _halfLifeUnit = "years";
    [ObservableProperty] private DateTime _calibrationDate = DateTime.Today.AddYears(-1);
    [ObservableProperty] private DateTime _calculationDate = DateTime.Today;
    [ObservableProperty] private string _selectedOutputUnit = "MBq";

    // ─── مدخلات وضع النشاط المستهدف ───
    [ObservableProperty] private string _targetActivityText = string.Empty;
    [ObservableProperty] private string _targetActivityUnit = "MBq";

    // ─── مؤشرات الأخطاء التفاعلية ───
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasInitialActivityError;
    [ObservableProperty] private bool _hasHalfLifeError;
    [ObservableProperty] private bool _hasTargetActivityError;
    [ObservableProperty] private bool _hasDateOrderError;

    // ─── نتائج وضع النشاط ───
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private string _resultActivityText = string.Empty;
    [ObservableProperty] private double _resultActivityBq;
    [ObservableProperty] private string _remainingPercentText = string.Empty;
    [ObservableProperty] private string _decayedPercentText = string.Empty;
    [ObservableProperty] private string _halfLivesElapsedText = string.Empty;
    [ObservableProperty] private string _decayConstantText = string.Empty;
    [ObservableProperty] private string _elapsedTimeText = string.Empty;

    // ─── نتائج وضع الزمن المستهدف ───
    [ObservableProperty] private string _requiredTimeText = string.Empty;
    [ObservableProperty] private string _targetEstimatedDateText = string.Empty;
    [ObservableProperty] private string _targetHalfLivesText = string.Empty;

    // ─── الرسم البياني لمنحنى الاضمحلال ───
    [ObservableProperty] private ISeries[] _decaySeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _decayXAxes = new Axis[] { new Axis() };
    [ObservableProperty] private Axis[] _decayYAxes = new Axis[] { new Axis() };
    [ObservableProperty] private DrawMarginFrame? _chartDrawMarginFrame = new DrawMarginFrame { Stroke = null };
    [ObservableProperty] private ObservableCollection<LegendItem> _chartLegendItems = new();
    [ObservableProperty] private bool _hasChartData;

    public LiveChartsCore.Measure.Margin ChartDrawMargin { get; } = new(60, 20, 20, 50);
    public SolidColorPaint ChartTextPaint => GetAxisPaint();
    public SolidColorPaint ChartTooltipBackgroundPaint => GetTooltipBackgroundPaint();

    // ─── الوحدات المتاحة ───
    public ObservableCollection<string> TimeUnits { get; } = new()
    {
        "seconds", "minutes", "hours", "days", "months", "years"
    };

    public ObservableCollection<string> ActivityUnits { get; } = new()
    {
        "Bq", "kBq", "MBq", "GBq", "TBq", "Ci", "mCi", "µCi"
    };

    public ActivityCalculatorViewModel(IRadioisotopeService isotopeService, IDecayCalculationService decayService)
    {
        _isotopeService = isotopeService;
        _decayService = decayService;
        InitChartAxes();
        LoadIsotopes();
    }

    private void InitChartAxes()
    {
        var axisPaint = GetAxisPaint();
        var axisLinePaint = GetAxisLinePaint();

        DecayXAxes = new Axis[]
        {
            new Axis
            {
                LabelsPaint = axisPaint,
                SeparatorsPaint = axisLinePaint,
                TextSize = 11
            }
        };

        DecayYAxes = new Axis[]
        {
            new Axis
            {
                LabelsPaint = axisPaint,
                SeparatorsPaint = axisLinePaint,
                TextSize = 11
            }
        };
    }

    private void LoadIsotopes()
    {
        try
        {
            var all = _isotopeService.GetAll();
            Isotopes = new ObservableCollection<Radioisotope>(all);
        }
        catch
        {
            Isotopes = new ObservableCollection<Radioisotope>();
        }
    }

    partial void OnSelectedModeIndexChanged(int value)
    {
        IsActivityMode = value == 0;
        IsTimeToTargetMode = value == 1;
        ClearErrors();
    }

    partial void OnSelectedIsotopeChanged(Radioisotope? value)
    {
        if (value != null && IsFromDatabase)
        {
            HalfLifeValueText = value.HalfLife.ToString("G");
            HalfLifeUnit = value.HalfLifeUnit;
            HasHalfLifeError = false;
        }
    }

    partial void OnIsFromDatabaseChanged(bool value)
    {
        if (IsManualInput == value) IsManualInput = !value;
        if (!value)
        {
            SelectedIsotope = null;
            HalfLifeValueText = string.Empty;
        }
    }

    partial void OnIsManualInputChanged(bool value)
    {
        if (IsFromDatabase == value) IsFromDatabase = !value;
    }

    partial void OnSelectedOutputUnitChanged(string value)
    {
        if (HasResult && ResultActivityBq > 0 && IsActivityMode)
        {
            double converted = _decayService.ConvertFromBq(ResultActivityBq, value);
            ResultActivityText = $"{FormatScientific(converted)} {value}";
        }
    }

    private void ClearErrors()
    {
        HasError = false;
        ErrorMessage = string.Empty;
        HasInitialActivityError = false;
        HasHalfLifeError = false;
        HasTargetActivityError = false;
        HasDateOrderError = false;
    }

    [RelayCommand]
    private void Calculate()
    {
        ClearErrors();
        HasResult = false;
        HasChartData = false;

        // ─── 1. التحقق من النشاط الأولي A₀ ───
        if (!double.TryParse(InitialActivityText, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double A0) || A0 <= 0)
        {
            HasError = true;
            HasInitialActivityError = true;
            ErrorMessage = TranslationHelper.GetString("CalcErrInitialActivity");
            return;
        }

        // ─── 2. التحقق من نصف العمر T½ ───
        if (!double.TryParse(HalfLifeValueText, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double halfLife) || halfLife <= 0)
        {
            HasError = true;
            HasHalfLifeError = true;
            ErrorMessage = TranslationHelper.GetString("CalcErrHalfLife");
            return;
        }

        double A0_Bq = _decayService.ConvertToBq(A0, InitialActivityUnit);
        double halfLifeSeconds = _decayService.ConvertTimeToSeconds(halfLife, HalfLifeUnit);
        if (halfLifeSeconds <= 0) halfLifeSeconds = 1;
        double lambda = Math.Log(2) / halfLifeSeconds;

        if (IsActivityMode)
        {
            // ─── 3. التحقق من ترتيب التواريخ ───
            if (CalculationDate < CalibrationDate)
            {
                HasError = true;
                HasDateOrderError = true;
                ErrorMessage = TranslationHelper.GetString("CalcErrDateOrder");
                return;
            }

            // ─── حساب النشاط الحالي/المستقبلي عبر الخدمة المركزية ───
            double activityBq = _decayService.CalculateActivityAtDate(A0_Bq, halfLife, HalfLifeUnit, CalibrationDate, CalculationDate);
            ResultActivityBq = activityBq;

            // حساب النسبة المتبقية والنسبة المضمحلة
            double remainingPercent = (activityBq / A0_Bq) * 100.0;
            if (remainingPercent > 100.0) remainingPercent = 100.0;
            if (remainingPercent < 0.0) remainingPercent = 0.0;
            double decayedPercent = _decayService.CalculateDecayPercentage(A0_Bq, activityBq);

            TimeSpan elapsed = CalculationDate - CalibrationDate;
            double halfLivesElapsed = elapsed.TotalSeconds / halfLifeSeconds;

            // تنسيق المخرجات
            double convertedResult = _decayService.ConvertFromBq(activityBq, SelectedOutputUnit);
            ResultActivityText = $"{FormatScientific(convertedResult)} {SelectedOutputUnit}";
            RemainingPercentText = $"{remainingPercent:F2} %";
            DecayedPercentText = $"{decayedPercent:F2} %";
            HalfLivesElapsedText = $"{halfLivesElapsed:F2} T½";
            DecayConstantText = FormatScientific(lambda) + " s⁻¹";
            ElapsedTimeText = FormatElapsedTime(elapsed);

            // توليد وتحديث الرسم البياني
            BuildDecayChart(A0_Bq, halfLife, HalfLifeUnit, CalibrationDate, CalculationDate, activityBq);
            HasResult = true;
        }
        else
        {
            // ─── وضع حساب الزمن لنشاط مستهدف ───
            if (!double.TryParse(TargetActivityText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double targetActivity) || targetActivity <= 0)
            {
                HasError = true;
                HasTargetActivityError = true;
                ErrorMessage = TranslationHelper.GetString("CalcErrTargetActivity");
                return;
            }

            double targetBq = _decayService.ConvertToBq(targetActivity, TargetActivityUnit);
            if (targetBq >= A0_Bq)
            {
                HasError = true;
                HasTargetActivityError = true;
                ErrorMessage = TranslationHelper.GetString("CalcErrTargetGreater");
                return;
            }

            double requiredSeconds = _decayService.CalculateTimeToActivity(A0_Bq, targetBq, halfLife, HalfLifeUnit);
            TimeSpan requiredSpan = TimeSpan.FromSeconds(requiredSeconds);
            DateTime targetEstimatedDate = CalibrationDate.AddSeconds(requiredSeconds);
            double targetHalfLives = requiredSeconds / halfLifeSeconds;

            double remainingPercent = (targetBq / A0_Bq) * 100.0;
            double decayedPercent = _decayService.CalculateDecayPercentage(A0_Bq, targetBq);

            ResultActivityBq = targetBq;
            double convertedResult = _decayService.ConvertFromBq(targetBq, SelectedOutputUnit);
            ResultActivityText = $"{FormatScientific(convertedResult)} {SelectedOutputUnit}";
            RemainingPercentText = $"{remainingPercent:F2} %";
            DecayedPercentText = $"{decayedPercent:F2} %";
            RequiredTimeText = FormatElapsedTime(requiredSpan);
            TargetEstimatedDateText = targetEstimatedDate.ToString("yyyy/MM/dd");
            TargetHalfLivesText = $"{targetHalfLives:F2} T½";
            DecayConstantText = FormatScientific(lambda) + " s⁻¹";

            // توليد الرسم البياني محدد عليه نقطة النشاط المستهدف
            BuildDecayChart(A0_Bq, halfLife, HalfLifeUnit, CalibrationDate, targetEstimatedDate, targetBq);
            HasResult = true;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedIsotope = null;
        InitialActivityText = string.Empty;
        InitialActivityUnit = "MBq";
        HalfLifeValueText = string.Empty;
        HalfLifeUnit = "years";
        CalibrationDate = DateTime.Today.AddYears(-1);
        CalculationDate = DateTime.Today;
        TargetActivityText = string.Empty;
        TargetActivityUnit = "MBq";
        SelectedOutputUnit = "MBq";
        ResultActivityBq = 0;
        ResultActivityText = string.Empty;
        RemainingPercentText = string.Empty;
        DecayedPercentText = string.Empty;
        HalfLivesElapsedText = string.Empty;
        DecayConstantText = string.Empty;
        ElapsedTimeText = string.Empty;
        RequiredTimeText = string.Empty;
        TargetEstimatedDateText = string.Empty;
        TargetHalfLivesText = string.Empty;
        HasResult = false;
        HasChartData = false;
        DecaySeries = Array.Empty<ISeries>();
        InitChartAxes();
        ClearErrors();
    }

    [RelayCommand]
    private void CopyResult()
    {
        if (!string.IsNullOrEmpty(ResultActivityText))
        {
            try
            {
                string copyData = IsActivityMode
                    ? $"{TranslationHelper.GetString("CalcResultActivity")}: {ResultActivityText}\n{TranslationHelper.GetString("CalcResultRemainingPercent")}: {RemainingPercentText}\n{TranslationHelper.GetString("CalcResultElapsed")}: {ElapsedTimeText}"
                    : $"{TranslationHelper.GetString("CalcResultRequiredTime")}: {RequiredTimeText}\n{TranslationHelper.GetString("CalcResultTargetDate")}: {TargetEstimatedDateText}\n{TranslationHelper.GetString("CalcResultRemainingPercent")}: {RemainingPercentText}";
                System.Windows.Clipboard.SetText(copyData);
            }
            catch { }
        }
    }

    // ═══════════════════════════════════════════════════════
    // إعداد ورسم منحنى الاضمحلال عبر LiveChartsCore
    // ═══════════════════════════════════════════════════════
    private void BuildDecayChart(double initialBq, double halfLife, string halfLifeUnit, DateTime calibDate, DateTime pointDate, double pointActivityBq)
    {
        try
        {
            var rawCurve = _decayService.GenerateDecayCurve(initialBq, halfLife, halfLifeUnit, calibDate, 60);
            if (rawCurve == null || rawCurve.Count == 0)
            {
                HasChartData = false;
                return;
            }

            string outUnit = SelectedOutputUnit;
            var curvePoints = new List<DateTimePoint>();
            foreach (var pt in rawCurve)
            {
                double convertedVal = _decayService.ConvertFromBq(pt.Activity, outUnit);
                curvePoints.Add(new DateTimePoint(pt.Time, convertedVal));
            }

            double convertedPointVal = _decayService.ConvertFromBq(pointActivityBq, outUnit);
            var calcPoint = new List<DateTimePoint> { new DateTimePoint(pointDate, convertedPointVal) };

            var axisPaint = GetAxisPaint();
            var axisLinePaint = GetAxisLinePaint();

            var seriesList = new List<ISeries>
            {
                new LineSeries<DateTimePoint>
                {
                    Name = TranslationHelper.GetString("CalcChartLegendCurve"),
                    Values = curvePoints,
                    Stroke = new SolidColorPaint(SKColor.Parse("#1976D2")) { StrokeThickness = 3 },
                    GeometrySize = 0,
                    Fill = new SolidColorPaint(SKColor.Parse("#1A1976D2")),
                    LineSmoothness = 0.65
                },
                new ScatterSeries<DateTimePoint>
                {
                    Name = TranslationHelper.GetString("CalcChartLegendCurrent"),
                    Values = calcPoint,
                    Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                    Fill = new SolidColorPaint(SKColor.Parse("#D32F2F")),
                    GeometrySize = 14
                }
            };

            DecaySeries = seriesList.ToArray();

            DecayXAxes = new Axis[]
            {
                new DateTimeAxis(TimeSpan.FromDays(1), date => date.ToString("yyyy/MM/dd"))
                {
                    LabelsPaint = axisPaint,
                    SeparatorsPaint = axisLinePaint,
                    TextSize = 11
                }
            };

            DecayYAxes = new Axis[]
            {
                new Axis
                {
                    Labeler = val => $"{FormatScientific(val)} {outUnit}",
                    LabelsPaint = axisPaint,
                    SeparatorsPaint = axisLinePaint,
                    TextSize = 11
                }
            };

            var legendList = new ObservableCollection<LegendItem>
            {
                new LegendItem { Label = TranslationHelper.GetString("CalcChartLegendCurve"), Color = "#1976D2" },
                new LegendItem { Label = TranslationHelper.GetString("CalcChartLegendCurrent"), Color = "#D32F2F" }
            };
            ChartLegendItems = legendList;
            HasChartData = true;
        }
        catch
        {
            HasChartData = false;
        }
    }

    private static SolidColorPaint GetAxisPaint()
    {
        var isDark = SettingsHelper.IsDarkMode;
        var color = isDark ? new SKColor(210, 210, 210) : new SKColor(70, 70, 70);
        return new SolidColorPaint(color);
    }

    private static SolidColorPaint GetTooltipBackgroundPaint()
    {
        var isDark = SettingsHelper.IsDarkMode;
        var color = isDark ? new SKColor(40, 40, 40) : new SKColor(250, 250, 250);
        return new SolidColorPaint(color);
    }

    private static SolidColorPaint GetAxisLinePaint()
    {
        var isDark = SettingsHelper.IsDarkMode;
        var color = isDark ? new SKColor(80, 80, 80, 100) : new SKColor(200, 200, 200, 150);
        return new SolidColorPaint(color) { StrokeThickness = 1f };
    }

    private static string FormatElapsedTime(TimeSpan elapsed)
    {
        double totalDays = Math.Abs(elapsed.TotalDays);
        if (totalDays >= 365.25)
        {
            double years = totalDays / 365.25;
            double remainDays = totalDays % 365.25;
            return $"{(int)years} yr, {(int)remainDays} d";
        }
        if (totalDays >= 30)
        {
            int months = (int)(totalDays / 30);
            int days = (int)(totalDays % 30);
            return $"{months} mo, {days} d";
        }
        if (totalDays >= 1)
            return $"{(int)totalDays} d, {elapsed.Hours} h";
        if (elapsed.TotalHours >= 1)
            return $"{(int)elapsed.TotalHours} h, {elapsed.Minutes} min";
        return $"{(int)elapsed.TotalMinutes} min";
    }

    private static string FormatScientific(double value)
    {
        if (value == 0) return "0";
        if (Math.Abs(value) >= 1e6 || Math.Abs(value) < 0.01)
        {
            string sci = value.ToString("E4");
            var parts = sci.Split('E');
            if (parts.Length == 2)
            {
                string baseNum = double.Parse(parts[0]).ToString("0.####");
                string exp = parts[1].Replace("+", "").TrimStart('0');
                if (string.IsNullOrEmpty(exp)) exp = "0";
                bool negative = parts[1].Contains('-');
                return $"{baseNum} × 10{(negative ? "⁻" : "")}{ToSuperscript(exp.Replace("-", ""))}";
            }
        }
        return value.ToString("0.####");
    }

    private static string ToSuperscript(string number)
    {
        var map = new Dictionary<char, char>
        {
            {'0', '⁰'}, {'1', '¹'}, {'2', '²'}, {'3', '³'}, {'4', '⁴'},
            {'5', '⁵'}, {'6', '⁶'}, {'7', '⁷'}, {'8', '⁸'}, {'9', '⁹'}
        };
        return new string(number.Select(c => map.ContainsKey(c) ? map[c] : c).ToArray());
    }
}
