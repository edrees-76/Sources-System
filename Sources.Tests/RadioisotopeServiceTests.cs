using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.Tests.Fixtures;
using Sources.Tests.Helpers;
using Xunit;

namespace Sources.Tests;

public class RadioisotopeServiceTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly FakeAuditService _fakeAuditService;
    private readonly FakeUserService _fakeUserService;
    private readonly RadioisotopeService _sut;

    public RadioisotopeServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        _fakeAuditService = new FakeAuditService();
        _fakeUserService = new FakeUserService();
        _sut = new RadioisotopeService(_fixture.ContextFactory, _fakeAuditService, _fakeUserService);
    }

    public void Dispose()
    {
        _fixture.ResetDatabase();
    }

    #region GetAll Tests

    [Fact]
    public void GetAll_ReturnsOnlyActiveRadioisotopes_OrderedAlphabeticallyByName()
    {
        // Arrange
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.AddRange(
                new Radioisotope { Symbol = "Co-60", Name = "Cobalt-60", ArabicName = "كوبالت-60", HalfLife = 5.27 },
                new Radioisotope { Symbol = "Am-241", Name = "Americium-241", ArabicName = "أمريسيوم-241", HalfLife = 432.2 },
                new Radioisotope { Symbol = "Cs-137", Name = "Cesium-137", ArabicName = "سيزيوم-137", HalfLife = 30.08, IsDeleted = true }
            );
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetAll();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Americium-241", result[0].Name);
        Assert.Equal("Cobalt-60", result[1].Name);
        Assert.DoesNotContain(result, r => r.IsDeleted);
        Assert.DoesNotContain(result, r => r.Symbol == "Cs-137");
    }

    [Fact]
    public void GetAll_WhenNoActiveRadioisotopes_ReturnsEmptyList()
    {
        // Arrange
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope { Symbol = "Cs-137", Name = "Cesium-137", IsDeleted = true, HalfLife = 30.08 });
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetAll();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public void GetById_ReturnsRadioisotope_WhenExistsAndActive()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope
            {
                Id = targetId,
                Symbol = "Cs-137",
                Name = "Cesium-137",
                ArabicName = "سيزيوم-137",
                HalfLife = 30.08,
                HalfLifeUnit = "years",
                Energy = 661.7,
                RadiationType = "Beta/Gamma",
                Category = 2
            });
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetById(targetId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(targetId, result.Id);
        Assert.Equal("Cs-137", result.Symbol);
        Assert.Equal("Cesium-137", result.Name);
    }

    [Fact]
    public void GetById_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = _sut.GetById(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetById_ReturnsNull_WhenRadioisotopeIsSoftDeleted()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope
            {
                Id = targetId,
                Symbol = "Cs-137",
                Name = "Cesium-137",
                HalfLife = 30.08,
                IsDeleted = true
            });
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetById(targetId);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Create Tests

    [Fact]
    public void Create_WithValidData_Succeeds_SetsAddedBy_AndLogsAudit()
    {
        // Arrange
        _fakeUserService.CurrentUser = new User { FullName = "علي أحمد" };
        var item = new Radioisotope
        {
            Symbol = "Co-60",
            Name = "Cobalt-60",
            ArabicName = "كوبالت-60",
            HalfLife = 5.27,
            HalfLifeUnit = "years",
            Energy = 1332.5,
            RadiationType = "Gamma",
            Yield = 0.9998,
            Category = 1,
            ExemptionLimit = 0.001,
            Notes = "ملاحظات عربية",
            EnglishNotes = "English Notes"
        };

        // Act
        var (success, message) = _sut.Create(item);

        // Assert
        Assert.True(success);
        Assert.Equal("تم إضافة النظير بنجاح", message);

        using (var db = _fixture.CreateContext())
        {
            var saved = db.Radioisotopes.FirstOrDefault(r => r.Symbol == "Co-60");
            Assert.NotNull(saved);
            Assert.Equal("Cobalt-60", saved.Name);
            Assert.Equal("كوبالت-60", saved.ArabicName);
            Assert.Equal("علي أحمد", saved.AddedBy);
            Assert.False(saved.IsDeleted);
        }

        Assert.Single(_fakeAuditService.LoggedEntries);
        var log = _fakeAuditService.LoggedEntries[0];
        Assert.Equal("Create", log.Action);
        Assert.Equal("Radioisotopes", log.TableName);
        Assert.Equal(item.Id, log.RecordId);
        Assert.Contains("Cobalt-60", log.Details);
    }

    [Fact]
    public void Create_WhenUserIsNull_SetsAddedByToDefaultUnknown()
    {
        // Arrange
        _fakeUserService.CurrentUser = null;
        var item = new Radioisotope
        {
            Symbol = "Co-60",
            Name = "Cobalt-60",
            HalfLife = 5.27
        };

        // Act
        var (success, _) = _sut.Create(item);

        // Assert
        Assert.True(success);
        using var db = _fixture.CreateContext();
        var saved = db.Radioisotopes.FirstOrDefault(r => r.Symbol == "Co-60");
        Assert.NotNull(saved);
        Assert.Equal("غير معروف", saved.AddedBy);
    }

    [Fact]
    public void Create_WithEmptyArabicName_AutoFillsArabicNameFromKnownSymbol()
    {
        // Arrange
        var item = new Radioisotope
        {
            Symbol = "Cs-137",
            Name = "Cesium-137",
            ArabicName = "", // empty
            HalfLife = 30.08
        };

        // Act
        var (success, message) = _sut.Create(item);

        // Assert
        Assert.True(success);
        using var db = _fixture.CreateContext();
        var saved = db.Radioisotopes.FirstOrDefault(r => r.Symbol == "Cs-137");
        Assert.NotNull(saved);
        Assert.Equal("137-سيزيوم", saved.ArabicName);
    }

    [Fact]
    public void Create_WithNullArabicName_AutoFillsArabicNameFromKnownSymbol()
    {
        // Arrange
        var item = new Radioisotope
        {
            Symbol = "Am-241",
            Name = "Americium-241",
            ArabicName = null,
            HalfLife = 432.2
        };

        // Act
        var (success, message) = _sut.Create(item);

        // Assert
        Assert.True(success);
        using var db = _fixture.CreateContext();
        var saved = db.Radioisotopes.FirstOrDefault(r => r.Symbol == "Am-241");
        Assert.NotNull(saved);
        Assert.Equal("241-أمريشيوم", saved.ArabicName);
    }

    [Fact]
    public void Create_WithUnknownSymbolAndEmptyArabicName_FallbacksToOriginalSymbol()
    {
        // Arrange
        var item = new Radioisotope
        {
            Symbol = "UnknownX-99",
            Name = "UnknownX-99",
            ArabicName = "",
            HalfLife = 10.0
        };

        // Act
        var (success, message) = _sut.Create(item);

        // Assert
        Assert.True(success);
        using var db = _fixture.CreateContext();
        var saved = db.Radioisotopes.FirstOrDefault(r => r.Symbol == "UnknownX-99");
        Assert.NotNull(saved);
        Assert.Equal("UnknownX-99", saved.ArabicName);
    }

    [Fact]
    public void Create_WithNullItem_ReturnsFalse()
    {
        // Act
        var (success, message) = _sut.Create(null!);

        // Assert
        Assert.False(success);
        Assert.Equal("بيانات النظير غير صالحة", message);
        Assert.Empty(_fakeAuditService.LoggedEntries);
    }

    [Fact]
    public void Create_WithDuplicateSymbol_ReturnsFalse()
    {
        // Arrange
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope
            {
                Symbol = "Cs-137",
                Name = "Cesium-137",
                HalfLife = 30.08
            });
            db.SaveChanges();
        }

        var duplicate = new Radioisotope
        {
            Symbol = "Cs-137",
            Name = "Cesium-137 Alternate",
            HalfLife = 30.08
        };

        // Act
        var (success, message) = _sut.Create(duplicate);

        // Assert
        Assert.False(success);
        Assert.Equal("رمز النظير موجود بالفعل", message);
        Assert.Empty(_fakeAuditService.LoggedEntries);
    }

    [Fact]
    public void Create_WithSymbolMatchingSoftDeletedIsotope_Succeeds()
    {
        // Arrange
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope
            {
                Symbol = "Cs-137",
                Name = "Old Cesium-137",
                HalfLife = 30.08,
                IsDeleted = true
            });
            db.SaveChanges();
        }

        var newItem = new Radioisotope
        {
            Symbol = "Cs-137",
            Name = "New Cesium-137",
            HalfLife = 30.08
        };

        // Act
        var (success, message) = _sut.Create(newItem);

        // Assert
        Assert.True(success);
        Assert.Equal("تم إضافة النظير بنجاح", message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.5)]
    public void Create_WithZeroOrNegativeHalfLife_ReturnsFalse(double halfLife)
    {
        // Arrange
        var item = new Radioisotope
        {
            Symbol = "Co-60",
            Name = "Cobalt-60",
            HalfLife = halfLife
        };

        // Act
        var (success, message) = _sut.Create(item);

        // Assert
        Assert.False(success);
        Assert.Equal("نصف العمر يجب أن يكون أكبر من صفر", message);
        Assert.Empty(_fakeAuditService.LoggedEntries);
    }

    [Fact]
    public void Create_WithNegativeEnergy_ReturnsFalse()
    {
        // Arrange
        var item = new Radioisotope
        {
            Symbol = "Co-60",
            Name = "Cobalt-60",
            HalfLife = 5.27,
            Energy = -50.0
        };

        // Act
        var (success, message) = _sut.Create(item);

        // Assert
        Assert.False(success);
        Assert.Equal("قيمة الطاقة غير صالحة", message);
        Assert.Empty(_fakeAuditService.LoggedEntries);
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_WithValidData_UpdatesAllFieldsIncludingEnglishNotesCategoryExemptionLimit_AndLogsAudit()
    {
        // Arrange
        var id = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope
            {
                Id = id,
                Symbol = "Co-60",
                Name = "Old Cobalt",
                ArabicName = "كوبالت قديم",
                RadiationType = "Gamma",
                HalfLife = 5.0,
                HalfLifeUnit = "years",
                Energy = 1000.0,
                Yield = 0.5,
                Category = 2,
                ExemptionLimit = 1.0,
                Notes = "Old Notes",
                EnglishNotes = "Old Eng Notes"
            });
            db.SaveChanges();
        }

        var updateItem = new Radioisotope
        {
            Id = id,
            Symbol = "Co-60",
            Name = "Updated Cobalt-60",
            ArabicName = "كوبالت-60 المحدث",
            RadiationType = "Beta/Gamma",
            HalfLife = 5.27,
            HalfLifeUnit = "years",
            Energy = 1332.5,
            Yield = 0.9998,
            Category = 1,
            ExemptionLimit = 0.001,
            Notes = "ملاحظات جديدة",
            EnglishNotes = "Updated English Notes"
        };

        // Act
        var (success, message) = _sut.Update(updateItem);

        // Assert
        Assert.True(success);
        Assert.Equal("تم تحديث النظير", message);

        using (var db = _fixture.CreateContext())
        {
            var updated = db.Radioisotopes.Find(id);
            Assert.NotNull(updated);
            Assert.Equal("Updated Cobalt-60", updated.Name);
            Assert.Equal("كوبالت-60 المحدث", updated.ArabicName);
            Assert.Equal("Co-60", updated.Symbol);
            Assert.Equal("Beta/Gamma", updated.RadiationType);
            Assert.Equal(5.27, updated.HalfLife);
            Assert.Equal(1332.5, updated.Energy);
            Assert.Equal(0.9998, updated.Yield);
            Assert.Equal(1, updated.Category);
            Assert.Equal(0.001, updated.ExemptionLimit);
            Assert.Equal("ملاحظات جديدة", updated.Notes);
            Assert.Equal("Updated English Notes", updated.EnglishNotes);
        }

        Assert.Single(_fakeAuditService.LoggedEntries);
        var log = _fakeAuditService.LoggedEntries[0];
        Assert.Equal("Update", log.Action);
        Assert.Equal("Radioisotopes", log.TableName);
        Assert.Equal(id, log.RecordId);
        Assert.Contains("Updated Cobalt-60", log.Details);
    }

    [Fact]
    public void Update_WithNullItem_ReturnsFalse()
    {
        // Act
        var (success, message) = _sut.Update(null!);

        // Assert
        Assert.False(success);
        Assert.Equal("بيانات النظير غير صالحة", message);
        Assert.Empty(_fakeAuditService.LoggedEntries);
    }

    [Fact]
    public void Update_WhenRadioisotopeNotFound_ReturnsFalse()
    {
        // Arrange
        var nonExistent = new Radioisotope
        {
            Id = Guid.NewGuid(),
            Symbol = "Co-60",
            Name = "Cobalt-60",
            HalfLife = 5.27
        };

        // Act
        var (success, message) = _sut.Update(nonExistent);

        // Assert
        Assert.False(success);
        Assert.Equal("النظير غير موجود", message);
        Assert.Empty(_fakeAuditService.LoggedEntries);
    }

    [Fact]
    public void Update_WithDuplicateSymbolOfAnotherActiveIsotope_ReturnsFalse()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.AddRange(
                new Radioisotope { Id = id1, Symbol = "Co-60", Name = "Cobalt-60", HalfLife = 5.27 },
                new Radioisotope { Id = id2, Symbol = "Cs-137", Name = "Cesium-137", HalfLife = 30.08 }
            );
            db.SaveChanges();
        }

        // Try to update id2's symbol to "Co-60" (which belongs to id1)
        var updateItem = new Radioisotope
        {
            Id = id2,
            Symbol = "Co-60",
            Name = "Cesium-137 Renamed",
            HalfLife = 30.08
        };

        // Act
        var (success, message) = _sut.Update(updateItem);

        // Assert
        Assert.False(success);
        Assert.Equal("رمز النظير موجود بالفعل", message);
        Assert.Empty(_fakeAuditService.LoggedEntries);
    }

    [Fact]
    public void Update_WithSameSymbol_Succeeds()
    {
        // Arrange
        var id = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope { Id = id, Symbol = "Co-60", Name = "Cobalt-60", HalfLife = 5.27 });
            db.SaveChanges();
        }

        var updateItem = new Radioisotope
        {
            Id = id,
            Symbol = "Co-60",
            Name = "Cobalt-60 Modified",
            HalfLife = 5.27
        };

        // Act
        var (success, message) = _sut.Update(updateItem);

        // Assert
        Assert.True(success);
        Assert.Equal("تم تحديث النظير", message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Update_WithInvalidHalfLife_ReturnsFalse(double halfLife)
    {
        // Arrange
        var id = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope { Id = id, Symbol = "Co-60", Name = "Cobalt-60", HalfLife = 5.27 });
            db.SaveChanges();
        }

        var updateItem = new Radioisotope
        {
            Id = id,
            Symbol = "Co-60",
            Name = "Cobalt-60",
            HalfLife = halfLife
        };

        // Act
        var (success, message) = _sut.Update(updateItem);

        // Assert
        Assert.False(success);
        Assert.Equal("نصف العمر يجب أن يكون أكبر من صفر", message);
        Assert.Empty(_fakeAuditService.LoggedEntries);
    }

    [Fact]
    public void Update_WithNegativeEnergy_ReturnsFalse()
    {
        // Arrange
        var id = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope { Id = id, Symbol = "Co-60", Name = "Cobalt-60", HalfLife = 5.27, Energy = 100 });
            db.SaveChanges();
        }

        var updateItem = new Radioisotope
        {
            Id = id,
            Symbol = "Co-60",
            Name = "Cobalt-60",
            HalfLife = 5.27,
            Energy = -10.0
        };

        // Act
        var (success, message) = _sut.Update(updateItem);

        // Assert
        Assert.False(success);
        Assert.Equal("قيمة الطاقة غير صالحة", message);
        Assert.Empty(_fakeAuditService.LoggedEntries);
    }

    [Fact]
    public void Update_WithEmptyArabicName_AutoFillsArabicNameFromSymbol()
    {
        // Arrange
        var id = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope
            {
                Id = id,
                Symbol = "Cs-137",
                Name = "Cesium-137",
                ArabicName = "اسم مخصص قديم",
                HalfLife = 30.08
            });
            db.SaveChanges();
        }

        var updateItem = new Radioisotope
        {
            Id = id,
            Symbol = "Cs-137",
            Name = "Cesium-137",
            ArabicName = "", // emptied by user
            HalfLife = 30.08
        };

        // Act
        var (success, message) = _sut.Update(updateItem);

        // Assert
        Assert.True(success);
        using var dbContext = _fixture.CreateContext();
        var updated = dbContext.Radioisotopes.Find(id);
        Assert.NotNull(updated);
        Assert.Equal("137-سيزيوم", updated.ArabicName);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_WithoutSourcesOrSourceIsotopes_SoftDeletes_AndLogsAudit()
    {
        // Arrange
        var id = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope
            {
                Id = id,
                Symbol = "Co-60",
                Name = "Cobalt-60",
                HalfLife = 5.27
            });
            db.SaveChanges();
        }

        // Act
        var (success, message) = _sut.Delete(id);

        // Assert
        Assert.True(success);
        Assert.Equal("تم حذف النظير", message);

        // Verify soft-deletion via raw query without query filter
        using (var db = _fixture.CreateContext())
        {
            var rawItem = db.Radioisotopes.IgnoreQueryFilters().FirstOrDefault(r => r.Id == id);
            Assert.NotNull(rawItem);
            Assert.True(rawItem.IsDeleted);

            // Normal query should not find it
            var normalItem = db.Radioisotopes.FirstOrDefault(r => r.Id == id);
            Assert.Null(normalItem);
        }

        Assert.Single(_fakeAuditService.LoggedEntries);
        var log = _fakeAuditService.LoggedEntries[0];
        Assert.Equal("Delete", log.Action);
        Assert.Equal("Radioisotopes", log.TableName);
        Assert.Equal(id, log.RecordId);
        Assert.Contains("Cobalt-60", log.Details);
    }

    [Fact]
    public void Delete_WhenLinkedDirectlyToSource_ReturnsFalse()
    {
        // Arrange
        var isotope = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137");
        var unit = TestDataBuilder.CreateActivityUnit();
        var location = TestDataBuilder.CreateLocation();
        var source = TestDataBuilder.CreateSource(isotope, unit, location);

        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(isotope);
            db.ActivityUnits.Add(unit);
            db.Locations.Add(location);
            db.Sources.Add(source);
            db.SaveChanges();
        }

        // Act
        var (success, message) = _sut.Delete(isotope.Id);

        // Assert
        Assert.False(success);
        Assert.Equal("لا يمكن حذف نظير مرتبط بمصادر", message);
        Assert.Empty(_fakeAuditService.LoggedEntries);

        using (var db = _fixture.CreateContext())
        {
            var item = db.Radioisotopes.Find(isotope.Id);
            Assert.NotNull(item);
            Assert.False(item.IsDeleted);
        }
    }

    [Fact]
    public void Delete_WhenLinkedOnlyToSourceIsotopes_ReturnsFalse()
    {
        // Arrange
        // Scenario: Primary source uses isotopeA, but source has detailed SourceIsotopes with isotopeB
        var isotopeA = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137");
        var isotopeB = TestDataBuilder.CreateRadioisotope("Co-60", "Cobalt-60");
        var unit = TestDataBuilder.CreateActivityUnit();
        var location = TestDataBuilder.CreateLocation();
        var source = TestDataBuilder.CreateSource(isotopeA, unit, location, hasDetailedIsotopes: true);
        var sourceIsotope = TestDataBuilder.CreateSourceIsotope(source, isotopeB, unit);

        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.AddRange(isotopeA, isotopeB);
            db.ActivityUnits.Add(unit);
            db.Locations.Add(location);
            db.Sources.Add(source);
            db.SourceIsotopes.Add(sourceIsotope);
            db.SaveChanges();
        }

        // Act: Attempt to delete isotopeB (which has no direct Source.RadioisotopeId link, only SourceIsotopes link)
        var (success, message) = _sut.Delete(isotopeB.Id);

        // Assert
        Assert.False(success);
        Assert.Equal("لا يمكن حذف نظير مرتبط بمصادر", message);
        Assert.Empty(_fakeAuditService.LoggedEntries);

        using (var db = _fixture.CreateContext())
        {
            var item = db.Radioisotopes.Find(isotopeB.Id);
            Assert.NotNull(item);
            Assert.False(item.IsDeleted);
        }
    }

    [Fact]
    public void Delete_WhenNotFoundOrAlreadyDeleted_ReturnsFalse()
    {
        // Arrange
        var deletedId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope
            {
                Id = deletedId,
                Symbol = "Cs-137",
                Name = "Cesium-137",
                HalfLife = 30.08,
                IsDeleted = true
            });
            db.SaveChanges();
        }

        // Act
        var (success1, message1) = _sut.Delete(deletedId);
        var (success2, message2) = _sut.Delete(Guid.NewGuid());

        // Assert
        Assert.False(success1);
        Assert.Equal("النظير غير موجود", message1);

        Assert.False(success2);
        Assert.Equal("النظير غير موجود", message2);

        Assert.Empty(_fakeAuditService.LoggedEntries);
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_WhenGammaConstantIsUpdated_PersistsNewGammaConstantValueToDatabase()
    {
        // Arrange: إنشاء نظير في قاعدة البيانات بدون ثابت غاما (أو بقيمة سابقة)
        var isotopeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope
            {
                Id = isotopeId,
                Symbol = "Cs-137",
                Name = "Cesium-137",
                ArabicName = "سيزيوم-137",
                RadiationType = "Gamma (γ)",
                HalfLife = 30.08,
                HalfLifeUnit = "years",
                Energy = 661.7,
                Yield = 0.85,
                GammaConstant = null
            });
            db.SaveChanges();
        }

        // Act: تحديث قيمة ثابت غاما إلى 0.0772 (قيمة ORNL المحولة)
        var updatedItem = new Radioisotope
        {
            Id = isotopeId,
            Symbol = "Cs-137",
            Name = "Cesium-137",
            ArabicName = "سيزيوم-137",
            RadiationType = "Gamma (γ)",
            HalfLife = 30.08,
            HalfLifeUnit = "years",
            Energy = 661.7,
            Yield = 0.85,
            GammaConstant = 0.0772,
            Notes = "Updated with ORNL gamma constant"
        };

        var (success, message) = _sut.Update(updatedItem);

        // Assert: التحقق من نجاح العملية ومن القيمة الفعلية في قاعدة البيانات
        Assert.True(success);
        Assert.Equal("تم تحديث النظير", message);

        using (var db = _fixture.CreateContext())
        {
            var savedInDb = db.Radioisotopes.Find(isotopeId);
            Assert.NotNull(savedInDb);
            Assert.NotNull(savedInDb.GammaConstant);
            Assert.Equal(0.0772, savedInDb.GammaConstant.Value, precision: 6);
            Assert.Equal("Updated with ORNL gamma constant", savedInDb.Notes);
        }
    }

    [Fact]
    public void Update_WhenChangingExistingGammaConstant_OverwritesWithNewValue()
    {
        // Arrange: نظير بقيمة غاما سابقة 0.3050 (Co-60)
        var isotopeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(new Radioisotope
            {
                Id = isotopeId,
                Symbol = "Co-60",
                Name = "Cobalt-60",
                ArabicName = "كوبالت-60",
                RadiationType = "Gamma (γ)",
                HalfLife = 5.27,
                HalfLifeUnit = "years",
                Energy = 1173.2,
                Yield = 0.99,
                GammaConstant = 0.3050
            });
            db.SaveChanges();
        }

        // Act: تعديل القيمة يدوياً إلى 0.3120
        var updatedItem = new Radioisotope
        {
            Id = isotopeId,
            Symbol = "Co-60",
            Name = "Cobalt-60",
            ArabicName = "كوبالت-60",
            RadiationType = "Gamma (γ)",
            HalfLife = 5.27,
            HalfLifeUnit = "years",
            Energy = 1173.2,
            Yield = 0.99,
            GammaConstant = 0.3120
        };

        var (success, message) = _sut.Update(updatedItem);

        // Assert
        Assert.True(success);
        using (var db = _fixture.CreateContext())
        {
            var savedInDb = db.Radioisotopes.Find(isotopeId);
            Assert.NotNull(savedInDb);
            Assert.NotNull(savedInDb.GammaConstant);
            Assert.Equal(0.3120, savedInDb.GammaConstant.Value, precision: 6);
        }
    }

    #endregion
}
