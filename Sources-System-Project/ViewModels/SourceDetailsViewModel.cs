using CommunityToolkit.Mvvm.ComponentModel;
using Sources.Helpers;
using Sources.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Sources.ViewModels;

/// <summary>
/// عنصر عرض النظير والنشاط في بطاقة النظائر
/// </summary>
public class SourceDetailsIsotopeItem
{
    public string Symbol { get; set; } = string.Empty;
    public string ActivityDisplay { get; set; } = string.Empty;
    public string UnitSymbol { get; set; } = string.Empty;
}

/// <summary>
/// عنصر عرض مساهمة النظير في معدل الجرعة الإشعاعية
/// </summary>
public class SourceDetailsDoseContributionItem
{
    public string Symbol { get; set; } = string.Empty;
    public string ContributionDisplay { get; set; } = string.Empty;
    public string GammaConstantDisplay { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public bool IsContributing { get; set; }
    public bool IsWarning { get; set; }
}

/// <summary>
/// ViewModel مخصص لنافذة تفاصيل المصدر المستقلة
/// </summary>
public partial class SourceDetailsViewModel : ObservableObject
{
    public Source Source { get; }

    // ── 1. الرأس (Header) ──
    public string SourceCode => Source.SourceCode;
    public string DisplaySourceCode => Source.DisplaySourceCode;
    public string Status => Source.Status;
    public string ArabicStatus => Source.ArabicStatus;
    public string StatusColor => Source.Status switch
    {
        "InUse" or "Active" => "#3FAE7A", // أخضر تشغيلي (Success)
        "Storage" => "#4F7FA3",           // أزرق تخزين (Info)
        "Waste" => "#E0A93E",             // كهرماني نفايات (Warning)
        "Transfer" => "#E0A93E",          // كهرماني نقل (Warning)
        _ => "#1F5A66"
    };

    public string? ImagePath { get; }
    public bool HasImage { get; }

    // ── 2. بطاقة الهوية (Identity Card) ──
    public string SerialNumber => string.IsNullOrWhiteSpace(Source.SerialNumber) ? "—" : Source.SerialNumber;
    public string LocationName => Source.Location?.LocationName ?? "—";
    public string CalibrationDateDisplay => Source.CalibrationDate.ToString("yyyy-MM-dd");
    public string Manufacturer => string.IsNullOrWhiteSpace(Source.Manufacturer) ? "—" : Source.Manufacturer;
    public string SourceTypeDisplay => Source.IsSealed ? "مصدر مختوم" : "غير مختوم";

    // ── 3. بطاقة النظائر والنشاط (Isotopes & Activity Card) ──
    public ObservableCollection<SourceDetailsIsotopeItem> Isotopes { get; } = new();

    // ── 4. بطاقة معدل الجرعة الإشعاعية (Dose Rate Card) ──
    public string DisplayDoseRate => Source.DisplayDoseRate;
    public string EquivalentDoseRatesDisplay { get; }
    public bool HasContributingIsotopes { get; }
    public bool HasDoseRateWarning { get; }
    public string? DoseRateWarningText { get; }
    public ObservableCollection<SourceDetailsDoseContributionItem> DoseRateContributions { get; } = new();
    public bool HasDoseRateContributions => DoseRateContributions.Count > 0;

    // ── 5. بطاقة الملاحظات (Notes Card) ──
    public string? Notes => Source.Notes;
    public bool HasNotes => !string.IsNullOrWhiteSpace(Source.Notes) && Source.Notes.Trim() != "N/A" && Source.Notes.Trim() != "—";

    public SourceDetailsViewModel(Source source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));

