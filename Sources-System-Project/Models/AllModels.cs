using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sources.Models;

// ─── النظائر المشعة ───
public class Radioisotope
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ArabicName { get; set; }

    [Required, MaxLength(20)]
    public string Symbol { get; set; } = string.Empty;

    [MaxLength(50)]
    public string RadiationType { get; set; } = string.Empty; // Alpha, Beta, Gamma, Neutron

    public double HalfLife { get; set; }

    [NotMapped]
    public int No { get; set; }

    [MaxLength(20)]
    public string HalfLifeUnit { get; set; } = "years"; // seconds, minutes, hours, days, years

    public double Energy { get; set; } // keV

    public double? Yield { get; set; } // Probability

    public int Category { get; set; } // Regulatory Category (1-5)
    public double? ExemptionLimit { get; set; } // Bq or appropriate unit

    public double? GammaConstant { get; set; } // Specific Gamma Constant in µSv·m²/(MBq·h) at 1 meter

    [NotMapped]
    public bool IsGammaEmitter => !string.IsNullOrWhiteSpace(RadiationType) && RadiationType.Contains("Gamma", StringComparison.OrdinalIgnoreCase);

    public string? Notes { get; set; }
    public string? EnglishNotes { get; set; }
    public bool IsDeleted { get; set; } = false;
    public string? AddedBy { get; set; }

    [NotMapped]
    public string DisplayNotes
    {
        get
        {
            bool isArabic = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ar";
            if (isArabic) return !string.IsNullOrEmpty(Notes) ? Notes : EnglishNotes ?? string.Empty;
            return !string.IsNullOrEmpty(EnglishNotes) ? EnglishNotes : Notes ?? string.Empty;
        }
    }

    [NotMapped]
    public string DisplaySymbol
    {
        get
        {
            if (string.IsNullOrEmpty(Symbol)) return "";
            var parts = Symbol.Split('-');
            if (parts.Length == 2) return $"{parts[1]}-{parts[0]}";
            return Symbol;
        }
    }

    [NotMapped]
    public string DisplayName
    {
        get
        {
            bool isArabic = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ar";
            if (isArabic) return !string.IsNullOrEmpty(ArabicName) ? ArabicName : Name;
            return !string.IsNullOrEmpty(Name) ? Name : ArabicName ?? string.Empty;
        }
    }

    [NotMapped]
    public string DisplayHalfLife
    {
        get
        {
            bool isArabic = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ar";
            string unit = HalfLifeUnit.ToLower() switch
            {
                "seconds" => isArabic ? "ثانية" : "sec",
                "minutes" => isArabic ? "دقيقة" : "min",
                "hours" => isArabic ? "ساعة" : "hours",
                "days" => isArabic ? "يوم" : "days",
                "years" => isArabic ? "سنة" : "years",
                _ => HalfLifeUnit
            };

            // تنسيق تدوين علمي للفترات الطويلة جداً (أكثر من 900 سنة)
            if (HalfLifeUnit.ToLower() == "years" && HalfLife >= 900)
            {
                string scientific = HalfLife.ToString("E3"); // X.XXXE+00
                var parts = scientific.Split('E');
                if (parts.Length == 2)
                {
                    string baseNum = parts[0];
                    string exponent = parts[1].Replace("+", "").TrimStart('0');
                    if (string.IsNullOrEmpty(exponent)) exponent = "0";
                    return $"{baseNum} × 10^{exponent} {unit}";
                }
            }

            return $"{HalfLife:0.##} {unit}";
        }
    }

    // Navigation
    public ICollection<Source> Sources { get; set; } = new List<Source>();
    public ICollection<GammaLine> GammaLines { get; set; } = new List<GammaLine>();
}

// ─── خطوط غاما ───
public class GammaLine
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RadioisotopeId { get; set; }
    [ForeignKey(nameof(RadioisotopeId))]
    public Radioisotope? Radioisotope { get; set; }

    public double Energy { get; set; } // keV or MeV
    public double? Intensity { get; set; } // Yield/Probability

    public string? Notes { get; set; }
}

