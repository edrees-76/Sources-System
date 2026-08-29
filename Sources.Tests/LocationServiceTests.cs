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

public class LocationServiceTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly FakeAuditService _fakeAuditService;
    private readonly FakeUserService _fakeUserService;
    private readonly LocationService _sut;

    public LocationServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        _fakeAuditService = new FakeAuditService();
        _fakeUserService = new FakeUserService();
        _sut = new LocationService(_fixture.ContextFactory, _fakeAuditService, _fakeUserService);
    }

    public void Dispose()
    {
        _fixture.ResetDatabase();
    }

    #region GetAll Tests

    [Fact]
    public void GetAll_ReturnsOnlyActiveLocations_OrderedAlphabetically()
    {
        // Arrange
        using (var db = _fixture.CreateContext())
        {
            db.Locations.AddRange(
                new Location { LocationName = "موقع ج", Building = "مبنى 3" },
                new Location { LocationName = "موقع أ", Building = "مبنى 1" },
                new Location { LocationName = "موقع ب (محذوف)", Building = "مبنى 2", IsDeleted = true }
            );
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetAll();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("موقع أ", result[0].LocationName);
        Assert.Equal("موقع ج", result[1].LocationName);
        Assert.DoesNotContain(result, l => l.IsDeleted);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public void GetById_ExistingActiveLocation_ReturnsLocation()
    {
        // Arrange
        var location = TestDataBuilder.CreateLocation(name: "مختبر القياس");
        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(location);
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetById(location.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(location.Id, result!.Id);
        Assert.Equal("مختبر القياس", result.LocationName);
    }

    [Fact]
    public void GetById_NonExistingLocation_ReturnsNull()
    {
        // Act
        var result = _sut.GetById(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetById_SoftDeletedLocation_ReturnsNull()
    {
        // Arrange
        var location = TestDataBuilder.CreateLocation(name: "موقع ملغي");
        location.IsDeleted = true;
        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(location);
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetById(location.Id);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Create Tests

    [Fact]
    public void Create_ValidLocation_WithCurrentUser_SavesSuccessfullyAndSetsAddedByAndLogsAudit()
    {
        // Arrange
        _fakeUserService.CurrentUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = "د. أحمد علي",
            Username = "ahmed",
            IsActive = true,
            RoleId = Guid.NewGuid()
        };

        var location = new Location
        {
            LocationName = "المستودع الرئيسي",
            LocationType = "Storage",
            Building = "المبنى المركزي",
            Room = "105",
            ResponsiblePerson = "علي حسن"
        };

        // Act
        var result = _sut.Create(location);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("تم إضافة الموقع بنجاح", result.Message);

        using (var db = _fixture.CreateContext())
        {
            var saved = db.Locations.Find(location.Id);
            Assert.NotNull(saved);
            Assert.Equal("المستودع الرئيسي", saved!.LocationName);
            Assert.Equal("د. أحمد علي", saved.AddedBy);
        }

        Assert.Single(_fakeAuditService.LoggedEntries);
        var audit = _fakeAuditService.LoggedEntries[0];
        Assert.Equal("Create", audit.Action);
        Assert.Equal("Locations", audit.TableName);
        Assert.Equal(location.Id, audit.RecordId);
        Assert.Contains("المستودع الرئيسي", audit.Details);
    }

    [Fact]
    public void Create_ValidLocation_WithNullCurrentUser_SavesSuccessfullyWithNullAddedBy()
    {
        // Arrange
        _fakeUserService.CurrentUser = null;

        var location = new Location
        {
            LocationName = "موقع بدون مستخدم"
        };

        // Act
        var result = _sut.Create(location);

        // Assert
        Assert.True(result.Success);
        using var db = _fixture.CreateContext();
        var saved = db.Locations.Find(location.Id);
        Assert.NotNull(saved);
        Assert.Null(saved!.AddedBy);
    }

    [Fact]
    public void Create_NullItem_ReturnsFalse()
    {
        // Act
        var result = _sut.Create(null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("بيانات الموقع غير صالحة", result.Message);
    }

    [Fact]
    public void Create_DuplicateNameWithWhitespace_ReturnsFalse()
    {
        // Arrange
        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(new Location { LocationName = "مختبر الكيمياء" });
            db.SaveChanges();
        }

        var newLocation = new Location
        {
            LocationName = "   مختبر الكيمياء   "
        };

        // Act
        var result = _sut.Create(newLocation);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("اسم الموقع موجود بالفعل", result.Message);
    }

    [Fact]
    public void Create_MatchingSoftDeletedLocationName_Succeeds()
    {
        // Arrange
        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(new Location
            {
                LocationName = "موقع قديم",
                IsDeleted = true
            });
            db.SaveChanges();
        }

        var newLocation = new Location
        {
            LocationName = "موقع قديم"
        };

        // Act
        var result = _sut.Create(newLocation);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("تم إضافة الموقع بنجاح", result.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_EmptyOrWhitespaceName_ReturnsFalse(string? name)
    {
        // Arrange
        var location = new Location
        {
            LocationName = name!
        };

        // Act
        var result = _sut.Create(location);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("اسم الموقع مطلوب", result.Message);
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_ValidLocation_UpdatesAllFieldsAndLogsAudit()
    {
        // Arrange
        var location = new Location
        {
            LocationName = "الاسم القديم",
            LocationType = "Storage",
            Building = "المبنى 1",
            Room = "101",
            ResponsiblePerson = "أحمد",
            AddedBy = "المسؤول الأول"
        };

        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(location);
            db.SaveChanges();
        }

        var updateItem = new Location
        {
            Id = location.Id,
            LocationName = "  الاسم الجديد  ",
            LocationType = "Lab",
            Building = "المبنى 2",
            Room = "202",
            ResponsiblePerson = "خالد"
        };

        // Act
        var result = _sut.Update(updateItem);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("تم تحديث الموقع", result.Message);

        using (var db = _fixture.CreateContext())
        {
            var updated = db.Locations.Find(location.Id);
            Assert.NotNull(updated);
            Assert.Equal("الاسم الجديد", updated!.LocationName);
            Assert.Equal("Lab", updated.LocationType);
            Assert.Equal("المبنى 2", updated.Building);
            Assert.Equal("202", updated.Room);
            Assert.Equal("خالد", updated.ResponsiblePerson);
            Assert.Equal("المسؤول الأول", updated.AddedBy); // AddedBy should remain unchanged
        }

        Assert.Single(_fakeAuditService.LoggedEntries);
        var audit = _fakeAuditService.LoggedEntries[0];
        Assert.Equal("Update", audit.Action);
        Assert.Equal("Locations", audit.TableName);
        Assert.Equal(location.Id, audit.RecordId);
        Assert.Contains("الاسم الجديد", audit.Details);
    }

    [Fact]
    public void Update_NullItem_ReturnsFalse()
    {
        // Act
        var result = _sut.Update(null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("بيانات الموقع غير صالحة", result.Message);
    }

    [Fact]
    public void Update_NonExistingLocation_ReturnsFalse()
    {
        // Arrange
        var item = new Location
        {
            Id = Guid.NewGuid(),
            LocationName = "موقع غير موجود"
        };

        // Act
        var result = _sut.Update(item);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("الموقع غير موجود", result.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Update_EmptyOrWhitespaceName_ReturnsFalse(string? name)
    {
        // Arrange
        var item = new Location
        {
            Id = Guid.NewGuid(),
            LocationName = name!
        };

        // Act
        var result = _sut.Update(item);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("اسم الموقع مطلوب", result.Message);
    }

    [Fact]
    public void Update_DuplicateNameWithAnotherActiveLocation_ReturnsFalse()
    {
        // Arrange
        var loc1 = new Location { LocationName = "الموقع الأول" };
        var loc2 = new Location { LocationName = "الموقع الثاني" };

        using (var db = _fixture.CreateContext())
        {
            db.Locations.AddRange(loc1, loc2);
            db.SaveChanges();
        }

        var updateItem = new Location
        {
            Id = loc2.Id,
            LocationName = "  الموقع الأول  "
        };

        // Act
        var result = _sut.Update(updateItem);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("اسم الموقع موجود بالفعل", result.Message);
    }

    [Fact]
    public void Update_KeepSameName_SucceedsSelfUpdate()
    {
        // Arrange
        var loc = new Location
        {
            LocationName = "موقع ثابت",
            Room = "100"
        };

        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(loc);
            db.SaveChanges();
        }

        var updateItem = new Location
        {
            Id = loc.Id,
            LocationName = "موقع ثابت",
            Room = "101"
        };

        // Act
        var result = _sut.Update(updateItem);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("تم تحديث الموقع", result.Message);

        using var dbVerify = _fixture.CreateContext();
        var updated = dbVerify.Locations.Find(loc.Id);
        Assert.NotNull(updated);
        Assert.Equal("101", updated!.Room);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_LocationWithoutSources_SoftDeletesAndLogsAudit()
    {
        // Arrange
        var location = TestDataBuilder.CreateLocation(name: "موقع للحذف");
        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(location);
            db.SaveChanges();
        }

        // Act
        var result = _sut.Delete(location.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("تم حذف الموقع", result.Message);

        using (var db = _fixture.CreateContext())
        {
            // Soft-deleted item should be invisible to normal query
            var normalQuery = db.Locations.Find(location.Id);
            Assert.Null(normalQuery);

            // Item exists when ignoring query filters with IsDeleted = true
            var deletedItem = db.Locations.IgnoreQueryFilters().FirstOrDefault(l => l.Id == location.Id);
            Assert.NotNull(deletedItem);
            Assert.True(deletedItem!.IsDeleted);
        }

        Assert.Single(_fakeAuditService.LoggedEntries);
        var audit = _fakeAuditService.LoggedEntries[0];
        Assert.Equal("Delete", audit.Action);
        Assert.Equal("Locations", audit.TableName);
        Assert.Equal(location.Id, audit.RecordId);
        Assert.Contains("موقع للحذف", audit.Details);
    }

    [Fact]
    public void Delete_LocationWithActiveSource_ReturnsFalse()
    {
        // Arrange
        var location = TestDataBuilder.CreateLocation(name: "موقع به مصادر");
        var isotope = TestDataBuilder.CreateRadioisotope();
        var unit = TestDataBuilder.CreateActivityUnit();
        var source = TestDataBuilder.CreateSource(isotope, unit, location, sourceCode: "SRC-LOC-01");

        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(isotope);
            db.ActivityUnits.Add(unit);
            db.Locations.Add(location);
            db.Sources.Add(source);
            db.SaveChanges();
        }

        // Act
        var result = _sut.Delete(location.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("لا يمكن حذف الموقع \"موقع به مصادر\" لاحتوائه على مصادر مرتبطة به", result.Message);

        using var dbVerify = _fixture.CreateContext();
        var notDeleted = dbVerify.Locations.Find(location.Id);
        Assert.NotNull(notDeleted);
        Assert.False(notDeleted!.IsDeleted);
    }

    [Fact]
    public void Delete_LocationWithOnlySoftDeletedSources_Succeeds()
    {
        // Arrange
        var location = TestDataBuilder.CreateLocation(name: "موقع به مصادر محذوفة");
        var isotope = TestDataBuilder.CreateRadioisotope();
        var unit = TestDataBuilder.CreateActivityUnit();
        var source = TestDataBuilder.CreateSource(isotope, unit, location, sourceCode: "SRC-LOC-02");
        source.IsDeleted = true;

        using (var db = _fixture.CreateContext())
        {
            db.Radioisotopes.Add(isotope);
            db.ActivityUnits.Add(unit);
            db.Locations.Add(location);
            db.Sources.Add(source);
            db.SaveChanges();
        }

        // Act
        var result = _sut.Delete(location.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("تم حذف الموقع", result.Message);

        using var dbVerify = _fixture.CreateContext();
        var deleted = dbVerify.Locations.Find(location.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public void Delete_NonExistingOrAlreadyDeletedLocation_ReturnsFalse()
    {
        // Act
        var result = _sut.Delete(Guid.NewGuid());

        // Assert
        Assert.False(result.Success);
        Assert.Equal("الموقع غير موجود", result.Message);
    }

    #endregion

    #region GetCount Tests

    [Fact]
    public void GetCount_ReturnsOnlyActiveLocationsCount()
    {
        // Arrange
        using (var db = _fixture.CreateContext())
        {
            db.Locations.AddRange(
                new Location { LocationName = "الموقع 1" },
                new Location { LocationName = "الموقع 2" },
                new Location { LocationName = "الموقع 3 (محذوف)", IsDeleted = true }
            );
            db.SaveChanges();
        }

        // Act
        var count = _sut.GetCount();

        // Assert
        Assert.Equal(2, count);
    }

    #endregion

    #region GetSourcesLinkedToLocation Tests

    [Fact]
    public void GetSourcesLinkedToLocation_LocationWithNoSources_ReturnsEmptyList()
    {
        // Arrange
        var loc = TestDataBuilder.CreateLocation(name: "موقع بدون مصادر");
        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(loc);
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetSourcesLinkedToLocation(loc.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void GetSourcesLinkedToLocation_CurrentlyLinkedSources_ReturnsThem()
    {
        // Arrange
        var loc = TestDataBuilder.CreateLocation(name: "موقع حالي");
        var iso = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137", 30.08, "years", 661.7);
        var unit = TestDataBuilder.CreateActivityUnit("Bq", "Bq", 1.0);
        var src1 = TestDataBuilder.CreateSource(iso, unit, loc, sourceCode: "SRC-LOC-01");
        var src2 = TestDataBuilder.CreateSource(iso, unit, loc, sourceCode: "SRC-LOC-02");

        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(loc);
            db.Radioisotopes.Add(iso);
            db.ActivityUnits.Add(unit);
            db.Sources.AddRange(src1, src2);
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetSourcesLinkedToLocation(loc.Id);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.SourceCode == "SRC-LOC-01");
        Assert.Contains(result, s => s.SourceCode == "SRC-LOC-02");
    }

    [Fact]
    public void GetSourcesLinkedToLocation_HistoricallyLinkedSources_ReturnsThemEvenIfCurrentLocationDifferent()
    {
        // Arrange
        var oldLoc = TestDataBuilder.CreateLocation(name: "الموقع القديم");
        var currentLoc = TestDataBuilder.CreateLocation(name: "الموقع الجديد الحالي");
        var iso = TestDataBuilder.CreateRadioisotope("Co-60", "Cobalt-60", 5.27, "years", 1332.5);
        var unit = TestDataBuilder.CreateActivityUnit("Ci", "Ci", 3.7e10);

        // المصدر موقعه الحالي هو الموقع الجديد
        var src = TestDataBuilder.CreateSource(iso, unit, currentLoc, sourceCode: "SRC-TRANSFERRED-01");

        // سجل تاريخي يوضح أنه كان سابقاً في الموقع القديم
        var history = new SourceLocationHistory
        {
            Id = Guid.NewGuid(),
            SourceId = src.Id,
            LocationId = oldLoc.Id,
            PreviousLocationId = null,
            MovedAt = DateTime.Now.AddDays(-30)
        };

        using (var db = _fixture.CreateContext())
        {
            db.Locations.AddRange(oldLoc, currentLoc);
            db.Radioisotopes.Add(iso);
            db.ActivityUnits.Add(unit);
            db.Sources.Add(src);
            db.SourceLocationHistories.Add(history);
            db.SaveChanges();
        }

        // Act - استعلام عن الموقع القديم
        var resultOldLoc = _sut.GetSourcesLinkedToLocation(oldLoc.Id);

        // Assert - يجب أن يظهر المصدر ضمن قائمة الموقع القديم بسبب السجل التاريخي
        Assert.Single(resultOldLoc);
        Assert.Equal("SRC-TRANSFERRED-01", resultOldLoc[0].SourceCode);
    }

    [Fact]
    public void GetSourcesLinkedToLocation_SourceBothCurrentAndHistorical_ReturnsSourceWithoutDuplicates()
    {
        // Arrange
        var loc = TestDataBuilder.CreateLocation(name: "موقع مزدوج الحالة");
        var otherLoc = TestDataBuilder.CreateLocation(name: "موقع مؤقت");
        var iso = TestDataBuilder.CreateRadioisotope("Am-241", "Americium-241", 432.2, "years", 59.54);
        var unit = TestDataBuilder.CreateActivityUnit("MBq", "MBq", 1.0e6);

        var src = TestDataBuilder.CreateSource(iso, unit, loc, sourceCode: "SRC-ROUNDTRIP-01");

        // سجلات تاريخية للمصدر في نفس الموقع
        var h1 = new SourceLocationHistory
        {
            Id = Guid.NewGuid(),
            SourceId = src.Id,
            LocationId = loc.Id,
            PreviousLocationId = null,
            MovedAt = DateTime.Now.AddDays(-60)
        };
        var h2 = new SourceLocationHistory
        {
            Id = Guid.NewGuid(),
            SourceId = src.Id,
            LocationId = otherLoc.Id,
            PreviousLocationId = loc.Id,
            MovedAt = DateTime.Now.AddDays(-30)
        };
        var h3 = new SourceLocationHistory
        {
            Id = Guid.NewGuid(),
            SourceId = src.Id,
            LocationId = loc.Id,
            PreviousLocationId = otherLoc.Id,
            MovedAt = DateTime.Now.AddDays(-5)
        };

        using (var db = _fixture.CreateContext())
        {
            db.Locations.AddRange(loc, otherLoc);
            db.Radioisotopes.Add(iso);
            db.ActivityUnits.Add(unit);
            db.Sources.Add(src);
            db.SourceLocationHistories.AddRange(h1, h2, h3);
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetSourcesLinkedToLocation(loc.Id);

        // Assert - يجب أن يظهر المصدر مرة واحدة فقط دون تكرار
        Assert.Single(result);
        Assert.Equal("SRC-ROUNDTRIP-01", result[0].SourceCode);
    }

    [Fact]
    public void GetAll_PopulatesCurrentSourceCountForEachLocation()
    {
        // Arrange
        var loc1 = TestDataBuilder.CreateLocation(name: "موقع به مصدرين");
        var loc2 = TestDataBuilder.CreateLocation(name: "موقع به مصدر واحد");
        var loc3 = TestDataBuilder.CreateLocation(name: "موقع بدون مصادر");

        var iso = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137", 30.08, "years", 661.7);
        var unit = TestDataBuilder.CreateActivityUnit("Bq", "Bq", 1.0);

        var s1 = TestDataBuilder.CreateSource(iso, unit, loc1, "SRC-CNT-01");
        var s2 = TestDataBuilder.CreateSource(iso, unit, loc1, "SRC-CNT-02");
        var s3 = TestDataBuilder.CreateSource(iso, unit, loc2, "SRC-CNT-03");

        using (var db = _fixture.CreateContext())
        {
            db.Locations.AddRange(loc1, loc2, loc3);
            db.Radioisotopes.Add(iso);
            db.ActivityUnits.Add(unit);
            db.Sources.AddRange(s1, s2, s3);
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetAll();

        // Assert
        var rLoc1 = result.First(l => l.Id == loc1.Id);
        var rLoc2 = result.First(l => l.Id == loc2.Id);
        var rLoc3 = result.First(l => l.Id == loc3.Id);

        Assert.Equal(2, rLoc1.SourceCount);
        Assert.Equal(1, rLoc2.SourceCount);
        Assert.Equal(0, rLoc3.SourceCount);
    }

    [Fact]
    public void Create_DuplicateLocationName_DifferentCase_ReturnsFailure()
    {
        // Arrange
        var existing = TestDataBuilder.CreateLocation(name: "Storage Room A");
        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(existing);
            db.SaveChanges();
        }

        var newLoc = new Location
        {
            Id = Guid.NewGuid(),
            LocationName = "storage room a", // Different case
            LocationType = "Storage"
        };

        // Act
        var result = _sut.Create(newLoc);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("اسم الموقع موجود بالفعل", result.Message);
    }

    [Fact]
    public void Update_DuplicateLocationName_DifferentCase_ReturnsFailure()
    {
        // Arrange
        var loc1 = TestDataBuilder.CreateLocation(name: "Central Lab");
        var loc2 = TestDataBuilder.CreateLocation(name: "Secondary Lab");
        using (var db = _fixture.CreateContext())
        {
            db.Locations.AddRange(loc1, loc2);
            db.SaveChanges();
        }

        loc2.LocationName = "central lab"; // Rename loc2 to loc1's name in lower case

        // Act
        var result = _sut.Update(loc2);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("اسم الموقع موجود بالفعل", result.Message);
    }

    [Fact]
    public void GetSourcesLinkedToLocation_WithSoftDeletedSource_ReturnsSourceWithDeletedBadge()
    {
        // Arrange
        var loc = TestDataBuilder.CreateLocation(name: "موقع به مصدر محذوف");
        var iso = TestDataBuilder.CreateRadioisotope("Co-60", "Cobalt-60", 5.27, "years", 1332.5);
        var unit = TestDataBuilder.CreateActivityUnit("Ci", "Ci", 3.7e10);

        var activeSource = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-ACTIVE-01");
        var deletedSource = TestDataBuilder.CreateSource(iso, unit, loc, "SRC-DEL-HIST-01");
        deletedSource.IsDeleted = true;

        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(loc);
            db.Radioisotopes.Add(iso);
            db.ActivityUnits.Add(unit);
            db.Sources.AddRange(activeSource, deletedSource);
            db.SaveChanges();
        }

        // Act
        var result = _sut.GetSourcesLinkedToLocation(loc.Id);

        // Assert
        Assert.Equal(2, result.Count);
        var active = result.FirstOrDefault(s => s.Id == activeSource.Id);
        var deleted = result.FirstOrDefault(s => s.Id == deletedSource.Id);

        Assert.NotNull(active);
        Assert.NotNull(deleted);
        Assert.Equal("SRC-ACTIVE-01", active.DisplaySourceCode);
        Assert.Equal("SRC-DEL-HIST-01 (محذوف)", deleted.DisplaySourceCode);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_WhenValidAndNoSources_PerformsSoftDelete_AndSetsDeletedAtAndDeletedBy()
    {
        // Arrange
        var role = TestDataBuilder.CreateRole();
        var user = TestDataBuilder.CreateUser(username: "deleter_loc", roleId: role.Id);
        var loc = TestDataBuilder.CreateLocation(name: "موقع للحذف");

        using (var db = _fixture.CreateContext())
        {
            db.Roles.Add(role);
            db.Users.Add(user);
            db.Locations.Add(loc);
            db.SaveChanges();
        }

        _fakeUserService.CurrentUser = user;

        // Act
        var (success, message) = _sut.Delete(loc.Id);

        // Assert
        Assert.True(success);
        Assert.Equal("تم حذف الموقع", message);

        using (var db = _fixture.CreateContext())
        {
            var rawLoc = db.Locations.IgnoreQueryFilters().FirstOrDefault(l => l.Id == loc.Id);
            Assert.NotNull(rawLoc);
            Assert.True(rawLoc.IsDeleted);
            Assert.NotNull(rawLoc.DeletedAt);
            Assert.Equal(user.Id, rawLoc.DeletedBy);

            // Verify normal query returns null due to Global Query Filter
            var normalLoc = db.Locations.FirstOrDefault(l => l.Id == loc.Id);
            Assert.Null(normalLoc);
        }

        Assert.Single(_fakeAuditService.LoggedEntries);
        var audit = _fakeAuditService.LoggedEntries[0];
        Assert.Equal("Delete", audit.Action);
        Assert.Equal("Locations", audit.TableName);
        Assert.Equal(loc.Id, audit.RecordId);
        Assert.Contains(loc.LocationName, audit.Details);
    }

    [Fact]
    public void Delete_WhenLocationHasLinkedSources_ReturnsFailure_AndDoesNotDelete()
    {
        // Arrange
        var loc = TestDataBuilder.CreateLocation(name: "موقع مرتبط بمصدر");
        var iso = TestDataBuilder.CreateRadioisotope("Co-60", "Cobalt-60");
        var unit = TestDataBuilder.CreateActivityUnit();
        var source = TestDataBuilder.CreateSource(iso, unit, loc);

        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(loc);
            db.Radioisotopes.Add(iso);
            db.ActivityUnits.Add(unit);
            db.Sources.Add(source);
            db.SaveChanges();
        }

        // Act
        var (success, message) = _sut.Delete(loc.Id);

        // Assert
        Assert.False(success);
        Assert.Contains("لا يمكن حذف الموقع", message);

        using (var db = _fixture.CreateContext())
        {
            var rawLoc = db.Locations.Find(loc.Id);
            Assert.NotNull(rawLoc);
            Assert.False(rawLoc.IsDeleted);
            Assert.Null(rawLoc.DeletedAt);
            Assert.Null(rawLoc.DeletedBy);
        }
    }

    #endregion
}
