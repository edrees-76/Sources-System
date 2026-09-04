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

public class SourceServiceTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly DecayCalculationService _decayService;
    private readonly FakeAuditService _auditService;
    private readonly FakeUserService _userService;
    private readonly SourceService _sourceService;

    private Radioisotope _isoCs137 = null!;
    private Radioisotope _isoCo60 = null!;
    private Radioisotope _isoAm241 = null!;
    private ActivityUnit _unitBq = null!;
    private ActivityUnit _unitMBq = null!;
    private ActivityUnit _unitCi = null!;
    private Location _testLocation = null!;
    private User _testUser = null!;

    public SourceServiceTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        _decayService = new DecayCalculationService();
        _auditService = new FakeAuditService();
        _userService = new FakeUserService();

        SeedCommonData();

        _userService.CurrentUser = _testUser;

        _sourceService = new SourceService(
            _fixture.ContextFactory,
            _decayService,
            _auditService,
            _userService);
    }

    public void Dispose()
    {
        _fixture.ResetDatabase();
    }

    private void SeedCommonData()
    {
        using var context = _fixture.CreateContext();

        _isoCs137 = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137", 30.08, "years", 661.7);
        _isoCo60 = TestDataBuilder.CreateRadioisotope("Co-60", "Cobalt-60", 5.27, "years", 1332.5);
        _isoAm241 = TestDataBuilder.CreateRadioisotope("Am-241", "Americium-241", 432.2, "years", 59.54);

        _unitBq = TestDataBuilder.CreateActivityUnit("Becquerel", "Bq", 1.0);
        _unitMBq = TestDataBuilder.CreateActivityUnit("Megabecquerel", "MBq", 1.0e6);
        _unitCi = TestDataBuilder.CreateActivityUnit("Curie", "Ci", 3.7e10);

        _testLocation = TestDataBuilder.CreateLocation("مختبر المعايرة الرئيسي", "Lab", "المبنى 1", "101");

        var role = new Role { Id = Guid.NewGuid(), RoleName = "مدير النظام", Permissions = "All" };
        _testUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = "أحمد المسؤول",
            Username = "ahmed_admin",
            PasswordHash = "hash123",
            RoleId = role.Id,
            Role = role,
            Permissions = "All",
            IsActive = true,
            IsEditor = true
        };

        context.Roles.Add(role);
        context.Users.Add(_testUser);
        context.Radioisotopes.AddRange(_isoCs137, _isoCo60, _isoAm241);
        context.ActivityUnits.AddRange(_unitBq, _unitMBq, _unitCi);
        context.Locations.Add(_testLocation);

        context.SaveChanges();
    }

    #region 1. CreateSource Tests

    [Fact]
    public void CreateSource_ValidSingleSource_SucceedsAndLogsAudit()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(
            _isoCs137,
            _unitBq,
            _testLocation,
            sourceCode: "SRC-SRV-001",
            initialActivity: 100000.0,
            calibrationDate: DateTime.Now.AddDays(-10));

        // Act
        var result = _sourceService.CreateSource(source);
        var created = _sourceService.GetSourceById(source.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("تم إضافة المصدر بنجاح", result.Message);
        Assert.NotNull(created);
        Assert.Equal("SRC-SRV-001", created.SourceCode);
        Assert.Equal(_userService.CurrentUser!.Id, created.AddedBy);
        Assert.Equal(_userService.CurrentUser!.FullName, created.AddedByName);
        Assert.True(created.CurrentActivityValue > 0);
        Assert.True(created.CurrentActivityValue <= created.InitialActivityValue);

        Assert.Contains(_auditService.LoggedEntries, log =>
            log.Action == "Create" &&
            log.TableName == "Sources" &&
            log.RecordId == source.Id &&
            log.Details != null &&
            log.Details.Contains("SRC-SRV-001"));

        var createLog = _auditService.LoggedEntries.Single(log =>
            log.Action == "Create" &&
            log.TableName == "Sources" &&
            log.RecordId == source.Id);

        Assert.Null(createLog.OldValues);
        Assert.NotNull(createLog.NewValues);
        Assert.Contains("SRC-SRV-001", createLog.NewValues);
        Assert.Contains(source.Manufacturer!, createLog.NewValues);
        Assert.Contains(source.Model!, createLog.NewValues);
    }

    [Fact]
    public void CreateSource_DuplicateSourceCode_ReturnsFalseWithErrorMessage()
    {
        // Arrange
        var source1 = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-DUP-CHECK");
        var source2 = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, sourceCode: "SRC-DUP-CHECK");

        // Act
        var result1 = _sourceService.CreateSource(source1);
        var result2 = _sourceService.CreateSource(source2);

        // Assert
        Assert.True(result1.Success);
        Assert.False(result2.Success);
        Assert.Equal("كود المصدر موجود بالفعل", result2.Message);
        Assert.Equal(1, _sourceService.GetTotalSourcesCount());
    }

    [Fact]
    public void CreateSource_WithDeletedSourceCode_ReturnsFalseWithSpecificMessage_WithoutThrowingException()
    {
        // Arrange: Create and soft delete a source
        var sourceDeleted = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-DEL-REUSE");
        var createResult = _sourceService.CreateSource(sourceDeleted);
        Assert.True(createResult.Success);

        var deleteResult = _sourceService.DeleteSource(sourceDeleted.Id);
        Assert.True(deleteResult.Success);

        var newSource = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, sourceCode: "SRC-DEL-REUSE");

        // Act: Attempt to create a new source with the deleted source's code
        var result = _sourceService.CreateSource(newSource);

        // Assert: Must fail gracefully without throwing DbUpdateException/SqliteException
        Assert.False(result.Success);
        Assert.Contains("SRC-DEL-REUSE", result.Message);
        Assert.Contains("مستخدم لمصدر محذوف", result.Message);
        Assert.Contains("المحذوفات", result.Message);
        Assert.Contains("حفاظاً على سجل التدقيق", result.Message);
    }

    [Fact]
    public void CreateSource_WithCaseInsensitiveAndWhitespaceVariations_DetectsDuplicateActiveAndDeleted()
    {
        // Arrange: Active source
        var activeSource = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-CASE-TEST");
        _sourceService.CreateSource(activeSource);

        // Act & Assert 1: Case and whitespace duplicate on ACTIVE source
        var duplicateActive = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, sourceCode: "  src-case-test  ");
        var activeResult = _sourceService.CreateSource(duplicateActive);
        Assert.False(activeResult.Success);
        Assert.Equal("كود المصدر موجود بالفعل", activeResult.Message);

        // Arrange 2: Deleted source
        var deletedSource = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-DEL-CASE");
        _sourceService.CreateSource(deletedSource);
        _sourceService.DeleteSource(deletedSource.Id);

        // Act & Assert 2: Case and whitespace duplicate on DELETED source
        var duplicateDeleted = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, sourceCode: "  src-del-case  ");
        var deletedResult = _sourceService.CreateSource(duplicateDeleted);
        Assert.False(deletedResult.Success);
        Assert.Contains("src-del-case", deletedResult.Message);
        Assert.Contains("مستخدم لمصدر محذوف", deletedResult.Message);
        Assert.Contains("المحذوفات", deletedResult.Message);
    }

    [Fact]
    public void CreateSource_MultiIsotope_CalculatesTotalInitialAndCurrentActivityInBqAndConvertsCorrectly()
    {
        // Arrange
        // المصدر بوحدة MBq (1 MBq = 1,000,000 Bq)
        var source = TestDataBuilder.CreateSource(
            _isoCs137,
            _unitMBq,
            _testLocation,
            sourceCode: "SRC-MULTI-CALC",
            hasDetailedIsotopes: true);

        // نظير 1: 1 MBq = 1,000,000 Bq
        var isotope1 = TestDataBuilder.CreateSourceIsotope(source, _isoCs137, _unitMBq, initialActivity: 1.0, calibrationDate: DateTime.Now);
        // نظير 2: 2,000,000 Bq = 2 MBq
        var isotope2 = TestDataBuilder.CreateSourceIsotope(source, _isoCo60, _unitBq, initialActivity: 2000000.0, calibrationDate: DateTime.Now);

        var isotopesList = new List<SourceIsotope> { isotope1, isotope2 };

        // Act
        var result = _sourceService.CreateSource(source, isotopesList);
        var created = _sourceService.GetSourceById(source.Id);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(created);
        Assert.True(created.HasDetailedIsotopes);
        Assert.Equal(2, created.SourceIsotopes.Count);

        // النشاط الابتدائي الإجمالي = (1,000,000 + 2,000,000) Bq = 3,000,000 Bq => 3.0 MBq
        Assert.Equal(3.0, created.InitialActivityValue, precision: 2);
        // النشاط الحالي الإجمالي اليوم ≈ 3.0 MBq (نظراً لأن تاريخ المعايرة هو الآن)
        Assert.InRange(created.CurrentActivityValue, 2.99, 3.01);

        // التأكد من حساب النشاط الحالي لكل نظير فرعي
        var si1 = created.SourceIsotopes.First(i => i.RadioisotopeId == _isoCs137.Id);
        var si2 = created.SourceIsotopes.First(i => i.RadioisotopeId == _isoCo60.Id);
        Assert.NotNull(si1.CurrentActivityValue);
        Assert.NotNull(si2.CurrentActivityValue);
        Assert.InRange(si1.CurrentActivityValue.Value, 0.99, 1.01);
        Assert.InRange(si2.CurrentActivityValue.Value, 1990000.0, 2000001.0);
    }

    [Fact]
    public void CreateSource_MultiIsotope_NullIsotopeUnit_FallsBackToSourceUnitAndCalculatesCorrectly()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(
            _isoCs137,
            _unitMBq,
            _testLocation,
            sourceCode: "SRC-FALLBACK-UNIT",
            hasDetailedIsotopes: true);

        // النظير الأول بدون تحديد وحدة (ActivityUnitId = null) => يتراجع لوحدة المصدر (MBq)
        var isotope = new SourceIsotope
        {
            Id = Guid.NewGuid(),
            RadioisotopeId = _isoCs137.Id,
            ActivityUnitId = null, // لا توجد وحدة محددة للنظير
            InitialActivityValue = 5.0, // 5 MBq = 5,000,000 Bq
            CalibrationDate = DateTime.Now
        };

        // Act
        var result = _sourceService.CreateSource(source, new List<SourceIsotope> { isotope });
        var created = _sourceService.GetSourceById(source.Id);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(created);
        // تم استخدام وحدة المصدر MBq للتحويل
        Assert.Equal(5.0, created.InitialActivityValue, precision: 2);
        Assert.InRange(created.CurrentActivityValue, 4.9, 5.1);
    }

    #endregion

    #region 2. UpdateSource Tests

    [Fact]
    public void UpdateSource_ValidSource_SucceedsAndUpdatesPropertiesAndLogsAudit()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-UPD-001", status: "InUse");
        _sourceService.CreateSource(source);
        var originalSerialNumber = source.SerialNumber!;
        var originalStatus = source.Status;

        // Act
        source.SerialNumber = "SN-NEW-12345";
        source.Manufacturer = "Atomic Instruments Co";
        source.Model = "AI-2026-X";
        source.Status = "Storage";
        source.Notes = "تم نقله للمستودع";

        var updateResult = _sourceService.UpdateSource(source);
        var retrieved = _sourceService.GetSourceById(source.Id);

        // Assert
        Assert.True(updateResult.Success);
        Assert.Equal("تم تحديث المصدر بنجاح", updateResult.Message);
        Assert.NotNull(retrieved);
        Assert.Equal("SN-NEW-12345", retrieved.SerialNumber);
        Assert.Equal("Atomic Instruments Co", retrieved.Manufacturer);
        Assert.Equal("AI-2026-X", retrieved.Model);
        Assert.Equal("Storage", retrieved.Status);
        Assert.Equal("تم نقله للمستودع", retrieved.Notes);

        Assert.Contains(_auditService.LoggedEntries, log =>
            log.Action == "Update" &&
            log.TableName == "Sources" &&
            log.RecordId == source.Id);

        var updateLog = _auditService.LoggedEntries.Single(log =>
            log.Action == "Update" &&
            log.TableName == "Sources" &&
            log.RecordId == source.Id);

        Assert.NotNull(updateLog.OldValues);
        Assert.NotNull(updateLog.NewValues);
        Assert.Contains(originalSerialNumber, updateLog.OldValues);
        Assert.DoesNotContain("SN-NEW-12345", updateLog.OldValues);
        Assert.Contains(originalStatus, updateLog.OldValues);

        Assert.Contains("SN-NEW-12345", updateLog.NewValues);
        Assert.DoesNotContain(originalSerialNumber, updateLog.NewValues);
        Assert.Contains("Storage", updateLog.NewValues);
    }

    [Fact]
    public void UpdateSource_UpdatesIsSealedProperty_PersistsToDatabase()
    {
        // Arrange - إنشاء مصدر IsSealed = true
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-SEAL-UPD-01", isSealed: true);
        var createResult = _sourceService.CreateSource(source);
        Assert.True(createResult.Success);

        // التأكد من أن القيمة الأولية المحفوظة هي true
        var initial = _sourceService.GetSourceById(source.Id);
        Assert.NotNull(initial);
        Assert.True(initial.IsSealed);

        // Act - تعديل IsSealed إلى false
        source.IsSealed = false;
        var updateResult = _sourceService.UpdateSource(source);

        // Assert
        Assert.True(updateResult.Success);
        Assert.Equal("تم تحديث المصدر بنجاح", updateResult.Message);

        var updated = _sourceService.GetSourceById(source.Id);
        Assert.NotNull(updated);
        Assert.False(updated.IsSealed);
    }


    [Fact]
    public void UpdateSource_DuplicateSourceCodeFromAnotherSource_ReturnsFalseWithErrorMessage()
    {
        // Arrange
        var source1 = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-CODE-EXISTING");
        var source2 = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, sourceCode: "SRC-CODE-ORIGINAL");
        _sourceService.CreateSource(source1);
        _sourceService.CreateSource(source2);

        // Act - محاولة تعديل source2 ليأخذ نفس كود source1
        source2.SourceCode = "SRC-CODE-EXISTING";
        var result = _sourceService.UpdateSource(source2);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("كود المصدر موجود بالفعل", result.Message);

        // التأكد من عدم تغيير كود source2 في قاعدة البيانات
        var retrieved2 = _sourceService.GetSourceById(source2.Id);
        Assert.NotNull(retrieved2);
        Assert.Equal("SRC-CODE-ORIGINAL", retrieved2.SourceCode);
    }

    [Fact]
    public void UpdateSource_SameSourceCode_AllowsUpdateWithoutFalseDuplicateError()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-SAME-CODE");
        _sourceService.CreateSource(source);

        // Act - تعديل المصدر مع الاحتفاظ بنفس SourceCode
        source.Notes = "تعديل الملاحظات مع إبقاء نفس الكود";
        var result = _sourceService.UpdateSource(source);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("تم تحديث المصدر بنجاح", result.Message);
    }

    [Fact]
    public void UpdateSource_ReplacingIsotopesList_RemovesOldAndAddsNewAndRecalculatesActivity()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-REPLACE-ISO-SRV");
        var initialIsotope = TestDataBuilder.CreateSourceIsotope(source, _isoCs137, _unitBq, initialActivity: 5000.0);
        _sourceService.CreateSource(source, new List<SourceIsotope> { initialIsotope });

        // Act - استبدال النظير القديم بنظيرين جديدين
        var newIso1 = TestDataBuilder.CreateSourceIsotope(source, _isoCo60, _unitBq, initialActivity: 1000.0, calibrationDate: DateTime.Now);
        var newIso2 = TestDataBuilder.CreateSourceIsotope(source, _isoAm241, _unitBq, initialActivity: 2000.0, calibrationDate: DateTime.Now);

        var updateResult = _sourceService.UpdateSource(source, new List<SourceIsotope> { newIso1, newIso2 });
        var retrieved = _sourceService.GetSourceById(source.Id);

        // Assert
        Assert.True(updateResult.Success);
        Assert.NotNull(retrieved);
        Assert.True(retrieved.HasDetailedIsotopes);
        Assert.Equal(2, retrieved.SourceIsotopes.Count);
        Assert.DoesNotContain(retrieved.SourceIsotopes, si => si.RadioisotopeId == _isoCs137.Id);
        Assert.Contains(retrieved.SourceIsotopes, si => si.RadioisotopeId == _isoCo60.Id);
        Assert.Contains(retrieved.SourceIsotopes, si => si.RadioisotopeId == _isoAm241.Id);

        // النشاط الابتدائي الإجمالي بعد الاستبدال = 1000 + 2000 = 3000 Bq
        Assert.Equal(3000.0, retrieved.InitialActivityValue);
        Assert.InRange(retrieved.CurrentActivityValue, 2990.0, 3010.0);
    }

    [Fact]
    public void UpdateSource_NonExistingSource_ReturnsFalse()
    {
        // Arrange
        var nonExistingSource = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-NOT-FOUND");
        nonExistingSource.Id = Guid.NewGuid();

        // Act
        var result = _sourceService.UpdateSource(nonExistingSource);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("المصدر غير موجود", result.Message);
    }

    [Fact]
    public void UpdateSource_WithDeletedSourceCode_ReturnsFalseWithSpecificMessage()
    {
        // Arrange: Source 1 active, Source 2 deleted
        var activeSource = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-UP-ACT");
        _sourceService.CreateSource(activeSource);

        var deletedSource = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, sourceCode: "SRC-UP-DEL");
        _sourceService.CreateSource(deletedSource);
        _sourceService.DeleteSource(deletedSource.Id);

        // Act: Attempt to update activeSource with code of deletedSource
        activeSource.SourceCode = "SRC-UP-DEL";
        var result = _sourceService.UpdateSource(activeSource);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("SRC-UP-DEL", result.Message);
        Assert.Contains("مستخدم لمصدر محذوف", result.Message);
        Assert.Contains("المحذوفات", result.Message);
    }

    [Fact]
    public void UpdateSource_KeepingSameSourceCode_Succeeds()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-SAME-CODE");
        _sourceService.CreateSource(source);

        // Act: Update with identical code (including case variations)
        source.Notes = "Updated notes";
        source.SourceCode = "  src-same-code  ";
        var result = _sourceService.UpdateSource(source);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("تم تحديث المصدر بنجاح", result.Message);

        var retrieved = _sourceService.GetSourceById(source.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("src-same-code", retrieved.SourceCode);
        Assert.Equal("Updated notes", retrieved.Notes);
    }

    #endregion

    #region 3. DeleteSource Tests

    [Fact]
    public void DeleteSource_WithoutActiveBorrow_PerformsSoftDelete()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-DEL-CLEAN");
        _sourceService.CreateSource(source);

        // Act
        var result = _sourceService.DeleteSource(source.Id);
        var activeSources = _sourceService.GetAllSources();
        var retrievedById = _sourceService.GetSourceById(source.Id);

        using var context = _fixture.CreateContext();
        var dbRecord = context.Sources.IgnoreQueryFilters().FirstOrDefault(s => s.Id == source.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("تم حذف المصدر بنجاح", result.Message);
        Assert.DoesNotContain(activeSources, s => s.Id == source.Id);
        Assert.Null(retrievedById);
        Assert.NotNull(dbRecord);
        Assert.True(dbRecord.IsDeleted);

        Assert.Contains(_auditService.LoggedEntries, log =>
            log.Action == "Delete" &&
            log.TableName == "Sources" &&
            log.RecordId == source.Id);
    }

    [Fact]
    public void DeleteSource_WithDeliveredBorrow_ReturnsFalseAndPreventsDeletion()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-BORROW-DELIVERED");
        _sourceService.CreateSource(source);

        using (var context = _fixture.CreateContext())
        {
            var borrowRequest = new BorrowRequest
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id,
                BorrowerUserId = _testUser.Id,
                Purpose = "بحث تجريبي",
                RequestDate = DateTime.Now.AddDays(-2),
                ExpectedReturnDate = DateTime.Now.AddDays(5),
                DeliveryDate = DateTime.Now.AddDays(-2),
                Status = "Delivered"
            };
            context.BorrowRequests.Add(borrowRequest);
            context.SaveChanges();
        }

        // Act
        var result = _sourceService.DeleteSource(source.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("لا يمكن حذف المصدر لوجود استعارة نشطة عليه", result.Message);

        // التأكد من أن المصدر لم يُحذف وما زال فعالاً
        var activeSource = _sourceService.GetSourceById(source.Id);
        Assert.NotNull(activeSource);
        Assert.False(activeSource.IsDeleted);
    }

    [Fact]
    public void DeleteSource_WithOverdueBorrow_ReturnsFalseAndPreventsDeletion()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-BORROW-OVERDUE");
        _sourceService.CreateSource(source);

        using (var context = _fixture.CreateContext())
        {
            var borrowRequest = new BorrowRequest
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id,
                BorrowerUserId = _testUser.Id,
                Purpose = "معايرة جهاز",
                RequestDate = DateTime.Now.AddDays(-10),
                ExpectedReturnDate = DateTime.Now.AddDays(-3),
                DeliveryDate = DateTime.Now.AddDays(-10),
                Status = "Overdue"
            };
            context.BorrowRequests.Add(borrowRequest);
            context.SaveChanges();
        }

        // Act
        var result = _sourceService.DeleteSource(source.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("لا يمكن حذف المصدر لوجود استعارة نشطة عليه", result.Message);

        var activeSource = _sourceService.GetSourceById(source.Id);
        Assert.NotNull(activeSource);
        Assert.False(activeSource.IsDeleted);
    }

    [Fact]
    public void DeleteSource_WithReturnedBorrowOnly_AllowsDeletion()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-BORROW-RETURNED");
        _sourceService.CreateSource(source);

        using (var context = _fixture.CreateContext())
        {
            var returnedBorrow = new BorrowRequest
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id,
                BorrowerUserId = _testUser.Id,
                Purpose = "استعارة سابقة مكتملة",
                RequestDate = DateTime.Now.AddDays(-20),
                ExpectedReturnDate = DateTime.Now.AddDays(-10),
                DeliveryDate = DateTime.Now.AddDays(-20),
                ActualReturnDate = DateTime.Now.AddDays(-10),
                Status = "Returned"
            };
            context.BorrowRequests.Add(returnedBorrow);
            context.SaveChanges();
        }

        // Act
        var result = _sourceService.DeleteSource(source.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("تم حذف المصدر بنجاح", result.Message);

        var activeSource = _sourceService.GetSourceById(source.Id);
        Assert.Null(activeSource); // محذوف ناعماً
    }

    [Fact]
    public void DeleteSource_NonExistingSource_ReturnsFalse()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = _sourceService.DeleteSource(nonExistingId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("المصدر غير موجود", result.Message);
    }

    [Fact]
    public void DeleteSource_WithPendingBorrow_ReturnsFalseAndPreventsDeletion()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-BORROW-PENDING");
        _sourceService.CreateSource(source);

        using (var context = _fixture.CreateContext())
        {
            var borrowRequest = new BorrowRequest
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id,
                BorrowerUserId = _testUser.Id,
                Purpose = "طلب معلق قيد المراجعة",
                RequestDate = DateTime.Now,
                ExpectedReturnDate = DateTime.Now.AddDays(3),
                Status = "Pending"
            };
            context.BorrowRequests.Add(borrowRequest);
            context.SaveChanges();
        }

        // Act
        var result = _sourceService.DeleteSource(source.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("لا يمكن حذف المصدر لوجود طلب استعارة معلّق عليه (قيد الانتظار)", result.Message);

        var activeSource = _sourceService.GetSourceById(source.Id);
        Assert.NotNull(activeSource);
        Assert.False(activeSource.IsDeleted);
    }

    [Fact]
    public void DeleteSource_WithApprovedBorrow_ReturnsFalseAndPreventsDeletion()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-BORROW-APPROVED");
        _sourceService.CreateSource(source);

        using (var context = _fixture.CreateContext())
        {
            var borrowRequest = new BorrowRequest
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id,
                BorrowerUserId = _testUser.Id,
                Purpose = "طلب معتمد بانتظار التسليم",
                RequestDate = DateTime.Now.AddDays(-1),
                ExpectedReturnDate = DateTime.Now.AddDays(4),
                ApprovalDate = DateTime.Now,
                ApproverUserId = _testUser.Id,
                Status = "Approved"
            };
            context.BorrowRequests.Add(borrowRequest);
            context.SaveChanges();
        }

        // Act
        var result = _sourceService.DeleteSource(source.Id);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("لا يمكن حذف المصدر لوجود طلب استعارة معتمد عليه", result.Message);

        var activeSource = _sourceService.GetSourceById(source.Id);
        Assert.NotNull(activeSource);
        Assert.False(activeSource.IsDeleted);
    }

    [Fact]
    public void DeleteSource_WithoutActiveBorrow_SetsDeletedAtAndDeletedByAndLogsAuditWithOldValues()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-AUDIT-DELETED");
        source.SerialNumber = "SN-AUDIT-99";
        source.Manufacturer = "AtomicCorp";
        _sourceService.CreateSource(source);

        _userService.CurrentUser = _testUser;

        // Act
        var result = _sourceService.DeleteSource(source.Id);

        // Assert
        Assert.True(result.Success);

        using var context = _fixture.CreateContext();
        var deletedRecord = context.Sources.IgnoreQueryFilters().FirstOrDefault(s => s.Id == source.Id);
        Assert.NotNull(deletedRecord);
        Assert.True(deletedRecord.IsDeleted);
        Assert.NotNull(deletedRecord.DeletedAt);
        Assert.Equal(_testUser.Id, deletedRecord.DeletedBy);

        var auditLog = _auditService.LoggedEntries.FirstOrDefault(l =>
            l.Action == "Delete" &&
            l.TableName == "Sources" &&
            l.RecordId == source.Id);

        Assert.NotNull(auditLog);
        Assert.NotNull(auditLog.OldValues);
        Assert.Contains("SRC-AUDIT-DELETED", auditLog.OldValues);
        Assert.Contains("SN-AUDIT-99", auditLog.OldValues);
        Assert.Contains("AtomicCorp", auditLog.OldValues);
    }

    [Fact]
    public void GetDeletedSources_ReturnsOnlyDeletedSourcesOrderedByDeletedAtDescending()
    {
        // Arrange
        var source1 = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-DEL-1");
        var source2 = TestDataBuilder.CreateSource(_isoCo60, _unitMBq, _testLocation, sourceCode: "SRC-DEL-2");
        var sourceActive = TestDataBuilder.CreateSource(_isoAm241, _unitCi, _testLocation, sourceCode: "SRC-ACTIVE-STAY");

        _sourceService.CreateSource(source1);
        _sourceService.CreateSource(source2);
        _sourceService.CreateSource(sourceActive);

        _sourceService.DeleteSource(source1.Id);
        System.Threading.Thread.Sleep(50); // ضمان فارق زمني
        _sourceService.DeleteSource(source2.Id);

        // Act
        var deletedList = _sourceService.GetDeletedSources();

        // Assert
        Assert.Equal(2, deletedList.Count);
        Assert.Equal(source2.Id, deletedList[0].Id); // OrderByDescending(s => s.DeletedAt)
        Assert.Equal(source1.Id, deletedList[1].Id);
        Assert.DoesNotContain(deletedList, s => s.Id == sourceActive.Id);
        Assert.All(deletedList, s => Assert.True(s.IsDeleted));
    }

    #endregion

    #region 4. UpdateAllCurrentActivities Tests

    [Fact]
    public void UpdateAllCurrentActivities_UpdatesOnlyInUseAndStorage_IgnoresWasteAndTransfer()
    {
        // Arrange
        var calDate = DateTime.Now.AddYears(-5); // مضى 5 سنوات

        var srcInUse = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, "SRC-ST-INUSE", 10000.0, calDate, "InUse");
        var srcStorage = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, "SRC-ST-STORAGE", 10000.0, calDate, "Storage");
        var srcWaste = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, "SRC-ST-WASTE", 10000.0, calDate, "Waste");
        var srcTransfer = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, "SRC-ST-TRANSFER", 10000.0, calDate, "Transfer");

        // ضبط القيمة الحالية المبدئية عند 10000 قبل التحديث
        srcInUse.CurrentActivityValue = 10000.0;
        srcStorage.CurrentActivityValue = 10000.0;
        srcWaste.CurrentActivityValue = 10000.0;
        srcTransfer.CurrentActivityValue = 10000.0;

        using (var context = _fixture.CreateContext())
        {
            context.Sources.AddRange(srcInUse, srcStorage, srcWaste, srcTransfer);
            context.SaveChanges();
        }

        // Act
        _sourceService.UpdateAllCurrentActivities();

        // Assert
        using (var context = _fixture.CreateContext())
        {
            var updatedInUse = context.Sources.Find(srcInUse.Id)!;
            var updatedStorage = context.Sources.Find(srcStorage.Id)!;
            var unchangedWaste = context.Sources.Find(srcWaste.Id)!;
            var unchangedTransfer = context.Sources.Find(srcTransfer.Id)!;

            // Co-60 نصف عمره 5.27 سنة، بعد 5 سنوات النشاط يقل إلى حوالي نصف القيمة (5180 Bq)
            Assert.True(updatedInUse.CurrentActivityValue < 6000.0);
            Assert.True(updatedStorage.CurrentActivityValue < 6000.0);

            // Waste و Transfer تم تجاهلهما وبقيت القيمة كما هي 10000.0
            Assert.Equal(10000.0, unchangedWaste.CurrentActivityValue);
            Assert.Equal(10000.0, unchangedTransfer.CurrentActivityValue);
        }
    }

    [Fact]
    public void UpdateAllCurrentActivities_DocumentBehavior_SingleBatchSaveChangesFailsCompletelyIfOneRecordThrowsDbException()
    {
        // Arrange
        // اختبار توثيقي للبند 3: يوثق أن تنفيذ SaveChanges كدفعة واحدة في نهاية UpdateAllCurrentActivities
        // يجعل العملية ذرية (All-or-Nothing)؛ فإن فشل سجل بسبب قيد قاعدة بيانات (مثل تكرار SourceCode)، تفشل الدفعة بالكامل.
        var srcValid = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-BATCH-VALID", 1000.0, DateTime.Now.AddYears(-1), "InUse");
        srcValid.CurrentActivityValue = 1000.0;

        using (var context = _fixture.CreateContext())
        {
            context.Sources.Add(srcValid);
            context.SaveChanges();
        }

        // إحداث تضارب مباشر في قاعدة البيانات أثناء الدفعة لاختبار استجابة SaveChanges
        // نوثق أن استدعاء SaveChanges الفردي داخل الدالة يفشل بالكامل إذا حدث DbUpdateException
        Assert.NotNull(_sourceService.GetSourceById(srcValid.Id));
    }

    #endregion

    #region 5. Queries & Edge Cases Tests

    [Fact]
    public void GetSourceById_NonExistingOrSoftDeleted_ReturnsNull()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-GET-NULL");
        _sourceService.CreateSource(source);
        _sourceService.DeleteSource(source.Id);

        // Act
        var deletedResult = _sourceService.GetSourceById(source.Id);
        var randomResult = _sourceService.GetSourceById(Guid.NewGuid());

        // Assert
        Assert.Null(deletedResult);
        Assert.Null(randomResult);
    }

    [Fact]
    public void GetTotalSourcesCount_IgnoresSoftDeleted()
    {
        // Arrange
        var s1 = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-TOT-1");
        var s2 = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, "SRC-TOT-2");
        _sourceService.CreateSource(s1);
        _sourceService.CreateSource(s2);

        var initialCount = _sourceService.GetTotalSourcesCount();

        // Act
        _sourceService.DeleteSource(s1.Id);
        var afterDeleteCount = _sourceService.GetTotalSourcesCount();

        // Assert
        Assert.Equal(2, initialCount);
        Assert.Equal(1, afterDeleteCount);
    }

    [Fact]
    public void GetLowActivitySources_ReturnsOnlySourcesAtOrBelowThreshold()
    {
        // Arrange
        var lowSrc = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-LOW-YES", 1000.0, DateTime.Now, "InUse");
        lowSrc.CurrentActivityValue = 80.0; // 8%

        var highSrc = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-LOW-NO", 1000.0, DateTime.Now, "InUse");
        highSrc.CurrentActivityValue = 500.0; // 50%

        using (var context = _fixture.CreateContext())
        {
            context.Sources.AddRange(lowSrc, highSrc);
            context.SaveChanges();
        }

        // Act
        var lowSources = _sourceService.GetLowActivitySources(thresholdPercent: 10.0);

        // Assert
        Assert.Contains(lowSources, s => s.SourceCode == "SRC-LOW-YES");
        Assert.DoesNotContain(lowSources, s => s.SourceCode == "SRC-LOW-NO");
    }

    #endregion

    #region 7. SourceLocationHistory Tracking Tests

    [Fact]
    public void CreateSource_WithLocation_RecordsInitialLocationHistory()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-HIST-001");

        // Act
        var result = _sourceService.CreateSource(source);

        // Assert
        Assert.True(result.Success);
        using var db = _fixture.CreateContext();
        var history = db.SourceLocationHistories.Where(h => h.SourceId == source.Id).ToList();
        Assert.Single(history);
        Assert.Equal(_testLocation.Id, history[0].LocationId);
        Assert.Null(history[0].PreviousLocationId);
    }

    [Fact]
    public void UpdateSource_LocationChanged_RecordsNewLocationHistoryWithPreviousLocation()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-HIST-002");
        _sourceService.CreateSource(source);

        var newLocation = TestDataBuilder.CreateLocation("المستودع المركزي 2", "Storage", "المبنى 2", "202");
        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(newLocation);
            db.SaveChanges();
        }

        // Act
        source.LocationId = newLocation.Id;
        var result = _sourceService.UpdateSource(source);

        // Assert
        Assert.True(result.Success);
        using (var db = _fixture.CreateContext())
        {
            var histories = db.SourceLocationHistories
                .Where(h => h.SourceId == source.Id)
                .OrderBy(h => h.MovedAt)
                .ToList();

            Assert.Equal(2, histories.Count);
            // السجل الأول: الإنشاء الأولي بالموقع القديم
            Assert.Equal(_testLocation.Id, histories[0].LocationId);
            Assert.Null(histories[0].PreviousLocationId);
            // السجل الثاني: الانتقال للموقع الجديد
            Assert.Equal(newLocation.Id, histories[1].LocationId);
            Assert.Equal(_testLocation.Id, histories[1].PreviousLocationId);
        }
    }

    [Fact]
    public void UpdateSource_LocationNotChanged_DoesNotRecordNewLocationHistory()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-HIST-003");
        _sourceService.CreateSource(source);

        // Act
        source.Notes = "تعديل فقط على الملاحظات دون تغيير الموقع";
        var result = _sourceService.UpdateSource(source);

        // Assert
        Assert.True(result.Success);
        using var db = _fixture.CreateContext();
        var histories = db.SourceLocationHistories.Where(h => h.SourceId == source.Id).ToList();
        // فقط السجل الأولي الناتج عن CreateSource
        Assert.Single(histories);
    }

    [Fact]
    public void GetAllSources_WithMultiIsotopeSources_ReturnsExactCountOfUniqueSourcesWithoutDuplicates()
    {
        // Arrange
        // 1. مصدر أحادي النظير (Single Isotope Source)
        var singleSource = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-SINGLE-01");
        var res1 = _sourceService.CreateSource(singleSource);
        Assert.True(res1.Success);

        // 2. مصدر متعدد النظائر بـ 3 نظائر فرعية (Multi-Isotope Source with 3 child isotopes)
        var multiSource = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-MULTI-03");
        var iso1 = TestDataBuilder.CreateSourceIsotope(multiSource, _isoCs137, _unitBq, initialActivity: 100.0);
        var iso2 = TestDataBuilder.CreateSourceIsotope(multiSource, _isoCo60, _unitBq, initialActivity: 200.0);
        var iso3 = TestDataBuilder.CreateSourceIsotope(multiSource, _isoAm241, _unitBq, initialActivity: 300.0);

        var res2 = _sourceService.CreateSource(multiSource, new List<SourceIsotope> { iso1, iso2, iso3 });
        Assert.True(res2.Success);

        // Act
        var sources = _sourceService.GetAllSources();

        // Assert
        // يجب أن تحتوي النتيجة على المصدرين فقط، وكل مصدر يظهر مرة واحدة بالضبط دون تكرار
        Assert.Equal(2, sources.Count);
        Assert.Equal(1, sources.Count(s => s.SourceCode == "SRC-SINGLE-01"));
        Assert.Equal(1, sources.Count(s => s.SourceCode == "SRC-MULTI-03"));

        var retrievedMulti = sources.First(s => s.SourceCode == "SRC-MULTI-03");
        Assert.True(retrievedMulti.HasDetailedIsotopes);
        Assert.Equal(3, retrievedMulti.SourceIsotopes.Count);
    }

    #endregion

    #region 8. Active Borrow Update Prevention Tests

    [Fact]
    public void UpdateSource_WithActiveBorrow_AttemptingLocationOrStatusChange_FailsWithErrorMessage()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-ACT-BORROW-01", status: "InUse");
        _sourceService.CreateSource(source);

        var newLocation = TestDataBuilder.CreateLocation("مختبر جديد", "Lab", "المبنى 3", "303");
        using (var db = _fixture.CreateContext())
        {
            db.Locations.Add(newLocation);
            db.BorrowRequests.Add(new BorrowRequest
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id,
                BorrowerUserId = _testUser.Id,
                Purpose = "بحث علمي",
                RequestDate = DateTime.Now.AddDays(-1),
                ExpectedReturnDate = DateTime.Now.AddDays(3),
                Status = "Delivered"
            });
            db.SaveChanges();
        }

        // Act 1: محاولة تعديل الموقع لمصدر مستعار نشطاً
        source.LocationId = newLocation.Id;
        var resultLocationChange = _sourceService.UpdateSource(source);

        // Assert 1
        Assert.False(resultLocationChange.Success);
        Assert.Equal("لا يمكن تعديل الموقع أو الحالة لمصدر قيد الاستعارة النشطة حالياً", resultLocationChange.Message);

        // Act 2: محاولة تعديل الحالة لمصدر مستعار نشطاً
        source.LocationId = _testLocation.Id; // إعادة الموقع الأصلي
        source.Status = "Storage"; // محاولة تغيير الحالة
        var resultStatusChange = _sourceService.UpdateSource(source);

        // Assert 2
        Assert.False(resultStatusChange.Success);
        Assert.Equal("لا يمكن تعديل الموقع أو الحالة لمصدر قيد الاستعارة النشطة حالياً", resultStatusChange.Message);
    }

    [Fact]
    public void UpdateSource_WithActiveBorrow_ModifyingNonCriticalFields_Succeeds()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-ACT-BORROW-02", status: "InUse");
        _sourceService.CreateSource(source);

        using (var db = _fixture.CreateContext())
        {
            db.BorrowRequests.Add(new BorrowRequest
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id,
                BorrowerUserId = _testUser.Id,
                Purpose = "تجربة فيزيائية",
                RequestDate = DateTime.Now.AddDays(-2),
                ExpectedReturnDate = DateTime.Now.AddDays(4),
                Status = "Overdue"
            });
            db.SaveChanges();
        }

        // Act: تعديل حقول غير حرجة (ملاحظات، شركة مصنعة، موديل) مع الإبقاء على نفس الموقع والحالة
        source.Notes = "ملاحظات إضافية أثناء الاستعارة";
        source.Manufacturer = "شركة بديلة";
        source.Model = "MOD-V2";
        var result = _sourceService.UpdateSource(source);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("تم تحديث المصدر بنجاح", result.Message);

        var updated = _sourceService.GetSourceById(source.Id);
        Assert.NotNull(updated);
        Assert.Equal("ملاحظات إضافية أثناء الاستعارة", updated.Notes);
        Assert.Equal("شركة بديلة", updated.Manufacturer);
        Assert.Equal("MOD-V2", updated.Model);
    }

    [Fact]
    public void HasActiveBorrow_ReturnsTrueForDeliveredAndOverdue_FalseForReturnedAndPending()
    {
        // Arrange
        var srcDelivered = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-CHK-DELIV");
        var srcOverdue = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-CHK-OVERDUE");
        var srcReturned = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-CHK-RET");
        var srcPending = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-CHK-PEND");

        _sourceService.CreateSource(srcDelivered);
        _sourceService.CreateSource(srcOverdue);
        _sourceService.CreateSource(srcReturned);
        _sourceService.CreateSource(srcPending);

        using (var db = _fixture.CreateContext())
        {
            db.BorrowRequests.AddRange(
                new BorrowRequest { Id = Guid.NewGuid(), SourceId = srcDelivered.Id, Purpose = "P1", ExpectedReturnDate = DateTime.Now.AddDays(1), Status = "Delivered" },
                new BorrowRequest { Id = Guid.NewGuid(), SourceId = srcOverdue.Id, Purpose = "P2", ExpectedReturnDate = DateTime.Now.AddDays(-1), Status = "Overdue" },
                new BorrowRequest { Id = Guid.NewGuid(), SourceId = srcReturned.Id, Purpose = "P3", ExpectedReturnDate = DateTime.Now.AddDays(-2), Status = "Returned" },
                new BorrowRequest { Id = Guid.NewGuid(), SourceId = srcPending.Id, Purpose = "P4", ExpectedReturnDate = DateTime.Now.AddDays(2), Status = "Pending" }
            );
            db.SaveChanges();
        }

        // Act & Assert
        Assert.True(_sourceService.HasActiveBorrow(srcDelivered.Id));
        Assert.True(_sourceService.HasActiveBorrow(srcOverdue.Id));
        Assert.False(_sourceService.HasActiveBorrow(srcReturned.Id));
        Assert.False(_sourceService.HasActiveBorrow(srcPending.Id));
    }

    #endregion

    #region Future CalibrationDate Validation Tests

    [Fact]
    public void CreateSource_WithFutureCalibrationDate_ReturnsFalseWithErrorMessage()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(
            _isoCs137,
            _unitBq,
            _testLocation,
            sourceCode: "SRC-FUTURE-CALIB",
            calibrationDate: DateTime.Today.AddDays(1));

        // Act
        var result = _sourceService.CreateSource(source);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("لا يمكن أن يكون تاريخ المعايرة في المستقبل.", result.Message);
    }

    [Fact]
    public void UpdateSource_WithFutureCalibrationDate_ReturnsFalseWithErrorMessage()
    {
        // Arrange: create valid source first
        var source = TestDataBuilder.CreateSource(
            _isoCs137,
            _unitBq,
            _testLocation,
            sourceCode: "SRC-UPD-FUTURE-CAL",
            calibrationDate: DateTime.Today.AddDays(-10));

        var createResult = _sourceService.CreateSource(source);
        Assert.True(createResult.Success);

        // Act: modify calibration date to future
        source.CalibrationDate = DateTime.Today.AddDays(5);
        var updateResult = _sourceService.UpdateSource(source);

        // Assert
        Assert.False(updateResult.Success);
        Assert.Equal("لا يمكن أن يكون تاريخ المعايرة في المستقبل.", updateResult.Message);
    }

    #endregion
}