// ─── النظائر داخل المصدر (جدول وسيط Many-to-Many) ───
public class SourceIsotope
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceId { get; set; }
    [ForeignKey(nameof(SourceId))]
    public Source? Source { get; set; }

    public Guid RadioisotopeId { get; set; }
    [ForeignKey(nameof(RadioisotopeId))]
    public Radioisotope? Radioisotope { get; set; }

    /// <summary>النشاط الابتدائي لهذا النظير (اختياري)</summary>
    public double? InitialActivityValue { get; set; }

    /// <summary>وحدة النشاط الابتدائي</summary>
    public Guid? ActivityUnitId { get; set; }
    [ForeignKey(nameof(ActivityUnitId))]
    public ActivityUnit? ActivityUnit { get; set; }

    /// <summary>النشاط الحالي المحسوب تلقائياً</summary>
    public double? CurrentActivityValue { get; set; }

    /// <summary>تاريخ معايرة النظير (اختياري، يُورث من المصدر إذا فارغ)</summary>
    public DateTime? CalibrationDate { get; set; }

    public string? Notes { get; set; }
}

// ─── وحدات النشاط الإشعاعي ───
public class ActivityUnit
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(50)]
    public string UnitName { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string UnitSymbol { get; set; } = string.Empty;

    public double ConversionToBq { get; set; } // معامل التحويل إلى Bq

    public string? Description { get; set; }

    // Navigation
    public ICollection<Source> SourcesInitial { get; set; } = new List<Source>();
    public ICollection<Source> SourcesCurrent { get; set; } = new List<Source>();
}

