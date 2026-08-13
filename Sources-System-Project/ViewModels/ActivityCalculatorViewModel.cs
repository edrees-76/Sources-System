using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Models;
using Sources.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Sources.ViewModels;

public partial class ActivityCalculatorViewModel : ObservableObject
{
    private readonly IRadioisotopeService _service;

    // ─── قائمة النظائر من قاعدة البيانات ───
    [ObservableProperty] private ObservableCollection<Radioisotope> _isotopes = new();
    [ObservableProperty] private Radioisotope? _selectedIsotope;

    // ─── إعدادات الإدخال ───
    [ObservableProperty] private bool _isFromDatabase = true;
    [ObservableProperty] private bool _isManualInput;
    [ObservableProperty] private string _customIsotopeName = string.Empty;

    // ─── المدخلات ───
    [ObservableProperty] private string _initialActivityText = string.Empty;
    [ObservableProperty] private string _initialActivityUnit = "Bq";
    [ObservableProperty] private string _halfLifeValueText = string.Empty;
    [ObservableProperty] private string _halfLifeUnit = "years";
    [ObservableProperty] private DateTime _calibrationDate = DateTime.Today.AddYears(-1);
    [ObservableProperty] private DateTime _calculationDate = DateTime.Today;

    // ─── المخرجات ───
    [ObservableProperty] private string _resultText = string.Empty;
    [ObservableProperty] private string _decayConstantText = string.Empty;
    [ObservableProperty] private string _elapsedTimeText = string.Empty;
    [ObservableProperty] private string _selectedOutputUnit = "Bq";
    [ObservableProperty] private double _resultActivityBq;
    [ObservableProperty] private bool _hasResult;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError;

    // ─── كل الوحدات المتاحة ───
    public ObservableCollection<string> TimeUnits { get; } = new()
    {
        "seconds", "minutes", "hours", "days", "months", "years"
    };

    public ObservableCollection<string> ActivityUnits { get; } = new()
    {
        "Bq", "kBq", "MBq", "GBq", "Ci", "mCi", "µCi"
    };

    public ActivityCalculatorViewModel(IRadioisotopeService service)
    {
        _service = service;
        LoadIsotopes();
    }

    private void LoadIsotopes()
    {
        var all = _service.GetAll();
        Isotopes = new ObservableCollection<Radioisotope>(all);
    }

