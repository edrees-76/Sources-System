using System;
using Sources.Models;

namespace Sources.Services;

/// <summary>
/// خدمة حساب الاضمحلال الإشعاعي للمصادر النيترونية
/// المعادلة المعتمدة: B(t) = B₀ × exp(-ln(2) × Δt / T½)
/// </summary>
public class NeutronDecayCalculationService : INeutronDecayCalculationService
{
    // الثابت المعتمد لطول السنة بالأيام (365.2422 يوماً)
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
                Status = NeutronDecayCalculationStatus.MissingSource,
                CurrentEmissionRate = null
            };
        }

        if (source.NeutronSourceType == null)
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.MissingSourceType,
                CurrentEmissionRate = null
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
                CurrentEmissionRate = null
            };
        }

        // 2. التحقق من تسلسل التاريخ (لا اضمحلال عكسي)
        if (calculationDate < emissionCalibrationDate.Value)
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.CalculationDatePrecedesCalibrationDate,
                CurrentEmissionRate = null
            };
        }

        // 3. التحقق من صحة معدل الانبعاث المعاير
        if (calibratedEmissionRate <= 0)
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.InvalidCalibratedRate,
                CurrentEmissionRate = null
            };
        }

        // 4. التحقق من نصف العمر
        if (halfLife <= 0 || double.IsNaN(halfLife) || double.IsInfinity(halfLife))
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.InvalidHalfLife,
                CurrentEmissionRate = null
            };
        }

        // 5. تحويل نصف العمر إلى ثوانٍ بناءً على الوحدة المعتمدة
        if (!TryConvertToSeconds(halfLife, halfLifeUnit, out double halfLifeSeconds))
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.UnsupportedHalfLifeUnit,
                CurrentEmissionRate = null
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
            CurrentEmissionRate = currentRate
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

    /// <summary>
    /// حساب نشاط الأمريسيوم-241 الحالي للمصدر (عند اللحظة الحالية)
    /// </summary>
    public NeutronDecayResult CalculateCurrentAm241Activity(NeutronSource? source)
        => CalculateAm241ActivityAtDate(source, DateTime.Now);

    /// <summary>
    /// حساب نشاط الأمريسيوم-241 للمصدر عند تاريخ حساب محدد اعتماداً على CalibrationDate
    /// كتاريخ مرجعي، ونصف عمر ثابت للأمريسيوم-241 = 432.2 سنة
    /// </summary>
    public NeutronDecayResult CalculateAm241ActivityAtDate(NeutronSource? source, DateTime calculationDate)
    {
        if (source == null)
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.MissingSource,
                CurrentActivityBq = null
            };
        }

        if (source.Am241ActivityValue is null || source.Am241ActivityUnitId is null)
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.NotRecorded,
                CurrentActivityBq = null
            };
        }

        if (source.Am241ActivityUnit == null)
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.MissingActivityUnit,
                CurrentActivityBq = null
            };
        }

        double activityBq = source.Am241ActivityValue.Value * source.Am241ActivityUnit.ConversionToBq;
        if (!double.IsFinite(activityBq) || activityBq <= 0)
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.InvalidActivityValue,
                CurrentActivityBq = null
            };
        }

        if (source.CalibrationDate is null)
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.MissingCalibrationDate,
                CurrentActivityBq = null
            };
        }

        if (calculationDate < source.CalibrationDate.Value)
        {
            return new NeutronDecayResult
            {
                Status = NeutronDecayCalculationStatus.CalculationDatePrecedesCalibrationDate,
                CurrentActivityBq = null
            };
        }

        // نصف عمر الأمريسيوم-241 = 432.2 سنة (يُكرر عمداً القيمة المرجعية المعتمدة في جدول
        // Radioisotope seed data، للحفاظ على استقلالية هذه الآلة الحسابية عن أي حقن خدمات).
        const double Am241HalfLifeYears = 432.2;
        double halfLifeSeconds = Am241HalfLifeYears * SecondsPerYear;

        double elapsedSeconds = (calculationDate - source.CalibrationDate.Value).TotalSeconds;
        double decayExponent = -Math.Log(2.0) * elapsedSeconds / halfLifeSeconds;
        double currentActivityBq = activityBq * Math.Exp(decayExponent);

        return new NeutronDecayResult
        {
            Status = NeutronDecayCalculationStatus.Calculated,
            CurrentActivityBq = currentActivityBq
        };
    }
}