// ─── المصادر المشعة ───
public class Source
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(50)]
    public string SourceCode { get; set; } = string.Empty;

    public Guid RadioisotopeId { get; set; }
    [ForeignKey(nameof(RadioisotopeId))]
    public Radioisotope? Radioisotope { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    public double InitialActivityValue { get; set; }

    public Guid InitialActivityUnitId { get; set; }
    [ForeignKey(nameof(InitialActivityUnitId))]
    public ActivityUnit? InitialActivityUnit { get; set; }

    public DateTime CalibrationDate { get; set; }

    public double CurrentActivityValue { get; set; }

    public Guid CurrentActivityUnitId { get; set; }
    [ForeignKey(nameof(CurrentActivityUnitId))]
    public ActivityUnit? CurrentActivityUnit { get; set; }

    public Guid? LocationId { get; set; }
    [ForeignKey(nameof(LocationId))]
    public Location? Location { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "InUse"; // InUse, Storage, Waste, Transfer

    /// <summary>هل المصدر يحتوي على تفاصيل متعددة النظائر؟</summary>
    public bool HasDetailedIsotopes { get; set; }

    /// <summary>هل المصدر مختوم (خاضع لاختبار التسرب الدوري)؟</summary>
    public bool IsSealed { get; set; } = true;

    public string? ImagePath { get; set; }

    public string? Notes { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    [ForeignKey(nameof(DeletedBy))]
    public User? DeletedByUser { get; set; }
    public string? AddedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public ICollection<BorrowRequest> BorrowRequests { get; set; } = new List<BorrowRequest>();
    public ICollection<SourceIsotope> SourceIsotopes { get; set; } = new List<SourceIsotope>();
    public ICollection<SourceLocationHistory> LocationHistories { get; set; } = new List<SourceLocationHistory>();
    public ICollection<LeakTestRecord> LeakTestRecords { get; set; } = new List<LeakTestRecord>();

    /// <summary>عرض النظائر كنص مختصر للجدول الرئيسي</summary>
    [NotMapped]
    public string DisplayIsotopes
    {
        get
        {
            string lre = "\u202A";
            string pdf = "\u202C";
            if (HasDetailedIsotopes && SourceIsotopes.Any())
                return lre + string.Join(" ,", SourceIsotopes.Select(si => si.Radioisotope?.Symbol ?? "")) + pdf;
            return lre + (Radioisotope?.Symbol ?? "") + pdf;
        }
    }

    /// <summary>قائمة النظائر كعناصر منفصلة للعرض في شارات</summary>
    [NotMapped]
    public List<string> DisplayIsotopesList
    {
        get
        {
            if (HasDetailedIsotopes && SourceIsotopes.Any())
                return SourceIsotopes.Select(si => si.Radioisotope?.Symbol ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
            var symbol = Radioisotope?.Symbol ?? "";
            return string.IsNullOrEmpty(symbol) ? new List<string>() : new List<string> { symbol };
        }
    }

    /// <summary>النشاط الإجمالي المحسوب (يتم تحديثه بواسطة الخدمة)</summary>
    [NotMapped]
    public double DisplayTotalActivity => CurrentActivityValue;

    [NotMapped]
    public string InitialActivityDisplay => FormatActivity(InitialActivityValue, InitialActivityUnit?.UnitSymbol);

    [NotMapped]
    public string InitialActivityWithUnit => $"{InitialActivityDisplay} {InitialActivityUnit?.UnitSymbol ?? ""}".Trim();

    [NotMapped]
    public string CurrentActivityDisplay => FormatActivity(CurrentActivityValue, CurrentActivityUnit?.UnitSymbol);

    [NotMapped]
    public string CurrentActivityWithUnit => $"{CurrentActivityDisplay} {CurrentActivityUnit?.UnitSymbol ?? ""}".Trim();

    [NotMapped]
    public string ArabicStatus => Status switch
    {
        "InUse" => "قيد الاستخدام",
        "Storage" => "مخزن",
        "Waste" => "نفايات",
        "Transfer" => "قيد النقل",
        _ => Status
    };

    [NotMapped]
    public string SimpleArabicStatus => Status switch
    {
        "InUse" => "قيد الاستخدام",
        "Storage" => "مخزن",
        "Waste" => "نفايات",
        "Transfer" => "قيد النقل",
        _ => Status
    };

    [NotMapped]
    public string? AlertSeverity { get; set; } // "Critical" or "Warning"

    [NotMapped]
    public string? AlertWorstIsotope { get; set; }

    [NotMapped]
    public double? AlertHalfLivesElapsed { get; set; }

    [NotMapped]
    public string AlertSeverityDisplay => AlertSeverity switch
    {
        "Critical" => "حرج",
        "Warning" => "تحذير",
        _ => AlertSeverity ?? "-"
    };

    /// <summary>كود المصدر مع حالة الحذف إن وُجد</summary>
    [NotMapped]
    public string DisplaySourceCode => IsDeleted ? $"{SourceCode} (محذوف)" : SourceCode;

    private DoseRateResult? _currentDoseRateResult;

    /// <summary>نتيجة حساب معدل الجرعة الإشعاعية التقديري عند 1 متر</summary>
    [NotMapped]
    public DoseRateResult? CurrentDoseRateResult
    {
        get => _currentDoseRateResult ?? ComputeLazyDoseRate();
        set => _currentDoseRateResult = value;
    }

    /// <summary>معدل الجرعة الإشعاعية التقديري عند 1 متر للعرض في الجداول والبطاقات</summary>
    [NotMapped]
    public string DisplayDoseRate => CurrentDoseRateResult?.FormattedSummary ?? "-";

    /// <summary>تفاصيل معدل الجرعة للتلميح (Tooltip)</summary>
    [NotMapped]
    public string DoseRateTooltip => CurrentDoseRateResult?.TooltipText ?? string.Empty;

    private DoseRateResult ComputeLazyDoseRate()
    {
        var result = new DoseRateResult();
        var list = new List<(Radioisotope Isotope, double ActivityMBq)>();

        if (HasDetailedIsotopes && SourceIsotopes != null && SourceIsotopes.Any())
        {
            foreach (var si in SourceIsotopes)
            {
                if (si.Radioisotope == null) continue;
                double conv = si.ActivityUnit?.ConversionToBq ?? InitialActivityUnit?.ConversionToBq ?? 1;
                double actBq = (si.CurrentActivityValue ?? 0) * conv;
                list.Add((si.Radioisotope, actBq / 1e6));
            }
        }
        else if (Radioisotope != null)
        {
            double conv = CurrentActivityUnit?.ConversionToBq ?? InitialActivityUnit?.ConversionToBq ?? 1;
            double actBq = CurrentActivityValue * conv;
            list.Add((Radioisotope, actBq / 1e6));
        }

        if (list.Count == 0) return result;

        double totalDoseRate = 0;
        foreach (var (isotope, activityMBq) in list)
        {
            var contribution = new DoseRateIsotopeContribution
            {
                Isotope = isotope,
                ActivityMBq = activityMBq,
                GammaConstant = isotope.GammaConstant
            };

            if (!isotope.IsGammaEmitter)
            {
                contribution.Status = DoseRateContributionStatus.NonGammaEmitter;
                contribution.ContributionMicroSvPerHour = 0;
                contribution.StatusText = "غير مساهم عند هذه المسافة";
            }
            else if (!isotope.GammaConstant.HasValue || isotope.GammaConstant.Value <= 0)
            {
                contribution.Status = DoseRateContributionStatus.MissingGammaConstant;
                contribution.ContributionMicroSvPerHour = 0;
                contribution.StatusText = "بيانات ثابت غاما غير مسجلة";
            }
            else
            {
                contribution.Status = DoseRateContributionStatus.Contributing;
                double dose = activityMBq * isotope.GammaConstant.Value;
                contribution.ContributionMicroSvPerHour = dose;
                contribution.StatusText = $"{dose:0.####} µSv/h";
                totalDoseRate += dose;
            }
            result.Contributions.Add(contribution);
        }

        result.TotalDoseRateMicroSvPerHour = totalDoseRate;
        return result;
    }

    private string FormatActivity(double value, string? unitSymbol)
    {
        // عرض القيمة فقط بدون الرمز لتجنب التكرار في الجدول
        if (value == 0) return "0";
        
        // التدوين العلمي للأرقام الكبيرة جداً أو الصغيرة جداً
        if (Math.Abs(value) >= 1e7 || Math.Abs(value) < 0.0001)
            return value.ToString("E2");

        // إذا كان الرقم صحيحاً لا نعرض اصفار، وإذا كان عشرياً نعرض رقمين
        return (value % 1 == 0) ? value.ToString("#,##0") : value.ToString("#,##0.00");
    }
}

// ─── نماذج حساب معدل الجرعة الإشعاعية عند 1 متر ───
public enum DoseRateContributionStatus
{
    Contributing,           // نظير باعث لجاما ولديه ثابت جاما مسجل
    NonGammaEmitter,        // نظير باعث لألفا أو بيتا فقط (غير مساهم عند 1م)
    MissingGammaConstant    // نظير باعث لجاما لكن ثابت جاما غير مسجل
}

public class DoseRateIsotopeContribution
{
    public Radioisotope Isotope { get; set; } = null!;
    public double ActivityMBq { get; set; }
    public double? GammaConstant { get; set; }
    public double ContributionMicroSvPerHour { get; set; }
    public DoseRateContributionStatus Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
}

public class DoseRateResult
{
    /// <summary>إجمالي معدل الجرعة بوحدة µSv/h</summary>
    public double TotalDoseRateMicroSvPerHour { get; set; }

    /// <summary>إجمالي معدل الجرعة المكافئ بوحدة mR/h (1 µSv/h ≈ 0.115 mR/h)</summary>
    public double TotalDoseRatemRPerHour => TotalDoseRateMicroSvPerHour * 0.115;

    /// <summary>إجمالي معدل الجرعة المكافئ بوحدة mrem/h (1 µSv/h = 0.1 mrem/h)</summary>
    public double TotalDoseRatemremPerHour => TotalDoseRateMicroSvPerHour * 0.1;

    /// <summary>تفصيل مساهمة كل نظير</summary>
    public List<DoseRateIsotopeContribution> Contributions { get; set; } = new();

    public bool HasContributingIsotopes => Contributions.Any(c => c.Status == DoseRateContributionStatus.Contributing);
    public bool HasMissingData => Contributions.Any(c => c.Status == DoseRateContributionStatus.MissingGammaConstant);
    public bool IsAllNonGamma => Contributions.Count > 0 && Contributions.All(c => c.Status == DoseRateContributionStatus.NonGammaEmitter);

    public string FormattedTotalMicroSv
    {
        get
        {
            if (TotalDoseRateMicroSvPerHour == 0) return "0 µSv/h";
            if (TotalDoseRateMicroSvPerHour >= 1000)
                return $"{(TotalDoseRateMicroSvPerHour / 1000):0.##} mSv/h";
            if (TotalDoseRateMicroSvPerHour >= 0.1)
                return $"{TotalDoseRateMicroSvPerHour:0.##} µSv/h";
            return $"{TotalDoseRateMicroSvPerHour:0.####} µSv/h";
        }
    }

    public string FormattedSummary
    {
        get
        {
            if (Contributions.Count == 0) return "-";
            if (HasContributingIsotopes)
            {
                string baseText;
                if (TotalDoseRateMicroSvPerHour == 0)
                    baseText = "0 µSv/h @ 1m";
                else if (TotalDoseRateMicroSvPerHour >= 1000)
                    baseText = $"{(TotalDoseRateMicroSvPerHour / 1000):0.##} mSv/h @ 1m";
                else if (TotalDoseRateMicroSvPerHour >= 0.1)
                    baseText = $"{TotalDoseRateMicroSvPerHour:0.##} µSv/h @ 1m";
                else
                    baseText = $"{TotalDoseRateMicroSvPerHour:0.####} µSv/h @ 1m";

                if (HasMissingData)
                    return $"{baseText} (*)";
                return baseText;
            }
            if (IsAllNonGamma)
                return "N/A (α/β)";
            if (HasMissingData)
                return "N/A (بيانات غير مسجلة)";
            return "0 µSv/h @ 1m";
        }
    }

    public string TooltipText
    {
        get
        {
            if (Contributions.Count == 0) return "لا توجد بيانات للنظائر";
            var lines = new List<string>();
            lines.Add("معدل الجرعة التقديري عند 1 متر في الهواء:");
            if (HasContributingIsotopes)
            {
                lines.Add($"• الإجمالي: {TotalDoseRateMicroSvPerHour:0.####} µSv/h ({TotalDoseRatemRPerHour:0.####} mR/h | {TotalDoseRatemremPerHour:0.####} mrem/h)");
            }
            else if (IsAllNonGamma)
            {
                lines.Add("• غير مؤثر عند 1 متر (أشعة ألفا/بيتا فقط ممتصة بغلاف المصدر والهواء)");
            }
            else if (HasMissingData)
            {
                lines.Add("• بيانات ثابت غاما غير مسجلة للنظير");
            }

            if (Contributions.Count > 1 || (Contributions.Count == 1 && HasContributingIsotopes))
            {
                lines.Add("تفصيل النظائر:");
                foreach (var c in Contributions)
                {
                    string sym = c.Isotope?.Symbol ?? "نظير";
                    if (c.Status == DoseRateContributionStatus.Contributing)
                    {
                        lines.Add($"  - {sym}: {c.ContributionMicroSvPerHour:0.####} µSv/h (Γ = {c.GammaConstant:0.####})");
                    }
                    else if (c.Status == DoseRateContributionStatus.NonGammaEmitter)
                    {
                        lines.Add($"  - {sym}: غير مساهم عند 1م (أشعة {c.Isotope?.RadiationType ?? "ألفا/بيتا"} فقط)");
                    }
                    else if (c.Status == DoseRateContributionStatus.MissingGammaConstant)
                    {
                        lines.Add($"  - {sym}: بيانات ثابت غاما غير مسجلة");
                    }
                }
            }
            return string.Join("\n", lines);
        }
    }
}

// ─── المواقع ───
public class Location
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string LocationName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? LocationType { get; set; } // Lab, Storage, Hospital, Clinic

    [MaxLength(100)]
    public string? Building { get; set; }

    [MaxLength(50)]
    public string? Room { get; set; }

    [MaxLength(100)]
    public string? ResponsiblePerson { get; set; }
    public bool IsDeleted { get; set; } = false;
    public string? AddedBy { get; set; }

    /// <summary>عدد المصادر المرتبطة حالياً بهذا الموقع</summary>
    [NotMapped]
    public int SourceCount { get; set; }

    // Navigation
    public ICollection<Source> Sources { get; set; } = new List<Source>();
}

// ─── سجل تاريخ تنقلات المصادر بين المواقع ───
public class SourceLocationHistory
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceId { get; set; }
    [ForeignKey(nameof(SourceId))]
    public Source? Source { get; set; }

    public Guid? LocationId { get; set; }
    [ForeignKey(nameof(LocationId))]
    public Location? Location { get; set; }

    public Guid? PreviousLocationId { get; set; }
    [ForeignKey(nameof(PreviousLocationId))]
    public Location? PreviousLocation { get; set; }

    public DateTime MovedAt { get; set; } = DateTime.Now;
}

// ─── استعارة المصادر ───
public class BorrowRequest
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>المصدر المطلوب استعارته</summary>
    public Guid SourceId { get; set; }
    [ForeignKey(nameof(SourceId))]
    public Source? Source { get; set; }

    /// <summary>اسم المستعير (نص حر)</summary>
    [MaxLength(200)]
    public string BorrowerName { get; set; } = string.Empty;

    /// <summary>المستعير (اختياري - للربط مع مستخدم نظام)</summary>
    public Guid? BorrowerUserId { get; set; }
    [ForeignKey(nameof(BorrowerUserId))]
    public User? BorrowerUser { get; set; }

    /// <summary>المسؤول الذي وافق أو رفض</summary>
    public Guid? ApproverUserId { get; set; }
    [ForeignKey(nameof(ApproverUserId))]
    public User? ApproverUser { get; set; }

    /// <summary>المستخدم الذي قام بإرجاع المصدر</summary>
    public Guid? ReturnedByUserId { get; set; }
    [ForeignKey(nameof(ReturnedByUserId))]
    public User? ReturnedByUser { get; set; }

    /// <summary>الغرض من الاستعارة</summary>
    [Required, MaxLength(500)]
    public string Purpose { get; set; } = string.Empty;

    public DateTime RequestDate { get; set; } = DateTime.Now;
    public DateTime ExpectedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Pending, Approved, Rejected, Delivered, Returned, Overdue</summary>
    [Required, MaxLength(30)]
    public string Status { get; set; } = "Pending";

    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }
    public string? AddedBy { get; set; }

    /// <summary>الحالة بالعربية</summary>
    [NotMapped]
    public string ArabicStatus => Status switch
    {
        "Pending" => "معلّق",
        "Approved" => "تمت الموافقة",
        "Rejected" => "مرفوض",
        "Delivered" => "تم التسليم",
        "Returned" => "تم الإرجاع",
        "Overdue" => "متأخر",
        _ => Status
    };

    /// <summary>اسم المستعير للعرض (يدعم المستخدم المسجل أو الاسم الحر)</summary>
    [NotMapped]
    public string DisplayBorrowerName => !string.IsNullOrWhiteSpace(BorrowerName)
        ? BorrowerName
        : (BorrowerUser?.FullName ?? "-");

    /// <summary>كود المصدر للعرض مع تمييز المصادر المحذوفة</summary>
    [NotMapped]
    public string DisplaySourceCode => Source != null
        ? (Source.IsDeleted ? $"{Source.SourceCode} (محذوف)" : Source.SourceCode)
        : "-";
}