    // ─── عند اختيار نظير، ملء نصف العمر تلقائياً ───
    partial void OnSelectedIsotopeChanged(Radioisotope? value)
    {
        if (value != null && IsFromDatabase)
        {
            HalfLifeValueText = value.HalfLife.ToString("G");
            HalfLifeUnit = value.HalfLifeUnit;
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

    // ─── عند تغيير وحدة الخرج، إعادة حساب العرض ───
    partial void OnSelectedOutputUnitChanged(string value)
    {
        if (HasResult && ResultActivityBq > 0)
        {
            ResultText = FormatResult(ResultActivityBq, value);
        }
    }

    [RelayCommand]
    private void Calculate()
    {
        HasError = false;
        ErrorMessage = string.Empty;
        HasResult = false;

        // ─── التحقق من النشاط الأولي A₀ ───
        if (!double.TryParse(InitialActivityText, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double A0) || A0 <= 0)
        {
            HasError = true;
            ErrorMessage = Helpers.TranslationHelper.GetString("CalcErrInvalidInput");
            return;
        }

        // ─── التحقق من نصف العمر ───
        if (!double.TryParse(HalfLifeValueText, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double halfLife) || halfLife <= 0)
        {
            HasError = true;
            ErrorMessage = Helpers.TranslationHelper.GetString("CalcErrInvalidInput");
            return;
        }

        // ─── التحقق من التواريخ ───
        if (CalculationDate < CalibrationDate)
        {
            HasError = true;
            ErrorMessage = Helpers.TranslationHelper.GetString("CalcErrDateOrder");
            return;
        }

        // ─── تحويل النشاط الأولي إلى Bq ───
        double A0_Bq = ConvertToBq(A0, InitialActivityUnit);

        // ─── حساب الفرق الزمني بالثواني ───
        TimeSpan elapsed = CalculationDate - CalibrationDate;
        double elapsedSeconds = elapsed.TotalSeconds;

        // ─── تحويل نصف العمر إلى ثوانٍ ───
        double halfLifeSeconds = ConvertTimeToSeconds(halfLife, HalfLifeUnit);

        // ─── حساب ثابت التحلل: λ = ln(2) / T½ ───
        double lambda = Math.Log(2) / halfLifeSeconds;

        // ─── حساب النشاط الحالي: A(t) = A₀ × e^(-λt) ───
        double activityBq = A0_Bq * Math.Exp(-lambda * elapsedSeconds);

        // ─── حفظ وعرض النتائج ───
        ResultActivityBq = activityBq;
        DecayConstantText = FormatScientific(lambda) + " s⁻¹";
        ElapsedTimeText = FormatElapsedTime(elapsed);
        ResultText = FormatResult(activityBq, SelectedOutputUnit);
        HasResult = true;
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedIsotope = null;
        InitialActivityText = string.Empty;
        InitialActivityUnit = "Bq";
        HalfLifeValueText = string.Empty;
        HalfLifeUnit = "years";
        CalibrationDate = DateTime.Today.AddYears(-1);
        CalculationDate = DateTime.Today;
        SelectedOutputUnit = "Bq";
        ResultActivityBq = 0;
        ResultText = string.Empty;
        DecayConstantText = string.Empty;
        ElapsedTimeText = string.Empty;
        HasResult = false;
        HasError = false;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void CopyResult()
    {
        if (!string.IsNullOrEmpty(ResultText))
        {
            try { System.Windows.Clipboard.SetText(ResultText); } catch { }
        }
    }

    // ═══════════════════════════════════════════════════════
    // وظائف التحويل المساعدة
    // ═══════════════════════════════════════════════════════

    /// <summary>تحويل الوحدات الزمنية إلى ثوانٍ</summary>
    private static double ConvertTimeToSeconds(double value, string unit)
    {
        return unit.ToLower() switch
        {
            "seconds" => value,
            "minutes" => value * 60,
            "hours"   => value * 3600,
            "days"    => value * 86400,
            "months"  => value * 2_592_000,    // 30 يوماً
            "years"   => value * 31_557_600,   // 365.25 يوماً (السنة اليوليانية)
            _ => value
        };
    }

    /// <summary>تحويل النشاط من وحدة معينة إلى Bq</summary>
    private static double ConvertToBq(double value, string unit)
    {
        return unit switch
        {
            "Bq"  => value,
            "kBq" => value * 1e3,
            "MBq" => value * 1e6,
            "GBq" => value * 1e9,
            "Ci"  => value * 3.7e10,
            "mCi" => value * 3.7e7,
            "µCi" => value * 3.7e4,
            _ => value
        };
    }

    /// <summary>تحويل Bq إلى الوحدة المطلوبة</summary>
    private static double ConvertFromBq(double bq, string unit)
    {
        return unit switch
        {
            "Bq"  => bq,
            "kBq" => bq / 1e3,
            "MBq" => bq / 1e6,
            "GBq" => bq / 1e9,
            "Ci"  => bq / 3.7e10,
            "mCi" => bq / 3.7e7,
            "µCi" => bq / 3.7e4,
            _ => bq
        };
    }

    /// <summary>تنسيق النتيجة</summary>
    private static string FormatResult(double bq, string unit)
    {
        double converted = ConvertFromBq(bq, unit);
        return $"{FormatScientific(converted)} {unit}";
    }

    /// <summary>تنسيق الفترة الزمنية المنقضية</summary>
    private static string FormatElapsedTime(TimeSpan elapsed)
    {
        double totalDays = elapsed.TotalDays;
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

    /// <summary>تنسيق الأرقام بتدوين علمي أنيق</summary>
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

    /// <summary>تحويل الأرقام إلى أرقام فوقية</summary>
    private static string ToSuperscript(string number)
    {
        var map = new System.Collections.Generic.Dictionary<char, char>
        {
            {'0', '⁰'}, {'1', '¹'}, {'2', '²'}, {'3', '³'}, {'4', '⁴'},
            {'5', '⁵'}, {'6', '⁶'}, {'7', '⁷'}, {'8', '⁸'}, {'9', '⁹'}
        };
        return new string(number.Select(c => map.ContainsKey(c) ? map[c] : c).ToArray());
    }
}
