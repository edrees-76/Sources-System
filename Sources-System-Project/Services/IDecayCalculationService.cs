using System;
using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

public interface IDecayCalculationService
{
    double CalculateCurrentActivity(double initialActivityBq, double halfLife, string halfLifeUnit, DateTime calibrationDate);
    double CalculateActivityAtDate(double initialActivityBq, double halfLife, string halfLifeUnit, DateTime calibrationDate, DateTime calculationDate);
    double CalculateCurrentActivityForSource(Source source, Radioisotope isotope, ActivityUnit initialUnit);
    double ConvertFromBq(double activityBq, double conversionToBq);
    double ConvertToBq(double activityValue, double conversionToBq);
    double ConvertFromBq(double activityBq, string unitSymbol);
    double ConvertToBq(double activityValue, string unitSymbol);
    double ConvertTimeToSeconds(double value, string unit);
    double CalculateDecayPercentage(double initialActivityBq, double currentActivityBq);
    double CalculateTimeToActivity(double initialActivityBq, double targetActivityBq, double halfLife, string halfLifeUnit);
    List<(DateTime Time, double Activity)> GenerateDecayCurve(double initialActivityBq, double halfLife, string halfLifeUnit, DateTime calibrationDate, int dataPoints = 50);
    List<(DateTime Time, double Activity)> GenerateUnifiedDecayCurve(double initialActivityBq, double halfLife, string halfLifeUnit, DateTime calibrationDate, DateTime startDate, DateTime endDate, int dataPoints = 50);
    List<(DateTime Time, double ActivityBq)> GetSourceCompositeDecayCurve(Source source, int points = 60);
}