// ─── المستخدمين ───
public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public Guid RoleId { get; set; }
    [ForeignKey(nameof(RoleId))]
    public Role? Role { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // ─── حقول الأمان (RBAC) ───
    /// <summary>عدد محاولات تسجيل الدخول الفاشلة</summary>
    public int FailedLoginAttempts { get; set; } = 0;

    /// <summary>تاريخ انتهاء قفل الحساب</summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>تاريخ آخر تسجيل دخول ناجح</summary>
    public DateTime? LastLoginDate { get; set; }

    // ─── صلاحيات تفصيلية ───
    /// <summary>صلاحيات الوصول لأقسام المنظومة (نص بفواصل): مثل "Sources,Reports,Locations"</summary>
    public string? Permissions { get; set; }

    /// <summary>هل يملك صلاحية تعديل البيانات (true) أم عرض فقط (false)</summary>
    public bool IsEditor { get; set; } = true;

    // ─── خصائص محسوبة ───
    [NotMapped]
    public string StatusDisplayName => IsActive ? "نشط" : "موقوف";

    [NotMapped]
    public bool IsLocked => LockoutEnd.HasValue && LockoutEnd.Value > DateTime.Now;

    /// <summary>التحقق من صلاحية الوصول لقسم معين</summary>
    [NotMapped]
    public bool IsAdmin => Role?.RoleName == "مدير النظام";

    public bool HasSectionPermission(string section)
    {
        // المدير لديه كل الصلاحيات
        if (IsAdmin) return true;
        if (string.IsNullOrEmpty(Permissions)) return false;
        if (string.Equals(Permissions.Trim(), "All", StringComparison.OrdinalIgnoreCase)) return true;
        return Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries)
                         .Any(p => p.Trim().Equals(section, StringComparison.OrdinalIgnoreCase));
    }

    // Navigation
    public ICollection<BorrowRequest> BorrowRequestsAsBorrower { get; set; } = new List<BorrowRequest>();
    public ICollection<BorrowRequest> BorrowRequestsAsApprover { get; set; } = new List<BorrowRequest>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

