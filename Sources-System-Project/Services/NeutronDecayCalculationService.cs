using System;
using Sources.Helpers;
using Sources.Models;

namespace Sources.Services;

/// <summary>
/// خدمة حساب الاضمحلال الإشعاعي للمصادر النيترونية
/// المعادلة المعتمدة: B(t) = B₀ × exp(-ln(2) × Δt / T½)
/// </summary>
public class NeutronDecayCalculationService : INeutronDecayCalculationService
{
    // الثابت المعتمد لطول السنة المدارية/الاستوائية بالأيام
    public const double DaysPerYear = 365.2422;
    public const double SecondsPerDay = 86400.0;
    public const double SecondsPerYear = DaysPerYear * SecondsPerDay;

    /// <summary>
    /// حساب معدل الانبعاث النيتروني الحالي للمصدر (عند اللحظة الحالية)
    /// </summary>
    public NeutronDecayResult CalculateCurrentEmissionRate(NeutronSource? source)
    {
        return CalculateEmissionRateAtDate(source, DateTime.Now);
    }

    /// <summary>
    /// حساب معدل الانبعاث النيتروني للمصدر عند تاريخ حساب محدد
    /// </summary>
    public NeutronDecayResult CalculateEmissionRateAtDate(NeutronSource? source, DateTime calculationDate)
    {
        if (source == null)
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.MissingSourceType,
                StatusText = "غير محسوب — بيانات المصدر غير متوفرة",
                CurrentEmissionRate = null,
                DisplayRate = "غير محسوب — بيانات المصدر غير متوفرة"
            };
        }

        if (source.NeutronSourceType == null)
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.MissingSourceType,
                StatusText = "غير محسوب — بيانات نوع المصدر غير متوفرة",
                CurrentEmissionRate = null,
                DisplayRate = "غير محسوب — بيانات نوع المصدر غير متوفرة"
            };
        }

        return CalculateEmissionRate(
            source.CalibratedEmissionRate,
            source.NeutronSourceType.HalfLife,
            source.NeutronSourceType.HalfLifeUnit,
            source.EmissionCalibrationDate,
            calculationDate);
    }

    /// <summary>
    /// حساب معدل الانبعاث النيتروني بالقيم والمعاملات المباشرة
    /// </summary>
    public NeutronDecayResult CalculateEmissionRate(
        double calibratedEmissionRate,
        double halfLife,
        string? halfLifeUnit,
        DateTime? emissionCalibrationDate,
        DateTime calculationDate)
    {
        // 1. التحقق من تاريخ المعايرة
        if (!emissionCalibrationDate.HasValue)
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.MissingCalibrationDate,
                StatusText = "غير محسوب — تاريخ المعايرة غير مسجّل",
                CurrentEmissionRate = null,
                DisplayRate = "غير محسوب — تاريخ المعايرة غير مسجّل"
            };
        }

        // 2. التحقق من تسلسل التاريخ (لا اضمحلال عكسي)
        if (calculationDate < emissionCalibrationDate.Value)
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.CalculationDatePrecedesCalibrationDate,
                StatusText = "غير محسوب — تاريخ الحساب يسبق تاريخ المعايرة",
                CurrentEmissionRate = null,
                DisplayRate = "غير محسوب — تاريخ الحساب يسبق تاريخ المعايرة"
            };
        }

        // 3. التحقق من صحة معدل الانبعاث المعاير
        if (calibratedEmissionRate <= 0)
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.InvalidCalibratedRate,
                StatusText = "غير محسوب — معدل الانبعاث المُعاير غير صالح",
                CurrentEmissionRate = null,
                DisplayRate = "غير محسوب — معدل الانبعاث المُعاير غير صالح"
            };
        }

        // 4. التحقق من نصف العمر
        if (halfLife <= 0 || double.IsNaN(halfLife) || double.IsInfinity(halfLife))
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.InvalidHalfLife,
                StatusText = "غير محسوب — نصف العمر غير صالح",
                CurrentEmissionRate = null,
                DisplayRate = "غير محسوب — نصف العمر غير صالح"
            };
        }

        // 5. تحويل نصف العمر إلى ثوانٍ بناءً على الوحدة المعتمدة
        if (!TryConvertToSeconds(halfLife, halfLifeUnit, out double halfLifeSeconds))
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.UnsupportedHalfLifeUnit,
                StatusText = "غير محسوب — وحدة نصف العمر غير مدعومة",
                CurrentEmissionRate = null,
                DisplayRate = "غير محسوب — وحدة نصف العمر غير مدعومة"
            };
        }

        // 6. حساب الزمن المنقضي بالثواني
        double elapsedSeconds = (calculationDate - emissionCalibrationDate.Value).TotalSeconds;

        // 7. تطبيق معادلة الاضمحلال: B(t) = B₀ × exp(-ln(2) × Δt / T½)
        double decayExponent = -Math.Log(2.0) * (elapsedSeconds / halfLifeSeconds);
        double currentRate = calibratedEmissionRate * Math.Exp(decayExponent);

        return new NeutronDecayResult
        {
            Status = NeutronDecayCalculationStatus.Calculated,
            StatusText = "محسوب",
            CurrentEmissionRate = currentRate,
            DisplayRate = $"{ScientificNotationParser.FormatScientific(currentRate)} n/s"
        };
    }

    /// <summary>
    /// تحويل نصف العمر إلى ثوانٍ مع دعم وحدتي years و days فقط
    /// </summary>
    private static bool TryConvertToSeconds(double halfLifeValue, string? unit, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(unit)) return false;

        var normalizedUnit = unit.Trim().ToLowerInvariant();
        switch (normalizedUnit)
        {
            case "years" or "year" or "yr" or "y":
                seconds = halfLifeValue * SecondsPerYear;
                return true;

            case "days" or "day" or "d":
                seconds = halfLifeValue * SecondsPerDay;
                return true;

            default:
                return false;
        }
    }
}
