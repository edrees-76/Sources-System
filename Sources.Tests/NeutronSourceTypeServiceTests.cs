using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fakes;
using Sources.Tests.Fixtures;
using Xunit;

namespace Sources.Tests;

public class NeutronSourceTypeServiceTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly FakeAuditService _fakeAuditService;
    private readonly FakeUserService _fakeUserService;
    private readonly NeutronSourceTypeService _sut;

    public NeutronSourceTypeServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        _fakeAuditService = new FakeAuditService();
        _fakeUserService = new FakeUserService();
        _sut = new NeutronSourceTypeService(_fixture.ContextFactory, _fakeAuditService, _fakeUserService);
    }

    public void Dispose()
    {
        _fixture.ResetDatabase();
    }

    [Fact]
    public void GetAll_ReturnsOnlyActiveTypes_OrderedAlphabeticallyByCode()
    {
        // Arrange
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.AddRange(
                new NeutronSourceType { Code = "Pu-239/Be", NameEn = "Plutonium-Beryllium", HalfLife = 24110 },
                new NeutronSourceType { Code = "Am-241/Be", NameEn = "Americium-Beryllium", HalfLife = 432.2 },
                new NeutronSourceType { Code = "Cf-252", NameEn = "Californium-252", HalfLife = 2.645, IsDeleted = true }
            );
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetAll();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Am-241/Be", result[0].Code);
        Assert.Equal("Pu-239/Be", result[1].Code);
        Assert.DoesNotContain(result, t => t.IsDeleted);
    }

    [Fact]
    public void GetDeleted_ReturnsOnlyDeletedTypes()
    {
        // Arrange
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.AddRange(
                new NeutronSourceType { Code = "Am-241/Be", NameEn = "Americium-Beryllium", HalfLife = 432.2, IsDeleted = false },
                new NeutronSourceType { Code = "Cf-252", NameEn = "Californium-252", HalfLife = 2.645, IsDeleted = true, DeletedAt = DateTime.Now }
            );
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetDeleted();

        // Assert
        Assert.Single(result);
        Assert.Equal("Cf-252", result[0].Code);
        Assert.True(result[0].IsDeleted);
    }

    [Fact]
    public void GetById_ExistingId_ReturnsType()
    {
        // Arrange
        var id = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = id, Code = "Am-241/Be", NameEn = "Americium-Beryllium", HalfLife = 432.2 });
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetById(id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Am-241/Be", result!.Code);
    }

    [Fact]
    public void Create_ValidType_ReturnsSuccess_AndLogsAudit()
    {
        // Arrange
        var newType = new NeutronSourceType
        {
            Code = "Am-241/Be",
            NameEn = "Americium-241/Beryllium",
            NameAr = "أمريسيوم-241 / بيريليوم",
            ReactionType = "(α,n)",
            TargetMaterial = "Be",
            ParentNuclide = "Am-241",
            HalfLife = 432.2,
            HalfLifeUnit = "years"
        };

        // Act
        var (success, message) = _sut.Create(newType);

        // Assert
        Assert.True(success);
        Assert.Contains("بنجاح", message);

        using (var db = _fixture.CreateContext())
        {
            var saved = db.NeutronSourceTypes.FirstOrDefault(t => t.Code == "Am-241/Be");
            Assert.NotNull(saved);
            Assert.Equal("Americium-241/Beryllium", saved!.NameEn);
        }

        Assert.Contains(_fakeAuditService.LoggedEntries, l => l.Action == "Create" && l.TableName == "NeutronSourceTypes");
    }

    [Fact]
    public void Create_DuplicateCode_ReturnsFailure()
    {
        // Arrange
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Code = "Cf-252", NameEn = "Californium-252", HalfLife = 2.645 });
            db.SaveChanges();
        }

        var duplicate = new NeutronSourceType { Code = "cf-252", NameEn = "Another Cf", HalfLife = 2.645 };

        // Act
        var (success, message) = _sut.Create(duplicate);

        // Assert
        Assert.False(success);
        Assert.Contains("موجود بالفعل", message);
    }

    [Fact]
    public void Create_InvalidData_ReturnsFailure()
    {
        // Act & Assert
        Assert.False(_sut.Create(null!).Success);
        Assert.False(_sut.Create(new NeutronSourceType { Code = "", NameEn = "Test", HalfLife = 10 }).Success);
        Assert.False(_sut.Create(new NeutronSourceType { Code = "T-1", NameEn = "", HalfLife = 10 }).Success);
        Assert.False(_sut.Create(new NeutronSourceType { Code = "T-1", NameEn = "Test", HalfLife = 0 }).Success);
        Assert.False(_sut.Create(new NeutronSourceType { Code = "T-1", NameEn = "Test", HalfLife = -5 }).Success);
    }

    [Fact]
    public void Update_ValidType_ReturnsSuccess_AndLogsAudit()
    {
        // Arrange
        var id = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = id, Code = "Cf-252", NameEn = "Old Name", HalfLife = 2.645 });
            db.SaveChanges();
        }

        var updateItem = new NeutronSourceType { Id = id, Code = "Cf-252", NameEn = "Updated Californium-252", HalfLife = 2.645, ReactionType = "Spontaneous Fission" };

        // Act
        var (success, message) = _sut.Update(updateItem);

        // Assert
        Assert.True(success);
        using (var db = _fixture.CreateContext())
        {
            var updated = db.NeutronSourceTypes.Find(id);
            Assert.NotNull(updated);
            Assert.Equal("Updated Californium-252", updated!.NameEn);
            Assert.Equal("Spontaneous Fission", updated.ReactionType);
        }

        Assert.Contains(_fakeAuditService.LoggedEntries, l => l.Action == "Update" && l.TableName == "NeutronSourceTypes");
    }

    [Fact]
    public void Delete_TypeWithoutSources_ReturnsSuccess_SoftDeletes_AndLogsAudit()
    {
        // Arrange
        var id = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = id, Code = "Am-241/Li", NameEn = "Americium-Lithium", HalfLife = 432.2 });
            db.SaveChanges();
        }

        // Act
        var (success, message) = _sut.Delete(id);

        // Assert
        Assert.True(success);
        using (var db = _fixture.CreateContext())
        {
            // Global query filter should hide it from normal queries
            Assert.Null(db.NeutronSourceTypes.FirstOrDefault(t => t.Id == id));

            // IgnoreQueryFilters should show it as IsDeleted = true
            var raw = db.NeutronSourceTypes.IgnoreQueryFilters().FirstOrDefault(t => t.Id == id);
            Assert.NotNull(raw);
            Assert.True(raw!.IsDeleted);
            Assert.NotNull(raw.DeletedAt);
        }

        Assert.Contains(_fakeAuditService.LoggedEntries, l => l.Action == "Delete" && l.TableName == "NeutronSourceTypes");
    }

    [Fact]
    public void Delete_TypeWithActiveSources_ReturnsFailure_AndProtectsFromDeletion()
    {
        // Arrange
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            var nType = new NeutronSourceType { Id = typeId, Code = "Am-241/Be", NameEn = "Americium-Beryllium", HalfLife = 432.2 };
            db.NeutronSourceTypes.Add(nType);
            db.NeutronSources.Add(new NeutronSource { SourceCode = "NS-001", NeutronSourceTypeId = typeId, CalibratedEmissionRate = 2.2e6 });
            db.SaveChanges();
        }

        // Act
        var (success, message) = _sut.Delete(typeId);

        // Assert
        Assert.False(success);
        Assert.Contains("مرتبط بمصادر", message);

        using (var db = _fixture.CreateContext())
        {
            var nType = db.NeutronSourceTypes.Find(typeId);
            Assert.NotNull(nType);
            Assert.False(nType!.IsDeleted);
        }
    }

    [Fact]
    public void Restore_DeletedType_ReturnsSuccess_AndClearsSoftDeleteFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = id, Code = "Cf-252", NameEn = "Californium-252", HalfLife = 2.645, IsDeleted = true, DeletedAt = DateTime.Now.AddDays(-1) });
            db.SaveChanges();
        }

        // Act
        var (success, message) = _sut.Restore(id);

        // Assert
        Assert.True(success);
        using (var db = _fixture.CreateContext())
        {
            var restored = db.NeutronSourceTypes.Find(id);
            Assert.NotNull(restored);
            Assert.False(restored!.IsDeleted);
            Assert.Null(restored.DeletedAt);
            Assert.Null(restored.DeletedBy);
        }

        Assert.Contains(_fakeAuditService.LoggedEntries, l => l.Action == "Restore" && l.TableName == "NeutronSourceTypes");
    }

    [Fact]
    public void Restore_WhenAnotherActiveTypeHasSameCode_ReturnsFailure()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.AddRange(
                new NeutronSourceType { Id = id1, Code = "Cf-252", NameEn = "Active Cf", HalfLife = 2.645, IsDeleted = false },
                new NeutronSourceType { Id = id2, Code = "Cf-252", NameEn = "Deleted Cf", HalfLife = 2.645, IsDeleted = true }
            );
            db.SaveChanges();
        }

        // Act
        var (success, message) = _sut.Restore(id2);

        // Assert
        Assert.False(success);
        Assert.Contains("بنفس الرمز", message);
    }
}
