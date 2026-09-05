using System;
using Sources.Models;

namespace Sources.Services;

/// <summary>
/// حالة نتيجة حساب الاضمحلال النيتروني
/// </summary>
public enum NeutronDecayCalculationStatus
{
    Calculated,
    MissingCalibrationDate,
    MissingSourceType,
    MissingSource,
    InvalidHalfLife,
    UnsupportedHalfLifeUnit,
    CalculationDatePrecedesCalibrationDate,
    InvalidCalibratedRate,

    /// <summary>Am241ActivityValue و/أو Am241ActivityUnitId غير مُدخلين على المصدر — حالة
    /// طبيعية «غير مُسجَّل» وليست خطأ.</summary>
    NotRecorded,

    /// <summary>Am241ActivityUnitId له قيمة لكن خاصية التنقل Am241ActivityUnit لم تُحمَّل من
    /// قِبل المستدعي — يُماثل نمط MissingSourceType تماماً.</summary>
    MissingActivityUnit,

    /// <summary>قيمة النشاط بعد التحويل إلى Bq غير منتهية (IsFinite) أو <= 0 — فحص احترازي
    /// يُماثل InvalidCalibratedRate.</summary>
    InvalidActivityValue
}

/// <summary>
/// كائن نتيجة حساب الاضمحلال لمصدر نيتروني
/// </summary>
public class NeutronDecayResult
{
    public bool IsCalculated => Status == NeutronDecayCalculationStatus.Calculated;
    public NeutronDecayCalculationStatus Status { get; set; }
    public double? CurrentEmissionRate { get; set; }

    /// <summary>نشاط الأمريسيوم-241 الحالي بوحدة Bq بعد الاضمحلال من CalibrationDate — لا
    /// علاقة له بـ CurrentEmissionRate (n/s)، وهما كميتان مستقلتان.</summary>
    public double? CurrentActivityBq { get; set; }
}

/// <summary>
/// واجهة خدمة حساب الاضمحلال الإشعاعي للمصادر النيترونية
/// B(t) = B₀ × exp(-ln(2) × Δt / T½)
/// </summary>
public interface INeutronDecayCalculationService
{
    /// <summary>
    /// حساب معدل الانبعاث النيتروني الحالي للمصدر (عند اللحظة الحالية)
    /// </summary>
    NeutronDecayResult CalculateCurrentEmissionRate(NeutronSource? source);

    /// <summary>
    /// حساب معدل الانبعاث النيتروني للمصدر عند تاريخ حساب محدد
    /// </summary>
    NeutronDecayResult CalculateEmissionRateAtDate(NeutronSource? source, DateTime calculationDate);

    /// <summary>
    /// حساب معدل الانبعاث النيتروني بالقيم والمعاملات المباشرة
    /// </summary>
    NeutronDecayResult CalculateEmissionRate(
        double calibratedEmissionRate,
        double halfLife,
        string? halfLifeUnit,
        DateTime? emissionCalibrationDate,
        DateTime calculationDate);

    /// <summary>
    /// حساب نشاط الأمريسيوم-241 الحالي للمصدر (عند اللحظة الحالية)
    /// </summary>
    NeutronDecayResult CalculateCurrentAm241Activity(NeutronSource? source);

    /// <summary>
    /// حساب نشاط الأمريسيوم-241 للمصدر عند تاريخ حساب محدد، بالاعتماد على CalibrationDate
    /// كتاريخ مرجعي وعمر النصف الثابت 432.2 سنة
    /// </summary>
    NeutronDecayResult CalculateAm241ActivityAtDate(NeutronSource? source, DateTime calculationDate);
}
