using System;
using Sources.Data;
using Sources.Models;

namespace Sources.Tests.Helpers;

public static class TestDataBuilder
{
    public static Radioisotope CreateRadioisotope(
        string symbol = "Cs-137",
        string name = "Cesium-137",
        double halfLife = 30.08,
        string halfLifeUnit = "years",
        double energy = 661.7,
        string radiationType = "Beta/Gamma",
        int category = 2)
    {
        return new Radioisotope
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            Name = name,
            ArabicName = name,
            HalfLife = halfLife,
            HalfLifeUnit = halfLifeUnit,
            Energy = energy,
            RadiationType = radiationType,
            Category = category
        };
    }

    public static ActivityUnit CreateActivityUnit(
        string name = "Becquerel",
        string symbol = "Bq",
        double conversionToBq = 1.0)
    {
        return new ActivityUnit
        {
            Id = Guid.NewGuid(),
            UnitName = name,
            UnitSymbol = symbol,
            ConversionToBq = conversionToBq
        };
    }

    public static Location CreateLocation(
        string name = "المختبر المركزي",
        string type = "Lab",
        string building = "مبنى 1",
        string room = "101")
    {
        return new Location
        {
            Id = Guid.NewGuid(),
            LocationName = name,
            LocationType = type,
            Building = building,
            Room = room
        };
    }

    public static Source CreateSource(
        Radioisotope isotope,
        ActivityUnit unit,
        Location? location = null,
        string sourceCode = "SRC-TEST-001",
        double initialActivity = 1000.0,
        DateTime? calibrationDate = null,
        string status = "InUse",
        bool hasDetailedIsotopes = false)
    {
        var calDate = calibrationDate ?? DateTime.Now.AddDays(-30);
        return new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = sourceCode,
            RadioisotopeId = isotope.Id,
            InitialActivityValue = initialActivity,
            InitialActivityUnitId = unit.Id,
            CalibrationDate = calDate,
            CurrentActivityValue = initialActivity,
            CurrentActivityUnitId = unit.Id,
            LocationId = location?.Id,
            Status = status,
            HasDetailedIsotopes = hasDetailedIsotopes,
            SerialNumber = $"SN-{Guid.NewGuid().ToString().Substring(0, 8)}",
            Manufacturer = "Test Manufacturer",
            Model = "Model-X",
            CreatedAt = DateTime.Now
        };
    }

    public static SourceIsotope CreateSourceIsotope(
        Source source,
        Radioisotope isotope,
        ActivityUnit unit,
        double initialActivity = 500.0,
        DateTime? calibrationDate = null)
    {
        return new SourceIsotope
        {
            Id = Guid.NewGuid(),
            SourceId = source.Id,
            RadioisotopeId = isotope.Id,
            ActivityUnitId = unit.Id,
            InitialActivityValue = initialActivity,
            CurrentActivityValue = initialActivity,
            CalibrationDate = calibrationDate ?? source.CalibrationDate
        };
    }
}