        // معالجة صورة المصدر
        if (!string.IsNullOrWhiteSpace(source.ImagePath))
        {
            try
            {
                string fullPath = Path.IsPathRooted(source.ImagePath)
                    ? source.ImagePath
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, source.ImagePath);

                if (File.Exists(fullPath))
                {
                    ImagePath = fullPath;
                    HasImage = true;
                }
            }
            catch
            {
                HasImage = false;
            }
        }

        // إعداد قائمة النظائر والنشاط الإشعاعي
        if (source.HasDetailedIsotopes && source.SourceIsotopes != null && source.SourceIsotopes.Any())
        {
            foreach (var si in source.SourceIsotopes)
            {
                string unit = si.ActivityUnit?.UnitSymbol
                           ?? source.InitialActivityUnit?.UnitSymbol
                           ?? "Bq";

                double val = si.CurrentActivityValue ?? 0;
                Isotopes.Add(new SourceDetailsIsotopeItem
                {
                    Symbol = si.Radioisotope?.Symbol ?? "—",
                    ActivityDisplay = FormatActivity(val),
                    UnitSymbol = unit
                });
            }
        }
        else
        {
            string unit = source.CurrentActivityUnit?.UnitSymbol
                       ?? source.InitialActivityUnit?.UnitSymbol
                       ?? "Bq";

            Isotopes.Add(new SourceDetailsIsotopeItem
            {
                Symbol = source.Radioisotope?.Symbol ?? "—",
                ActivityDisplay = FormatActivity(source.CurrentActivityValue),
                UnitSymbol = unit
            });
        }

        // إعداد بيانات معدل الجرعة الإشعاعية التقديري عند 1 متر
        var doseResult = source.CurrentDoseRateResult;
        if (doseResult != null)
        {
            HasContributingIsotopes = doseResult.HasContributingIsotopes;
            if (doseResult.HasContributingIsotopes)
            {
                EquivalentDoseRatesDisplay = $"{doseResult.TotalDoseRatemRPerHour:N4} mR/h  |  {doseResult.TotalDoseRatemremPerHour:N4} mrem/h";
            }
            else
            {
                EquivalentDoseRatesDisplay = string.Empty;
            }

            if (doseResult.IsAllNonGamma)
            {
                HasDoseRateWarning = true;
                DoseRateWarningText = "غير مؤثر عند هذه المسافة (أشعة ألفا/بيتا فقط ممتصة بغلاف المصدر والهواء)";
            }
            else if (doseResult.HasMissingData)
            {
                HasDoseRateWarning = true;
                DoseRateWarningText = "بيانات ثابت غاما غير مسجلة للنظير";
            }
            else if (!doseResult.HasContributingIsotopes && doseResult.Contributions.Count > 0)
            {
                HasDoseRateWarning = true;
                DoseRateWarningText = "لا توجد مساهمة إشعاعية غاما عند 1 متر";
            }

            foreach (var c in doseResult.Contributions)
            {
                string sym = c.Isotope?.Symbol ?? "—";
                string gammaStr = c.GammaConstant.HasValue && c.GammaConstant.Value > 0
                    ? $"Γ = {c.GammaConstant.Value:0.####}"
                    : "—";

                string contribStr = c.Status == DoseRateContributionStatus.Contributing
                    ? $"{c.ContributionMicroSvPerHour:N4} µSv/h"
                    : "—";

                string statusDesc = c.Status switch
                {
                    DoseRateContributionStatus.Contributing => "مساهم",
                    DoseRateContributionStatus.NonGammaEmitter => $"غير مساهم ({c.Isotope?.RadiationType ?? "α/β"})",
                    DoseRateContributionStatus.MissingGammaConstant => "بيانات Γ غير مسجلة",
                    _ => c.StatusText
                };

                DoseRateContributions.Add(new SourceDetailsDoseContributionItem
                {
                    Symbol = sym,
                    GammaConstantDisplay = gammaStr,
                    ContributionDisplay = contribStr,
                    StatusText = statusDesc,
                    IsContributing = c.Status == DoseRateContributionStatus.Contributing,
                    IsWarning = c.Status != DoseRateContributionStatus.Contributing
                });
            }
        }
        else
        {
            EquivalentDoseRatesDisplay = string.Empty;
        }
    }

    private static string FormatActivity(double value)
    {
        if (value == 0) return "0";
        if (Math.Abs(value) >= 1e7 || Math.Abs(value) < 0.0001)
            return value.ToString("E4");
        return (value % 1 == 0) ? value.ToString("#,##0") : value.ToString("#,##0.0000");
    }
}
