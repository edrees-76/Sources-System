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

public class NeutronSourceServiceTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly FakeAuditService _fakeAuditService;
    private readonly FakeUserService _fakeUserService;
    private readonly NeutronSourceService _sut;
    private readonly LocationService _locationService;

    public NeutronSourceServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        _fakeAuditService = new FakeAuditService();
        var role = new Role { Id = Guid.NewGuid(), RoleName = "مدير النظام", Permissions = "All" };
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "مسؤول النيوترونات",
            Username = "neutron_admin",
            RoleId = role.Id,
            Role = role,
            Permissions = "All",
            IsActive = true,
            IsEditor = true
        };
        _fakeUserService = new FakeUserService(user);
        _sut = new NeutronSourceService(_fixture.ContextFactory, _fakeAuditService, _fakeUserService);
        _locationService = new LocationService(_fixture.ContextFactory, _fakeAuditService, _fakeUserService);
    }

    public void Dispose()
    {
        _fixture.ResetDatabase();
    }

    [Fact]
    public void GetAll_ReturnsOnlyActiveNeutronSources_WithRelationships()
    {
        // Arrange
        var typeId = Guid.NewGuid();
        var locId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Am-241/Be", NameEn = "Americium-Beryllium", HalfLife = 432.2 });
            db.Locations.Add(new Location { Id = locId, LocationName = "Lab 101" });
            db.NeutronSources.AddRange(
                new NeutronSource { SourceCode = "NS-001", NeutronSourceTypeId = typeId, LocationId = locId, CalibratedEmissionRate = 2.2e6, IsDeleted = false },
                new NeutronSource { SourceCode = "NS-002", NeutronSourceTypeId = typeId, LocationId = locId, CalibratedEmissionRate = 1.1e6, IsDeleted = true }
            );
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetAll();

        // Assert
        Assert.Single(result);
        Assert.Equal("NS-001", result[0].SourceCode);
        Assert.NotNull(result[0].NeutronSourceType);
        Assert.Equal("Am-241/Be", result[0].NeutronSourceType!.Code);
        Assert.NotNull(result[0].Location);
        Assert.Equal("Lab 101", result[0].Location!.LocationName);
    }

    [Fact]
    public void GetDeleted_ReturnsOnlyDeletedSources()
    {
        // Arrange
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Cf-252", NameEn = "Californium-252", HalfLife = 2.645 });
            db.NeutronSources.AddRange(
                new NeutronSource { SourceCode = "NS-001", NeutronSourceTypeId = typeId, CalibratedEmissionRate = 1e6, IsDeleted = false },
                new NeutronSource { SourceCode = "NS-002", NeutronSourceTypeId = typeId, CalibratedEmissionRate = 2e6, IsDeleted = true, DeletedAt = DateTime.Now }
            );
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetDeleted();

        // Assert
        Assert.Single(result);
        Assert.Equal("NS-002", result[0].SourceCode);
        Assert.True(result[0].IsDeleted);
    }

    [Fact]
    public void GetById_And_GetByCode_ReturnExpectedSource()
    {
        // Arrange
        var id = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Pu-239/Be", NameEn = "Plutonium-Beryllium", HalfLife = 24110 });
            db.NeutronSources.Add(new NeutronSource { Id = id, SourceCode = "NS-100", SerialNumber = "SN-999", NeutronSourceTypeId = typeId, CalibratedEmissionRate = 5e5 });
            db.SaveChanges();
        }

        // Act
        var byId = _sut.GetById(id);
        var byCode = _sut.GetByCode("ns-100");

        // Assert
        Assert.NotNull(byId);
        Assert.Equal("NS-100", byId!.SourceCode);
        Assert.NotNull(byCode);
        Assert.Equal(id, byCode!.Id);
    }

    [Fact]
    public void GetByLocation_ReturnsOnlySourcesAtSpecificLocation()
    {
        // Arrange
        var typeId = Guid.NewGuid();
        var loc1 = Guid.NewGuid();
        var loc2 = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Cf-252", NameEn = "Cf-252", HalfLife = 2.645 });
            db.Locations.AddRange(new Location { Id = loc1, LocationName = "L1" }, new Location { Id = loc2, LocationName = "L2" });
            db.NeutronSources.AddRange(
                new NeutronSource { SourceCode = "NS-1", NeutronSourceTypeId = typeId, LocationId = loc1, CalibratedEmissionRate = 1e6 },
                new NeutronSource { SourceCode = "NS-2", NeutronSourceTypeId = typeId, LocationId = loc2, CalibratedEmissionRate = 2e6 }
            );
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetByLocation(loc1);

        // Assert
        Assert.Single(result);
        Assert.Equal("NS-1", result[0].SourceCode);
    }

    [Fact]
    public void Create_ValidSource_ReturnsSuccess_AndLogsAudit()
    {
        // Arrange
        var typeId = Guid.NewGuid();
        var locId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Am-241/Be", NameEn = "Americium-Beryllium", HalfLife = 432.2 });
            db.Locations.Add(new Location { Id = locId, LocationName = "Storage Room" });
            db.SaveChanges();
        }

        var newSource = new NeutronSource
        {
            SourceCode = "NS-2026-001",
            SerialNumber = "SN-12345",
            NeutronSourceTypeId = typeId,
            LocationId = locId,
            CalibratedEmissionRate = 2.2e6,
            RelativeExpandedUncertaintyPercent = 3.5,
            CalibrationDate = new DateTime(2025, 1, 1),
            Status = "Storage"
        };

        // Act
        var (success, message) = _sut.Create(newSource);

        // Assert
        Assert.True(success);
        Assert.Contains("بنجاح", message);

        using (var db = _fixture.CreateContext())
        {
            var saved = db.NeutronSources.FirstOrDefault(n => n.SourceCode == "NS-2026-001");
            Assert.NotNull(saved);
            Assert.Equal(2.2e6, saved!.CalibratedEmissionRate);
            Assert.Equal("SN-12345", saved.SerialNumber);
        }

        Assert.Contains(_fakeAuditService.LoggedEntries, l => l.Action == "Create" && l.TableName == "NeutronSources");
    }

    [Fact]
    public void Create_DuplicateSourceCode_ReturnsFailure()
    {
        // Arrange
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Am-241/Be", NameEn = "Am-Be", HalfLife = 432.2 });
            db.NeutronSources.Add(new NeutronSource { SourceCode = "NS-001", NeutronSourceTypeId = typeId, CalibratedEmissionRate = 1e6 });
            db.SaveChanges();
        }

        var duplicate = new NeutronSource { SourceCode = "ns-001", NeutronSourceTypeId = typeId, CalibratedEmissionRate = 2e6 };

        // Act
        var (success, message) = _sut.Create(duplicate);

        // Assert
        Assert.False(success);
        Assert.Contains("موجود بالفعل", message);
    }

    [Fact]
    public void Create_WithCodeOfDeletedNeutronSource_Succeeds_BecauseFilteredIndexAllowsReuse()
    {
        // Arrange: Soft-deleted neutron source with code "NS-REUSE-01"
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Cf-252", NameEn = "Californium-252", HalfLife = 2.645 });
            db.NeutronSources.Add(new NeutronSource
            {
                SourceCode = "NS-REUSE-01",
                NeutronSourceTypeId = typeId,
                CalibratedEmissionRate = 1e6,
                IsDeleted = true,
                DeletedAt = DateTime.Now
            });
            db.SaveChanges();
        }

        var newNeutronSource = new NeutronSource
        {
            SourceCode = "NS-REUSE-01",
            NeutronSourceTypeId = typeId,
            CalibratedEmissionRate = 2e6,
            Status = "Storage"
        };

        // Act: Create new neutron source with the same code as the soft-deleted one
        var (success, message) = _sut.Create(newNeutronSource);

        // Assert: Must succeed because NeutronSources uses a filtered index (IsDeleted = 0)
        Assert.True(success);
        Assert.Contains("بنجاح", message);

        using (var db = _fixture.CreateContext())
        {
            var sourcesWithCode = db.NeutronSources.IgnoreQueryFilters().Where(n => n.SourceCode == "NS-REUSE-01").ToList();
            Assert.Equal(2, sourcesWithCode.Count);
            Assert.Single(sourcesWithCode.Where(n => !n.IsDeleted));
            Assert.Single(sourcesWithCode.Where(n => n.IsDeleted));
        }
    }

    [Fact]
    public void Create_InvalidFields_ReturnsFailure()
    {
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Am-241/Be", NameEn = "Am-Be", HalfLife = 432.2 });
            db.SaveChanges();
        }

        // Act & Assert
        Assert.False(_sut.Create(null!).Success);
        Assert.False(_sut.Create(new NeutronSource { SourceCode = "", NeutronSourceTypeId = typeId, CalibratedEmissionRate = 1e6 }).Success);
        Assert.False(_sut.Create(new NeutronSource { SourceCode = "NS-1", NeutronSourceTypeId = typeId, CalibratedEmissionRate = 0 }).Success);
        Assert.False(_sut.Create(new NeutronSource { SourceCode = "NS-1", NeutronSourceTypeId = typeId, CalibratedEmissionRate = -100 }).Success);
        Assert.False(_sut.Create(new NeutronSource { SourceCode = "NS-1", NeutronSourceTypeId = Guid.NewGuid(), CalibratedEmissionRate = 1e6 }).Success);
        Assert.False(_sut.Create(new NeutronSource { SourceCode = "NS-1", NeutronSourceTypeId = typeId, LocationId = Guid.NewGuid(), CalibratedEmissionRate = 1e6 }).Success);
    }

    [Fact]
    public void Update_ValidSource_ReturnsSuccess_AndLogsAudit()
    {
        // Arrange
        var id = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Cf-252", NameEn = "Cf-252", HalfLife = 2.645 });
            db.NeutronSources.Add(new NeutronSource { Id = id, SourceCode = "NS-OLD", NeutronSourceTypeId = typeId, CalibratedEmissionRate = 1e6, Status = "Storage" });
            db.SaveChanges();
        }

        var updateItem = new NeutronSource { Id = id, SourceCode = "NS-NEW", NeutronSourceTypeId = typeId, CalibratedEmissionRate = 3.5e6, Status = "InUse" };

        // Act
        var (success, message) = _sut.Update(updateItem);

        // Assert
        Assert.True(success);
        using (var db = _fixture.CreateContext())
        {
            var updated = db.NeutronSources.Find(id);
            Assert.NotNull(updated);
            Assert.Equal("NS-NEW", updated!.SourceCode);
            Assert.Equal(3.5e6, updated.CalibratedEmissionRate);
            Assert.Equal("InUse", updated.Status);
        }

        Assert.Contains(_fakeAuditService.LoggedEntries, l => l.Action == "Update" && l.TableName == "NeutronSources");
    }

    [Fact]
    public void Delete_ExistingSource_SoftDeletes_AndLogsAudit()
    {
        // Arrange
        var id = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Cf-252", NameEn = "Cf-252", HalfLife = 2.645 });
            db.NeutronSources.Add(new NeutronSource { Id = id, SourceCode = "NS-DEL", NeutronSourceTypeId = typeId, CalibratedEmissionRate = 1e6 });
            db.SaveChanges();
        }

        // Act
        var (success, message) = _sut.Delete(id);

        // Assert
        Assert.True(success);
        using (var db = _fixture.CreateContext())
        {
            // Filtered query: hidden
            Assert.Null(db.NeutronSources.Find(id));

            // Raw query: soft deleted
            var raw = db.NeutronSources.IgnoreQueryFilters().FirstOrDefault(n => n.Id == id);
            Assert.NotNull(raw);
            Assert.True(raw!.IsDeleted);
            Assert.NotNull(raw.DeletedAt);
        }

        Assert.Contains(_fakeAuditService.LoggedEntries, l => l.Action == "Delete" && l.TableName == "NeutronSources");
    }

    [Fact]
    public void Restore_DeletedSource_RestoresSuccessfully_AndClearsSoftDeleteFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Am-241/Be", NameEn = "Am-Be", HalfLife = 432.2 });
            db.NeutronSources.Add(new NeutronSource { Id = id, SourceCode = "NS-REST", NeutronSourceTypeId = typeId, CalibratedEmissionRate = 1e6, IsDeleted = true, DeletedAt = DateTime.Now });
            db.SaveChanges();
        }

        // Act
        var (success, message) = _sut.Restore(id);

        // Assert
        Assert.True(success);
        using (var db = _fixture.CreateContext())
        {
            var restored = db.NeutronSources.Find(id);
            Assert.NotNull(restored);
            Assert.False(restored!.IsDeleted);
            Assert.Null(restored.DeletedAt);
            Assert.Null(restored.DeletedBy);
        }

        Assert.Contains(_fakeAuditService.LoggedEntries, l => l.Action == "Restore" && l.TableName == "NeutronSources");
    }

    [Fact]
    public void Location_Delete_FailsWhenActiveNeutronSourceExistsInLocation()
    {
        // Arrange
        var locId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(new Location { Id = locId, LocationName = "Neutron Bunker" });
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Pu-238/Be", NameEn = "Pu-238/Be", HalfLife = 87.7 });
            db.NeutronSources.Add(new NeutronSource { SourceCode = "NS-BUNKER-1", LocationId = locId, NeutronSourceTypeId = typeId, CalibratedEmissionRate = 5e6 });
            db.SaveChanges();
        }

        // Act
        var (success, message) = _locationService.Delete(locId);

        // Assert
        Assert.False(success);
        Assert.Contains("لا يمكن حذف الموقع", message);

        using (var db = _fixture.CreateContext())
        {
            var loc = db.Locations.Find(locId);
            Assert.NotNull(loc);
            Assert.False(loc!.IsDeleted);
        }
    }

    [Fact]
    public void Location_Delete_SucceedsWhenNeutronSourceInLocationIsSoftDeleted()
    {
        // Arrange
        var locId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(new Location { Id = locId, LocationName = "Old Neutron Lab" });
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Pu-238/Be", NameEn = "Pu-238/Be", HalfLife = 87.7 });
            db.NeutronSources.Add(new NeutronSource { SourceCode = "NS-OLD-1", LocationId = locId, NeutronSourceTypeId = typeId, CalibratedEmissionRate = 5e6, IsDeleted = true });
            db.SaveChanges();
        }

        // Act
        var (success, message) = _locationService.Delete(locId);

        // Assert
        Assert.True(success);
        using (var db = _fixture.CreateContext())
        {
            Assert.Null(db.Locations.Find(locId)); // Soft deleted
        }
    }

    #region Future CalibrationDate & EmissionCalibrationDate Validation Tests

    [Fact]
    public void Create_WithFutureCalibrationDate_ReturnsFalseWithErrorMessage()
    {
        // Arrange
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Am-241/Be", NameEn = "Americium-Beryllium", HalfLife = 432.2 });
            db.SaveChanges();
        }

        var item = new NeutronSource
        {
            SourceCode = "NS-FUT-CAL",
            NeutronSourceTypeId = typeId,
            CalibratedEmissionRate = 1e6,
            CalibrationDate = DateTime.Today.AddDays(1)
        };

        // Act
        var result = _sut.Create(item);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("لا يمكن أن يكون تاريخ المعايرة في المستقبل.", result.Message);
    }

    [Fact]
    public void Create_WithFutureEmissionCalibrationDate_ReturnsFalseWithErrorMessage()
    {
        // Arrange
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Am-241/Be", NameEn = "Americium-Beryllium", HalfLife = 432.2 });
            db.SaveChanges();
        }

        var item = new NeutronSource
        {
            SourceCode = "NS-FUT-EMIS",
            NeutronSourceTypeId = typeId,
            CalibratedEmissionRate = 1e6,
            EmissionCalibrationDate = DateTime.Today.AddDays(2)
        };

        // Act
        var result = _sut.Create(item);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("تاريخ معايرة الانبعاث لا يمكن أن يكون في المستقبل", result.Message);
    }

    [Fact]
    public void Update_WithFutureCalibrationDate_ReturnsFalseWithErrorMessage()
    {
        // Arrange
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Cf-252", NameEn = "Californium-252", HalfLife = 2.645 });
            db.SaveChanges();
        }

        var item = new NeutronSource
        {
            SourceCode = "NS-UPD-FUT-CAL",
            NeutronSourceTypeId = typeId,
            CalibratedEmissionRate = 1e6,
            CalibrationDate = DateTime.Today.AddDays(-10)
        };
        var createResult = _sut.Create(item);
        Assert.True(createResult.Success);

        // Act
        item.CalibrationDate = DateTime.Today.AddDays(3);
        var updateResult = _sut.Update(item);

        // Assert
        Assert.False(updateResult.Success);
        Assert.Equal("لا يمكن أن يكون تاريخ المعايرة في المستقبل.", updateResult.Message);
    }

    [Fact]
    public void Update_WithFutureEmissionCalibrationDate_ReturnsFalseWithErrorMessage()
    {
        // Arrange
        var typeId = Guid.NewGuid();
        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType { Id = typeId, Code = "Cf-252", NameEn = "Californium-252", HalfLife = 2.645 });
            db.SaveChanges();
        }

        var item = new NeutronSource
        {
            SourceCode = "NS-UPD-FUT-EMIS",
            NeutronSourceTypeId = typeId,
            CalibratedEmissionRate = 1e6,
            EmissionCalibrationDate = DateTime.Today.AddDays(-10)
        };
        var createResult = _sut.Create(item);
        Assert.True(createResult.Success);

        // Act
        item.EmissionCalibrationDate = DateTime.Today.AddDays(4);
        var updateResult = _sut.Update(item);

        // Assert
        Assert.False(updateResult.Success);
        Assert.Equal("تاريخ معايرة الانبعاث لا يمكن أن يكون في المستقبل", updateResult.Message);
    }

    #endregion
}