// ─── الصلاحيات ───
public class Role
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(50)]
    public string RoleName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>صلاحيات الدور (JSON): مثل ["ManageSources","ViewReports","ManageUsers"]</summary>
    public string? Permissions { get; set; }

    [NotMapped]
    public string DisplayName => Sources.Helpers.TranslationHelper.GetString(RoleName == "مدير النظام" ? "RoleAdmin" : "RoleUser");

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
}

// ─── سجل التدقيق ───
public class AuditLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required, MaxLength(50)]
    public string Action { get; set; } = string.Empty; // Create, Update, Delete, Login, Logout

    [MaxLength(50)]
    public string? TableName { get; set; }

    public Guid? RecordId { get; set; }

    public DateTime ActionDate { get; set; } = DateTime.Now;

    public string? Details { get; set; }

    /// <summary>القيم السابقة قبل التعديل (JSON)</summary>
    public string? OldValues { get; set; }

    /// <summary>القيم الجديدة بعد التعديل (JSON)</summary>
    public string? NewValues { get; set; }
}

// ─── التنبيهات الذكية ───
public class AlertNotification
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(50)]
    public string AlertType { get; set; } = string.Empty; // CalibrationDue, HighDecay, LowActivity, Expiring

    [Required, MaxLength(20)]
    public string Severity { get; set; } = "Warning"; // Critical, Warning, Info

    [Required]
    public string Message { get; set; } = string.Empty;

    public Guid? SourceId { get; set; }
    [ForeignKey(nameof(SourceId))]
    public Source? Source { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsRead { get; set; } = false;

    public bool IsDismissed { get; set; } = false;
}

// ─── إعدادات النظام ───
public class AppSetting
{
    [Key]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }
}

// ─── اختبارات التسرب الدوري والمسح الإشعاعي (Leak/Wipe Test) ───
public class LeakTestRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceId { get; set; }
    [ForeignKey(nameof(SourceId))]
    public Source? Source { get; set; }

    public DateTime TestDate { get; set; } = DateTime.Today;

    public DateTime NextDueDate { get; set; }

    [Required, MaxLength(20)]
    public string Result { get; set; } = "Pass"; // Pass, Fail, Inconclusive

    public double? MeasuredActivityBq { get; set; }

    public Guid? PerformedByUserId { get; set; }
    [ForeignKey(nameof(PerformedByUserId))]
    public User? PerformedByUser { get; set; }

    [MaxLength(200)]
    public string? InspectorName { get; set; }

    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [NotMapped]
    public string ArabicResult => Result switch
    {
        "Pass" => "ناجح (سليم)",
        "Fail" => "راسب (تسرب)",
        "Inconclusive" => "غير حاسم",
        _ => Result
    };

    [NotMapped]
    public string StatusDisplay => (NextDueDate.Date < DateTime.Today) ? "متأخر" : "ساري";
}

