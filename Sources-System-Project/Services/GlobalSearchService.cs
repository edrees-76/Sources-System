using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;

namespace Sources.Services;

public class GlobalSearchService : IGlobalSearchService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public GlobalSearchService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<GlobalSearchResultGroup>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return new List<GlobalSearchResultGroup>();
        }

        var normalizedQuery = TextNormalizer.Normalize(query);
        if (string.IsNullOrEmpty(normalizedQuery))
        {
            return new List<GlobalSearchResultGroup>();
        }

        // تشغيل الاستعلامات الأربعة بالتوازي عبر Task.WhenAll
        var sourcesTask = SearchSourcesAsync(normalizedQuery, cancellationToken);
        var locationsTask = SearchLocationsAsync(normalizedQuery, cancellationToken);
        var usersTask = SearchUsersAsync(normalizedQuery, cancellationToken);
        var isotopesTask = SearchRadioisotopesAsync(normalizedQuery, cancellationToken);

        await Task.WhenAll(sourcesTask, locationsTask, usersTask, isotopesTask);

        var results = new List<GlobalSearchResultGroup>();

        if (sourcesTask.Result != null && sourcesTask.Result.Items.Count > 0)
            results.Add(sourcesTask.Result);

        if (locationsTask.Result != null && locationsTask.Result.Items.Count > 0)
            results.Add(locationsTask.Result);

        if (usersTask.Result != null && usersTask.Result.Items.Count > 0)
            results.Add(usersTask.Result);

        if (isotopesTask.Result != null && isotopesTask.Result.Items.Count > 0)
            results.Add(isotopesTask.Result);

        return results;
    }

    private async Task<GlobalSearchResultGroup> SearchSourcesAsync(string normalizedQuery, CancellationToken cancellationToken)
    {
        using var db = _dbFactory.CreateDbContext();

        // استخدام AsNoTracking مع إسقاط خفيف للحقول المطلوبة فقط
        var rawSources = await db.Sources
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .Select(s => new
            {
                s.Id,
                s.SourceCode,
                s.SerialNumber,
                s.Manufacturer,
                s.Model,
                s.Status,
                s.CurrentActivityValue,
                CurrentUnitSymbol = s.CurrentActivityUnit != null ? s.CurrentActivityUnit.UnitSymbol : "",
                IsotopeSymbol = s.Radioisotope != null ? s.Radioisotope.Symbol : "",
                IsotopeName = s.Radioisotope != null ? s.Radioisotope.Name : "",
                IsotopeArabicName = s.Radioisotope != null ? s.Radioisotope.ArabicName : "",
                LocationName = s.Location != null ? s.Location.LocationName : ""
            })
            .ToListAsync(cancellationToken);

        var matched = rawSources.Where(s =>
            TextNormalizer.ContainsNormalized(s.SourceCode, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(s.SerialNumber, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(s.IsotopeSymbol, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(s.IsotopeName, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(s.IsotopeArabicName, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(s.LocationName, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(s.Manufacturer, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(s.Model, normalizedQuery)
        ).ToList();

        var items = matched.Take(5).Select(s =>
        {
            var isotopeDisplay = !string.IsNullOrEmpty(s.IsotopeSymbol) ? s.IsotopeSymbol : s.IsotopeName;
            var locDisplay = !string.IsNullOrEmpty(s.LocationName) ? $" • {s.LocationName}" : "";
            var actDisplay = s.CurrentActivityValue > 0 ? $" • {s.CurrentActivityValue:0.##} {s.CurrentUnitSymbol}".Trim() : "";

            return new GlobalSearchResultItem
            {
                Id = s.Id,
                Category = SearchCategory.Sources,
                Title = s.SourceCode,
                Subtitle = $"{isotopeDisplay}{locDisplay}{actDisplay}",
                ExtraInfo = !string.IsNullOrEmpty(s.SerialNumber) ? $"S/N: {s.SerialNumber}" : null,
                IconKind = "Radioactive",
                TargetView = "Sources"
            };
        }).ToList();

        return new GlobalSearchResultGroup
        {
            Category = SearchCategory.Sources,
            GroupTitle = TranslationHelper.GetString("MenuSources") ?? "المصادر المشعة",
            GroupIcon = "Radioactive",
            TotalCount = matched.Count,
            Items = items
        };
    }

    private async Task<GlobalSearchResultGroup> SearchLocationsAsync(string normalizedQuery, CancellationToken cancellationToken)
    {
        using var db = _dbFactory.CreateDbContext();

        var rawLocations = await db.Locations
            .AsNoTracking()
            .Where(l => !l.IsDeleted)
            .Select(l => new
            {
                l.Id,
                l.LocationName,
                l.LocationType,
                l.Building,
                l.Room,
                l.ResponsiblePerson
            })
            .ToListAsync(cancellationToken);

        var matched = rawLocations.Where(l =>
            TextNormalizer.ContainsNormalized(l.LocationName, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(l.Building, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(l.Room, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(l.ResponsiblePerson, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(l.LocationType, normalizedQuery)
        ).ToList();

        var items = matched.Take(5).Select(l =>
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(l.Building)) parts.Add(l.Building);
            if (!string.IsNullOrEmpty(l.Room)) parts.Add($"غرفة {l.Room}");
            if (!string.IsNullOrEmpty(l.ResponsiblePerson)) parts.Add($"المسؤول: {l.ResponsiblePerson}");

            return new GlobalSearchResultItem
            {
                Id = l.Id,
                Category = SearchCategory.Locations,
                Title = l.LocationName,
                Subtitle = parts.Count > 0 ? string.Join(" • ", parts) : (l.LocationType ?? "موقع"),
                ExtraInfo = l.LocationType,
                IconKind = "MapMarker",
                TargetView = "Locations"
            };
        }).ToList();

        return new GlobalSearchResultGroup
        {
            Category = SearchCategory.Locations,
            GroupTitle = TranslationHelper.GetString("MenuLocations") ?? "المواقع والمخازن",
            GroupIcon = "MapMarker",
            TotalCount = matched.Count,
            Items = items
        };
    }

    private async Task<GlobalSearchResultGroup> SearchUsersAsync(string normalizedQuery, CancellationToken cancellationToken)
    {
        using var db = _dbFactory.CreateDbContext();

        var rawUsers = await db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Username,
                u.Email,
                RoleName = u.Role != null ? u.Role.RoleName : ""
            })
            .ToListAsync(cancellationToken);

        var matched = rawUsers.Where(u =>
            TextNormalizer.ContainsNormalized(u.FullName, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(u.Username, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(u.Email, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(u.RoleName, normalizedQuery)
        ).ToList();

        var items = matched.Take(5).Select(u =>
        {
            var roleStr = u.RoleName == "مدير النظام" 
                ? (TranslationHelper.GetString("RoleAdmin") ?? "مدير النظام") 
                : (TranslationHelper.GetString("RoleUser") ?? "مستخدم عادي");

            var subParts = new List<string> { $"@{u.Username}" };
            if (!string.IsNullOrEmpty(u.Email)) subParts.Add(u.Email);

            return new GlobalSearchResultItem
            {
                Id = u.Id,
                Category = SearchCategory.Users,
                Title = u.FullName,
                Subtitle = string.Join(" • ", subParts),
                ExtraInfo = roleStr,
                IconKind = "AccountGroup",
                TargetView = "Users"
            };
        }).ToList();

        return new GlobalSearchResultGroup
        {
            Category = SearchCategory.Users,
            GroupTitle = TranslationHelper.GetString("MenuUsers") ?? "المستخدمين",
            GroupIcon = "AccountGroup",
            TotalCount = matched.Count,
            Items = items
        };
    }

    private async Task<GlobalSearchResultGroup> SearchRadioisotopesAsync(string normalizedQuery, CancellationToken cancellationToken)
    {
        using var db = _dbFactory.CreateDbContext();

        var rawIsotopes = await db.Radioisotopes
            .AsNoTracking()
            .Where(r => !r.IsDeleted)
            .Select(r => new
            {
                r.Id,
                r.Symbol,
                r.Name,
                r.ArabicName,
                r.RadiationType,
                r.HalfLife,
                r.HalfLifeUnit
            })
            .ToListAsync(cancellationToken);

        var matched = rawIsotopes.Where(r =>
            TextNormalizer.ContainsNormalized(r.Symbol, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(r.Name, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(r.ArabicName, normalizedQuery) ||
            TextNormalizer.ContainsNormalized(r.RadiationType, normalizedQuery)
        ).ToList();

        var items = matched.Take(5).Select(r =>
        {
            var nameDisplay = !string.IsNullOrEmpty(r.ArabicName) ? $"{r.Name} ({r.ArabicName})" : r.Name;
            var halfLifeDisplay = $"{r.HalfLife:0.##} {r.HalfLifeUnit}";

            return new GlobalSearchResultItem
            {
                Id = r.Id,
                Category = SearchCategory.Radioisotopes,
                Title = r.Symbol,
                Subtitle = $"{nameDisplay} • {r.RadiationType}",
                ExtraInfo = $"نصف العمر: {halfLifeDisplay}",
                IconKind = "Atom",
                TargetView = "Radioisotopes"
            };
        }).ToList();

        return new GlobalSearchResultGroup
        {
            Category = SearchCategory.Radioisotopes,
            GroupTitle = TranslationHelper.GetString("MenuRadioisotopes") ?? "النظائر المشعة",
            GroupIcon = "Atom",
            TotalCount = matched.Count,
            Items = items
        };
    }
}
