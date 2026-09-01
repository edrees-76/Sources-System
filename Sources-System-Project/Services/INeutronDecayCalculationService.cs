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
    InvalidHalfLife,
    UnsupportedHalfLifeUnit,
    CalculationDatePrecedesCalibrationDate,
    InvalidCalibratedRate
}

/// <summary>
/// كائن نتيجة حساب الاضمحلال لمصدر نيتروني
/// </summary>
public class NeutronDecayResult
{
    public bool IsCalculated => Status == NeutronDecayCalculationStatus.Calculated;
    public NeutronDecayCalculationStatus Status { get; set; }
    public double? CurrentEmissionRate { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string DisplayRate { get; set; } = string.Empty;
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
}
