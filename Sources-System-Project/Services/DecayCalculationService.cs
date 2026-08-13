using System;
using Sources.Models;

namespace Sources.Services;

/// <summary>
/// محرك حساب الاضمحلال الإشعاعي
/// A = A₀ × (0.5)^(t / T½)
/// </summary>
public class DecayCalculationService : IDecayCalculationService
{
    /// <summary>
    /// حساب النشاط الإشعاعي الحالي
    /// </summary>
    /// <param name="initialActivityBq">النشاط الابتدائي بالبيكريل</param>
    /// <param name="halfLife">نصف العمر</param>
    /// <param name="halfLifeUnit">وحدة نصف العمر</param>
    /// <param name="calibrationDate">تاريخ المعايرة</param>
    /// <returns>النشاط الحالي بالبيكريل</returns>
    public double CalculateCurrentActivity(double initialActivityBq, double halfLife, string halfLifeUnit, DateTime calibrationDate)
    {
        if (halfLife <= 0 || initialActivityBq <= 0)
            return 0;

        // حساب الزمن المنقضي بالثواني
        var elapsedTime = (DateTime.Now - calibrationDate).TotalSeconds;
        if (elapsedTime < 0) elapsedTime = 0;

        // تحويل نصف العمر إلى ثوانٍ
        var halfLifeSeconds = ConvertToSeconds(halfLife, halfLifeUnit);
        if (halfLifeSeconds <= 0) return initialActivityBq;

        // A = A₀ × (0.5)^(t / T½)
        var decayFactor = Math.Pow(0.5, elapsedTime / halfLifeSeconds);
        return initialActivityBq * decayFactor;
    }

    /// <summary>
    /// حساب النشاط الحالي لمصدر مشع كامل
    /// </summary>
    public double CalculateCurrentActivityForSource(Source source, Radioisotope isotope, ActivityUnit initialUnit)
    {
        // تحويل النشاط الابتدائي إلى Bq
        var initialBq = source.InitialActivityValue * initialUnit.ConversionToBq;

        // حساب النشاط الحالي بالـ Bq
        return CalculateCurrentActivity(initialBq, isotope.HalfLife, isotope.HalfLifeUnit, source.CalibrationDate);
    }

    /// <summary>
    /// تحويل النشاط من Bq إلى وحدة أخرى
    /// </summary>
    public double ConvertFromBq(double activityBq, double conversionToBq)
    {
        if (conversionToBq <= 0) return activityBq;
        return activityBq / conversionToBq;
    }

    /// <summary>
    /// تحويل النشاط إلى Bq
    /// </summary>
    public double ConvertToBq(double activityValue, double conversionToBq)
    {
        return activityValue * conversionToBq;
    }

    /// <summary>
    /// حساب نسبة الاضمحلال
    /// </summary>
    public double CalculateDecayPercentage(double initialActivityBq, double currentActivityBq)
    {
        if (initialActivityBq <= 0) return 0;
        return ((initialActivityBq - currentActivityBq) / initialActivityBq) * 100;
    }

    /// <summary>
    /// حساب الزمن اللازم للوصول لنشاط معين
    /// </summary>
    public double CalculateTimeToActivity(double initialActivityBq, double targetActivityBq, double halfLife, string halfLifeUnit)
    {
        if (initialActivityBq <= 0 || targetActivityBq <= 0 || halfLife <= 0 || targetActivityBq >= initialActivityBq)
            return 0;

        var halfLifeSeconds = ConvertToSeconds(halfLife, halfLifeUnit);
        // t = T½ × log₂(A₀/A)
        return halfLifeSeconds * Math.Log2(initialActivityBq / targetActivityBq);
    }

    public List<(DateTime Time, double Activity)> GenerateDecayCurve(
        double initialActivityBq, double halfLife, string halfLifeUnit,
        DateTime calibrationDate, int dataPoints = 50)
    {
        var curve = new List<(DateTime, double)>();
        var halfLifeSeconds = ConvertToSeconds(halfLife, halfLifeUnit);
        
        // تمديد المنحنى للمستقبل: 5 أنصاف أعمار من "اليوم" لكي يظهر للمستخدم متى يصبح النشاط صغيراً جداً (حوالي 3% من نشاط اليوم)
        var futureEndDate = DateTime.Now.AddSeconds(halfLifeSeconds * 5);
        var totalTime = (futureEndDate - calibrationDate).TotalSeconds;
        
        // التحقق من أن المدة لا تقل عن 5 أنصاف أعمار في كل الأحوال
        if (totalTime < halfLifeSeconds * 5)
        {
            totalTime = halfLifeSeconds * 5;
        }

        var interval = totalTime / dataPoints;

        for (int i = 0; i <= dataPoints; i++)
        {
            var t = i * interval;
            var activity = initialActivityBq * Math.Pow(0.5, t / halfLifeSeconds);
            var time = calibrationDate.AddSeconds(t);
            curve.Add((time, activity));
        }

        return curve;
    }

    /// <summary>
    /// إنشاء بيانات منحنى الاضمحلال بتوقيتات محددة وموحدة (لضمان تطابق المحور السيني بين المصادر المختلفة)
    /// </summary>
    public List<(DateTime Time, double Activity)> GenerateUnifiedDecayCurve(
        double initialActivityBq, double halfLife, string halfLifeUnit,
        DateTime calibrationDate, DateTime startDate, DateTime endDate, int dataPoints = 50)
    {
        var curve = new List<(DateTime, double)>();
        var halfLifeSeconds = ConvertToSeconds(halfLife, halfLifeUnit);
        
        var totalSeconds = (endDate - startDate).TotalSeconds;
        if (totalSeconds <= 0) totalSeconds = 1;
        
        var interval = totalSeconds / dataPoints;

        for (int i = 0; i <= dataPoints; i++)
        {
            var time = startDate.AddSeconds(i * interval);
            var elapsedSecondsFromCalibration = (time - calibrationDate).TotalSeconds;
            
            double activity = initialActivityBq;
            if (elapsedSecondsFromCalibration > 0 && halfLifeSeconds > 0)
            {
                activity = initialActivityBq * Math.Pow(0.5, elapsedSecondsFromCalibration / halfLifeSeconds);
            }
            
            curve.Add((time, activity));
        }

        return curve;
    }

    private double ConvertToSeconds(double value, string unit)
    {
        return unit?.ToLower() switch
        {
            "seconds" => value,
            "minutes" => value * 60,
            "hours" => value * 3600,
            "days" => value * 86400,
            "years" => value * 365.25 * 86400,
            _ => value * 365.25 * 86400 // افتراضي: سنوات
        };
    }
}
