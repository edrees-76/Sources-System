using System;
using System.Collections.Generic;
using System.Linq;
using Sources.Models;

namespace Sources.Services;

/// <summary>
/// محرك حساب الاضمحلال الإشعاعي
/// A = A₀ × (0.5)^(t / T½)
/// </summary>
public class DecayCalculationService : IDecayCalculationService
{
    /// <summary>
    /// حساب النشاط الإشعاعي الحالي (عند اللحظة الحالية)
    /// </summary>
    public double CalculateCurrentActivity(double initialActivityBq, double halfLife, string halfLifeUnit, DateTime calibrationDate)
    {
        return CalculateActivityAtDate(initialActivityBq, halfLife, halfLifeUnit, calibrationDate, DateTime.Now);
    }

    /// <summary>
    /// حساب النشاط الإشعاعي عند تاريخ حساب محدد
    /// </summary>
    public double CalculateActivityAtDate(double initialActivityBq, double halfLife, string halfLifeUnit, DateTime calibrationDate, DateTime calculationDate)
    {
        if (halfLife <= 0 || initialActivityBq <= 0)
            return 0;

        // حساب الزمن المنقضي بالثواني
        var elapsedTime = (calculationDate - calibrationDate).TotalSeconds;
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
    /// تحويل النشاط من Bq إلى وحدة باستخدام اسم/رمز الوحدة
    /// </summary>
    public double ConvertFromBq(double activityBq, string unitSymbol)
    {
        return unitSymbol switch
        {
            "Bq" => activityBq,
            "kBq" => activityBq / 1e3,
            "MBq" => activityBq / 1e6,
            "GBq" => activityBq / 1e9,
            "TBq" => activityBq / 1e12,
            "Ci" => activityBq / 3.7e10,
            "mCi" => activityBq / 3.7e7,
            "µCi" or "uCi" => activityBq / 3.7e4,
            _ => activityBq
        };
    }

    /// <summary>
    /// تحويل النشاط إلى Bq
    /// </summary>
    public double ConvertToBq(double activityValue, double conversionToBq)
    {
        return activityValue * conversionToBq;
    }

    /// <summary>
    /// تحويل النشاط إلى Bq باستخدام اسم/رمز الوحدة
    /// </summary>
    public double ConvertToBq(double activityValue, string unitSymbol)
    {
        return unitSymbol switch
        {
            "Bq" => activityValue,
            "kBq" => activityValue * 1e3,
            "MBq" => activityValue * 1e6,
            "GBq" => activityValue * 1e9,
            "TBq" => activityValue * 1e12,
            "Ci" => activityValue * 3.7e10,
            "mCi" => activityValue * 3.7e7,
            "µCi" or "uCi" => activityValue * 3.7e4,
            _ => activityValue
        };
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

    /// <summary>
    /// توليد منحنى التحلل المركب لمصدر (سواء كان أحادي أو متعدد النويدات)
    /// </summary>
    public List<(DateTime Time, double ActivityBq)> GetSourceCompositeDecayCurve(Source source, int points = 60)
    {
        var curve = new List<(DateTime, double)>();
        if (source == null || points <= 0) return curve;

        // حالة 1: المصدر متعدد النويدات ويحتوي على تفاصيل نويدات صالحة
        if (source.HasDetailedIsotopes &&
            source.SourceIsotopes != null &&
            source.SourceIsotopes.Any(si => si.Radioisotope != null && (si.InitialActivityValue ?? 0) > 0))
        {
            var validIsotopes = source.SourceIsotopes
                .Where(si => si.Radioisotope != null && (si.InitialActivityValue ?? 0) > 0)
                .ToList();

            if (validIsotopes.Count > 0)
            {
                // أقدم تاريخ معايرة بين كل النويدات
                var startDate = validIsotopes.Min(si => si.CalibrationDate ?? (source.CalibrationDate != default ? source.CalibrationDate : DateTime.Today));
                if (startDate == default) startDate = DateTime.Today;

                // أطول فترة نصف عمر بين كل النويدات
                double maxHalfLifeSec = 0;
                foreach (var si in validIsotopes)
                {
                    var iso = si.Radioisotope!;
                    double sec = ConvertToSeconds(iso.HalfLife, iso.HalfLifeUnit);
                    if (sec > maxHalfLifeSec) maxHalfLifeSec = sec;
                }
                if (maxHalfLifeSec <= 0) maxHalfLifeSec = 86400; // يوم كافتراضي

                DateTime endDate;
                try
                {
                    double secondsToAdd = maxHalfLifeSec * 5;
                    double maxSecondsAllowed = (DateTime.MaxValue - startDate).TotalSeconds;
                    if (secondsToAdd > maxSecondsAllowed) secondsToAdd = maxSecondsAllowed - 86400;
                    endDate = startDate.AddSeconds(secondsToAdd);
                }
                catch
                {
                    endDate = DateTime.MaxValue.AddDays(-1);
                }

                double totalSeconds = (endDate - startDate).TotalSeconds;
                if (totalSeconds <= 0) totalSeconds = 1;
                double interval = totalSeconds / points;

                for (int i = 0; i <= points; i++)
                {
                    var pointTime = startDate.AddSeconds(i * interval);
                    double totalActivityBq = 0;

                    foreach (var si in validIsotopes)
                    {
                        var iso = si.Radioisotope!;
                        var calibDate = si.CalibrationDate ?? (source.CalibrationDate != default ? source.CalibrationDate : startDate);
                        double unitConv = si.ActivityUnit?.ConversionToBq ?? source.InitialActivityUnit?.ConversionToBq ?? 1;
                        double initBq = (si.InitialActivityValue ?? 0) * unitConv;
                        double halfLifeSec = ConvertToSeconds(iso.HalfLife, iso.HalfLifeUnit);

                        if (halfLifeSec <= 0 || initBq <= 0)
                        {
                            totalActivityBq += initBq;
                            continue;
                        }

                        double elapsed = (pointTime - calibDate).TotalSeconds;
                        if (elapsed <= 0)
                        {
                            totalActivityBq += initBq;
                        }
                        else
                        {
                            double decayFactor = Math.Pow(0.5, elapsed / halfLifeSec);
                            totalActivityBq += initBq * decayFactor;
                        }
                    }

                    curve.Add((pointTime, totalActivityBq));
                }

                return curve;
            }
        }

        // حالة 2: المصدر أحادي النويدة (أو الرجوع للنظير الأساسي)
        if (source.Radioisotope != null)
        {
            double unitConv = source.InitialActivityUnit?.ConversionToBq ?? 1;
            double initBq = source.InitialActivityValue * unitConv;
            var iso = source.Radioisotope;
            double halfLifeSec = ConvertToSeconds(iso.HalfLife, iso.HalfLifeUnit);
            if (halfLifeSec <= 0) halfLifeSec = 86400;

            var startDate = source.CalibrationDate != default ? source.CalibrationDate : DateTime.Today;
            DateTime endDate;
            try
            {
                double secondsToAdd = halfLifeSec * 5;
                double maxSecondsAllowed = (DateTime.MaxValue - startDate).TotalSeconds;
                if (secondsToAdd > maxSecondsAllowed) secondsToAdd = maxSecondsAllowed - 86400;
                endDate = startDate.AddSeconds(secondsToAdd);
            }
            catch
            {
                endDate = DateTime.MaxValue.AddDays(-1);
            }

            double totalSeconds = (endDate - startDate).TotalSeconds;
            if (totalSeconds <= 0) totalSeconds = 1;
            double interval = totalSeconds / points;

            for (int i = 0; i <= points; i++)
            {
                var pointTime = startDate.AddSeconds(i * interval);
                double elapsed = (pointTime - startDate).TotalSeconds;
                double activity = initBq;
                if (elapsed > 0 && halfLifeSec > 0)
                {
                    activity = initBq * Math.Pow(0.5, elapsed / halfLifeSec);
                }
                curve.Add((pointTime, activity));
            }

            return curve;
        }

        return curve;
    }

    public double ConvertTimeToSeconds(double value, string unit)
    {
        return ConvertToSeconds(value, unit);
    }


    private double ConvertToSeconds(double value, string unit)
    {
        return unit?.ToLower() switch
        {
            "seconds" or "second" or "s" => value,
            "minutes" or "minute" or "min" or "m" => value * 60,
            "hours" or "hour" or "h" => value * 3600,
            "days" or "day" or "d" => value * 86400,
            "months" or "month" or "mo" => value * 30 * 86400, // 30 يوماً
            "years" or "year" or "yr" or "y" => value * 365.25 * 86400, // 365.25 يوماً
            _ => value * 365.25 * 86400 // افتراضي: سنوات
        };
    }
}
