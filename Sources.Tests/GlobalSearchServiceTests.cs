using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Sources.Tests.Helpers;
using Xunit;

namespace Sources.Tests;

public class GlobalSearchServiceTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly GlobalSearchService _searchService;

    public GlobalSearchServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        _searchService = new GlobalSearchService(_fixture.ContextFactory);
        SeedDatabase();
    }

    public void Dispose()
    {
        _fixture.ResetDatabase();
    }

    private void SeedDatabase()
    {
        using var context = _fixture.CreateContext();

        // 1. النظائر المشعة
        var isoCo60 = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Symbol = "Co-60",
            Name = "Cobalt-60",
            ArabicName = "كوبالت-60",
            HalfLife = 5.27,
            HalfLifeUnit = "years",
            RadiationType = "Gamma"
        };
        var isoCs137 = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Symbol = "Cs-137",
            Name = "Cesium-137",
            ArabicName = "سيزيوم-137",
            HalfLife = 30.08,
            HalfLifeUnit = "years",
            RadiationType = "Gamma"
        };
        var isoAm241 = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Symbol = "Am-241",
            Name = "Americium-241",
            ArabicName = "أمريسيوم-241",
            HalfLife = 432.2,
            HalfLifeUnit = "years",
            RadiationType = "Alpha"
        };
        var isoDeleted = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Symbol = "Ir-192",
            Name = "Iridium-192",
            ArabicName = "إيريديوم-192",
            IsDeleted = true
        };

        context.Radioisotopes.AddRange(isoCo60, isoCs137, isoAm241, isoDeleted);

        // 2. وحدات النشاط
        var unitMBq = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "Megabecquerel", UnitSymbol = "MBq", ConversionToBq = 1e6 };
        context.ActivityUnits.Add(unitMBq);

        // 3. المواقع
        var locLab = new Location
        {
            Id = Guid.NewGuid(),
            LocationName = "مختبر المعايرة الرئيسي",
            LocationType = "Lab",
            Building = "مبنى البحوث أ",
            Room = "101",
            ResponsiblePerson = "د. أحمد علي"
        };
        var locStorage = new Location
        {
            Id = Guid.NewGuid(),
            LocationName = "مخزن النظائر المركزي",
            LocationType = "Storage",
            Building = "مبنى ب",
            Room = "B-05",
            ResponsiblePerson = "م. إبراهيم خليل"
        };
        var locDeleted = new Location
        {
            Id = Guid.NewGuid(),
            LocationName = "مستودع قديم محذوف",
            IsDeleted = true
        };

        context.Locations.AddRange(locLab, locStorage, locDeleted);

        // 4. الأدوار والمستخدمين
        var adminRole = new Role { Id = Guid.NewGuid(), RoleName = "مدير النظام" };
        var userRole = new Role { Id = Guid.NewGuid(), RoleName = "مستخدم عادي" };
        context.Roles.AddRange(adminRole, userRole);

        var userAdmin = new User
        {
            Id = Guid.NewGuid(),
            FullName = "أحمد عبد الله",
            Username = "ahmed_admin",
            PasswordHash = "hash",
            Email = "ahmed@energy.gov.ly",
            RoleId = adminRole.Id,
            Role = adminRole
        };
        var userTech = new User
        {
            Id = Guid.NewGuid(),
            FullName = "عمر إبراهيم",
            Username = "omar_tech",
            PasswordHash = "hash",
            Email = "omar@lab.org",
            RoleId = userRole.Id,
            Role = userRole
        };
        var userDeleted = new User
        {
            Id = Guid.NewGuid(),
            FullName = "مستخدم محذوف",
            Username = "deleted_user",
            PasswordHash = "hash",
            IsDeleted = true,
            RoleId = userRole.Id
        };

        context.Users.AddRange(userAdmin, userTech, userDeleted);

        // 5. المصادر المشعة
        for (int i = 1; i <= 7; i++)
        {
            context.Sources.Add(new Source
            {
                Id = Guid.NewGuid(),
                SourceCode = $"SRC-CO-{i:D3}",
                SerialNumber = $"SN-COBALT-{i}",
                RadioisotopeId = isoCo60.Id,
                Radioisotope = isoCo60,
                LocationId = locStorage.Id,
                Location = locStorage,
                InitialActivityValue = 100,
                InitialActivityUnitId = unitMBq.Id,
                CurrentActivityValue = 90,
                CurrentActivityUnitId = unitMBq.Id,
                Manufacturer = "Best Theratronics",
                Model = "GammaBeam-100",
                Status = "InUse",
                CalibrationDate = DateTime.Today.AddYears(-1)
            });
        }

        context.Sources.Add(new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "SRC-CS-001",
            SerialNumber = "SN-CESIUM-882",
            RadioisotopeId = isoCs137.Id,
            Radioisotope = isoCs137,
            LocationId = locLab.Id,
            Location = locLab,
            InitialActivityValue = 50,
            InitialActivityUnitId = unitMBq.Id,
            CurrentActivityValue = 48,
            CurrentActivityUnitId = unitMBq.Id,
            Manufacturer = "Eckert & Ziegler",
            Model = "Isotope-Model-X",
            Status = "Storage",
            CalibrationDate = DateTime.Today.AddYears(-2)
        });

        context.Sources.Add(new Source
        {
            Id = Guid.NewGuid(),
            SourceCode = "SRC-DELETED-999",
            SerialNumber = "SN-DEL-999",
            RadioisotopeId = isoCo60.Id,
            Radioisotope = isoCo60,
            LocationId = locLab.Id,
            Location = locLab,
            InitialActivityValue = 10,
            InitialActivityUnitId = unitMBq.Id,
            CurrentActivityValue = 5,
            CurrentActivityUnitId = unitMBq.Id,
            IsDeleted = true
        });

        context.SaveChanges();
    }

    [Fact]
    public void TextNormalizer_NormalizesArabicHamzasAndAlifs()
    {
        Assert.Equal("احمد", TextNormalizer.Normalize("أحمد"));
        Assert.Equal("احمد", TextNormalizer.Normalize("إحمد"));
        Assert.Equal("احمد", TextNormalizer.Normalize("آحمد"));
        Assert.Equal("ابراهيم", TextNormalizer.Normalize("إبراهيم"));
    }

    [Fact]
    public void TextNormalizer_NormalizesYaaAndTaaMarboutaAndRemovesDiacritics()
    {
        Assert.Equal("مستشفي", TextNormalizer.Normalize("مُسْتَشْفَى"));
        Assert.Equal("معايره", TextNormalizer.Normalize("مُعَايَرَةٌ"));
        Assert.Equal("كوبالت", TextNormalizer.Normalize("كُـوْبَـالْـت"));
    }

    [Fact]
    public void TextNormalizer_ContainsNormalized_MatchesFlexibleQueries()
    {
        Assert.True(TextNormalizer.ContainsNormalized("مختبر المعايرة الرئيسي", "معايره"));
        Assert.True(TextNormalizer.ContainsNormalized("د. أحمد علي", "احمد"));
        Assert.True(TextNormalizer.ContainsNormalized("Cobalt-60 Beam", "cobalt"));
        Assert.True(TextNormalizer.ContainsNormalized("SRC-CO-001", "src-co"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData("أ")]
    public async Task GlobalSearchService_ReturnsEmpty_WhenQueryTooShort(string? shortQuery)
    {
        var results = await _searchService.SearchAsync(shortQuery!);
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task GlobalSearchService_SearchesSources_ByCode_AndSerialNumber()
    {
        var resultsByCode = await _searchService.SearchAsync("SRC-CS");
        Assert.NotEmpty(resultsByCode);
        var sourceGroup = resultsByCode.FirstOrDefault(g => g.Category == SearchCategory.Sources);
        Assert.NotNull(sourceGroup);
        Assert.Contains(sourceGroup.Items, item => item.Title == "SRC-CS-001");

        var resultsBySn = await _searchService.SearchAsync("CESIUM-882");
        var sourceGroupBySn = resultsBySn.FirstOrDefault(g => g.Category == SearchCategory.Sources);
        Assert.NotNull(sourceGroupBySn);
        Assert.Contains(sourceGroupBySn.Items, item => item.Title == "SRC-CS-001");
    }

    [Fact]
    public async Task GlobalSearchService_SearchesLocations_ByName_AndResponsiblePerson()
    {
        // البحث مع اختلاف الهمزة والتاء المربوطة
        var results = await _searchService.SearchAsync("معايره");
        var locGroup = results.FirstOrDefault(g => g.Category == SearchCategory.Locations);
        Assert.NotNull(locGroup);
        Assert.Contains(locGroup.Items, item => item.Title == "مختبر المعايرة الرئيسي");

        var resultsByPerson = await _searchService.SearchAsync("ابراهيم");
        var locGroupByPerson = resultsByPerson.FirstOrDefault(g => g.Category == SearchCategory.Locations);
        Assert.NotNull(locGroupByPerson);
        Assert.Contains(locGroupByPerson.Items, item => item.Title == "مخزن النظائر المركزي");
    }

    [Fact]
    public async Task GlobalSearchService_SearchesUsers_ByFullName_AndUsername_AndEmail()
    {
        var resultsByName = await _searchService.SearchAsync("احمد");
        var userGroup = resultsByName.FirstOrDefault(g => g.Category == SearchCategory.Users);
        Assert.NotNull(userGroup);
        Assert.Contains(userGroup.Items, item => item.Title == "أحمد عبد الله");

        var resultsByEmail = await _searchService.SearchAsync("lab.org");
        var userGroupByEmail = resultsByEmail.FirstOrDefault(g => g.Category == SearchCategory.Users);
        Assert.NotNull(userGroupByEmail);
        Assert.Contains(userGroupByEmail.Items, item => item.Title == "عمر إبراهيم");
    }

    [Fact]
    public async Task GlobalSearchService_SearchesRadioisotopes_BySymbol_AndNames()
    {
        var resultsBySymbol = await _searchService.SearchAsync("Am-241");
        var isotopeGroup = resultsBySymbol.FirstOrDefault(g => g.Category == SearchCategory.Radioisotopes);
        Assert.NotNull(isotopeGroup);
        Assert.Contains(isotopeGroup.Items, item => item.Title == "Am-241");

        var resultsByArabic = await _searchService.SearchAsync("امريسيوم");
        var isotopeGroupByArabic = resultsByArabic.FirstOrDefault(g => g.Category == SearchCategory.Radioisotopes);
        Assert.NotNull(isotopeGroupByArabic);
        Assert.Contains(isotopeGroupByArabic.Items, item => item.Title == "Am-241");
    }

    [Fact]
    public async Task GlobalSearchService_EnforcesTake5Limit_WhenMoreThan5Matches()
    {
        // أضفنا 7 مصادر لكوبالت Co-60
        var results = await _searchService.SearchAsync("SRC-CO");
        var sourceGroup = results.FirstOrDefault(g => g.Category == SearchCategory.Sources);

        Assert.NotNull(sourceGroup);
        Assert.Equal(5, sourceGroup.Items.Count);
        Assert.Equal(7, sourceGroup.TotalCount);
    }

    [Fact]
    public async Task GlobalSearchService_ExcludesDeletedEntities()
    {
        var results = await _searchService.SearchAsync("DELETED");
        Assert.Empty(results);

        var resultsIsotope = await _searchService.SearchAsync("Ir-192");
        Assert.Empty(resultsIsotope);
    }

    [Fact]
    public async Task GlobalSearchService_RunsInParallel_AndReturnsMultipleCategories()
    {
        // "Co-60" exists in Sources and in Radioisotopes
        var results = await _searchService.SearchAsync("Co-60");
        Assert.NotNull(results);

        var sourceGroup = results.FirstOrDefault(g => g.Category == SearchCategory.Sources);
        var isotopeGroup = results.FirstOrDefault(g => g.Category == SearchCategory.Radioisotopes);

        Assert.NotNull(sourceGroup);
        Assert.NotNull(isotopeGroup);
        Assert.True(sourceGroup.Items.Count > 0);
        Assert.True(isotopeGroup.Items.Count > 0);
    }
}
