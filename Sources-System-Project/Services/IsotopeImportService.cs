using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Sources.Helpers;

namespace Sources.Services;

public class IsotopeImportService : IIsotopeImportService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly string _jsonPath = @"D:\tmp\LibParser\isotopes_data.json";

    public IsotopeImportService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<(int imported, int updated)> ImportIsotopesAsync()
    {
        if (!File.Exists(_jsonPath))
            throw new FileNotFoundException("ملف البيانات غير موجود", _jsonPath);

        string json = await File.ReadAllTextAsync(_jsonPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var externalIsotopes = JsonSerializer.Deserialize<List<ExternalIsotope>>(json, options);

        if (externalIsotopes == null) return (0, 0);

        int importedCount = 0;
        int updatedCount = 0;

        using var db = _dbFactory.CreateDbContext();

        foreach (var ext in externalIsotopes)
        {
            var (value, unit) = GetSmartHalfLife(ext.HalfLife);
            string arabicName = IsotopeHelper.GetArabicNameFromSymbol(ext.Symbol);

            var existing = await db.Radioisotopes.FirstOrDefaultAsync(r => r.Symbol == ext.Symbol);
            if (existing != null)
            {
                existing.ArabicName = arabicName;
                existing.HalfLife = value;
                existing.HalfLifeUnit = unit;
                existing.Energy = ext.Energy;
                existing.Yield = ext.Yield;
                updatedCount++;
            }
            else
            {
                var isotope = new Radioisotope
                {
                    Symbol = ext.Symbol,
                    Name = ext.Symbol,
                    ArabicName = arabicName,
                    HalfLife = value,
                    HalfLifeUnit = unit,
                    Energy = ext.Energy,
                    Yield = ext.Yield,
                    RadiationType = "Gamma",
                    Notes = "Imported from Erdtmann/Soyka Library"
                };
                db.Radioisotopes.Add(isotope);
                importedCount++;
            }
        }

        if (importedCount > 0 || updatedCount > 0)
        {
            await db.SaveChangesAsync();
        }

        return (importedCount, updatedCount);
    }

    private (double value, string unit) GetSmartHalfLife(double days)
    {
        double seconds = days * 86400.0;
        if (seconds < 120) return (seconds, "seconds");
        
        double minutes = seconds / 60.0;
        if (minutes < 120) return (minutes, "minutes");

        double hours = minutes / 60.0;
        if (hours < 48) return (hours, "hours");

        if (days < 730) return (days, "days");

        double years = days / 365.25;
        return (years, "years");
    }

    private class ExternalIsotope
    {
        public string Symbol { get; set; } = string.Empty;
        public double HalfLife { get; set; } // in days
        public double Energy { get; set; }
        public double Yield { get; set; }
    }
}
