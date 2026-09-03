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

public class SourceRepositoryTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly DecayCalculationService _decayService;
    private readonly FakeAuditService _auditService;
    private readonly FakeUserService _userService;
    private readonly SourceService _sourceService;

    // كيانات أساسية مشتركة في الاختبارات
    private Radioisotope _isoCs137 = null!;
    private Radioisotope _isoCo60 = null!;
    private Radioisotope _isoAm241 = null!;
    private ActivityUnit _unitBq = null!;
    private ActivityUnit _unitCi = null!;
    private Location _testLocation = null!;

    public SourceRepositoryTests(SqliteInMemoryFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();

        _decayService = new DecayCalculationService();
        _auditService = new FakeAuditService();
        _userService = new FakeUserService();

        _sourceService = new SourceService(
            _fixture.ContextFactory,
            _decayService,
            _auditService,
            _userService);

        SeedCommonLookupData();
    }

    private void SeedCommonLookupData()
    {
        using var context = _fixture.CreateContext();

        if (_userService.CurrentUser != null)
        {
            var roleId = _userService.CurrentUser.RoleId != Guid.Empty ? _userService.CurrentUser.RoleId : Guid.NewGuid();
            var role = new Role { Id = roleId, RoleName = "مدير النظام", Permissions = "All" };
            _userService.CurrentUser.RoleId = role.Id;
            _userService.CurrentUser.Role = role;
            _userService.CurrentUser.Permissions = "All";
            _userService.CurrentUser.IsEditor = true;
            context.Roles.Add(role);
            context.Users.Add(_userService.CurrentUser);
        }

        _isoCs137 = TestDataBuilder.CreateRadioisotope("Cs-137", "Cesium-137", 30.08, "years", 661.7);
        _isoCo60 = TestDataBuilder.CreateRadioisotope("Co-60", "Cobalt-60", 5.27, "years", 1332.5);
        _isoAm241 = TestDataBuilder.CreateRadioisotope("Am-241", "Americium-241", 432.2, "years", 59.54);

        _unitBq = TestDataBuilder.CreateActivityUnit("Becquerel", "Bq", 1.0);
        _unitCi = TestDataBuilder.CreateActivityUnit("Curie", "Ci", 3.7e10);

        _testLocation = TestDataBuilder.CreateLocation("مختبر النظائر 1", "Lab", "المبنى الرئيسي", "101");

        context.Radioisotopes.AddRange(_isoCs137, _isoCo60, _isoAm241);
        context.ActivityUnits.AddRange(_unitBq, _unitCi);
        context.Locations.Add(_testLocation);

        context.SaveChanges();
    }

    public void Dispose()
    {
        // تنظيف بعد كل اختبار إن لزم
    }

    #region أ. عمليات أساسية (CRUD Operations)

    [Fact]
    public void CreateSource_ValidSource_PersistsAndRetrievedById()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(
            _isoCs137,
            _unitBq,
            _testLocation,
            sourceCode: "SRC-CRUD-001",
            initialActivity: 50000.0);

        // Act
        var result = _sourceService.CreateSource(source);
        var retrieved = _sourceService.GetSourceById(source.Id);

        // Assert
        Assert.True(result.Success, $"فشل إنشاء المصدر: {result.Message}");
        Assert.NotNull(retrieved);
        Assert.Equal("SRC-CRUD-001", retrieved.SourceCode);
        Assert.Equal(_isoCs137.Id, retrieved.RadioisotopeId);
        Assert.Equal(_testLocation.Id, retrieved.LocationId);
        Assert.Equal(50000.0, retrieved.InitialActivityValue);
        Assert.Equal(_userService.CurrentUser!.Id, retrieved.AddedBy);
        Assert.Equal(_userService.CurrentUser!.FullName, retrieved.AddedByName);

        // التحقق من تسجيل العملية في الـ Audit Log
        Assert.Contains(_auditService.LoggedEntries, log =>
            log.Action == "Create" &&
            log.TableName == "Sources" &&
            log.RecordId == source.Id);
    }

    [Fact]
    public void CreateSource_DuplicateSourceCode_ReturnsFalseAndDoesNotInsert()
    {
        // Arrange
        var source1 = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-DUP-UNIQUE");
        var source2 = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, sourceCode: "SRC-DUP-UNIQUE");

        // Act
        var result1 = _sourceService.CreateSource(source1);
        var result2 = _sourceService.CreateSource(source2);
        var totalCount = _sourceService.GetTotalSourcesCount();

        // Assert
        Assert.True(result1.Success);
        Assert.False(result2.Success);
        Assert.Equal("كود المصدر موجود بالفعل", result2.Message);
        Assert.Equal(1, totalCount);
    }

    [Fact]
    public void UpdateSource_ExistingSource_UpdatesPropertiesCorrectly()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-UPDATE-001");
        _sourceService.CreateSource(source);

        // Act
        source.SerialNumber = "SN-UPDATED-999";
        source.Manufacturer = "New Manufacturer Ltd";
        source.Model = "Model-2026-PRO";
        source.Notes = "تم تحديث الملاحظات بنجاح";
        source.Status = "Storage";

        var updateResult = _sourceService.UpdateSource(source);
        var retrieved = _sourceService.GetSourceById(source.Id);

        // Assert
        Assert.True(updateResult.Success);
        Assert.NotNull(retrieved);
        Assert.Equal("SN-UPDATED-999", retrieved.SerialNumber);
        Assert.Equal("New Manufacturer Ltd", retrieved.Manufacturer);
        Assert.Equal("Model-2026-PRO", retrieved.Model);
        Assert.Equal("تم تحديث الملاحظات بنجاح", retrieved.Notes);
        Assert.Equal("Storage", retrieved.Status);

        // التحقق من تسجيل عملية التعديل
        Assert.Contains(_auditService.LoggedEntries, log =>
            log.Action == "Update" &&
            log.TableName == "Sources" &&
            log.RecordId == source.Id);
    }

    [Fact]
    public void DeleteSource_ExistingSource_PerformsSoftDeleteAndHiddenFromGetAll()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-DELETE-001");
        _sourceService.CreateSource(source);

        // Act
        var deleteResult = _sourceService.DeleteSource(source.Id);
        var activeSources = _sourceService.GetAllSources();
        var retrievedById = _sourceService.GetSourceById(source.Id);

        // استعلام مباشر مع تجاوز فلتر الحذف للتأكد من وجود السجل في قاعدة البيانات
        using var context = _fixture.CreateContext();
        var directFromDb = context.Sources.IgnoreQueryFilters().FirstOrDefault(s => s.Id == source.Id);

        // Assert
        Assert.True(deleteResult.Success);
        Assert.DoesNotContain(activeSources, s => s.Id == source.Id);
        Assert.Null(retrievedById); // مفلتر بـ Global Query Filter
        Assert.NotNull(directFromDb);
        Assert.True(directFromDb.IsDeleted, "يجب أن تكون قيمة IsDeleted تساوي true");

        // التحقق من تسجيل الحذف
        Assert.Contains(_auditService.LoggedEntries, log =>
            log.Action == "Delete" &&
            log.TableName == "Sources" &&
            log.RecordId == source.Id);
    }

    #endregion

    #region ب. سلامة العلاقات (SourceIsotope Relationships)

    [Fact]
    public void CreateSource_WithMultipleIsotopes_PersistsAllIsotopesWithCorrectSourceId()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(
            _isoCs137,
            _unitBq,
            _testLocation,
            sourceCode: "SRC-MULTI-001",
            hasDetailedIsotopes: true);

        var isotope1 = TestDataBuilder.CreateSourceIsotope(source, _isoCs137, _unitBq, initialActivity: 1000.0);
        var isotope2 = TestDataBuilder.CreateSourceIsotope(source, _isoCo60, _unitBq, initialActivity: 2000.0);

        var isotopesList = new List<SourceIsotope> { isotope1, isotope2 };

        // Act
        var result = _sourceService.CreateSource(source, isotopesList);
        var retrieved = _sourceService.GetSourceById(source.Id);

        using var context = _fixture.CreateContext();
        var dbIsotopes = context.SourceIsotopes.Where(si => si.SourceId == source.Id).ToList();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(retrieved);
        Assert.True(retrieved.HasDetailedIsotopes);
        Assert.Equal(2, retrieved.SourceIsotopes.Count);
        Assert.Equal(2, dbIsotopes.Count);
        Assert.All(dbIsotopes, si => Assert.Equal(source.Id, si.SourceId));
        Assert.True(retrieved.CurrentActivityValue > 0);
    }

    [Fact]
    public void DeleteSource_HardDeleteOnDbContext_CascadesAndDeletesSourceIsotopes()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-CASCADE-001");
        var isotope1 = TestDataBuilder.CreateSourceIsotope(source, _isoCs137, _unitBq, initialActivity: 500.0);
        var isotope2 = TestDataBuilder.CreateSourceIsotope(source, _isoCo60, _unitBq, initialActivity: 600.0);

        _sourceService.CreateSource(source, new List<SourceIsotope> { isotope1, isotope2 });

        using (var context = _fixture.CreateContext())
        {
            var initialIsotopesCount = context.SourceIsotopes.Count(si => si.SourceId == source.Id);
            Assert.Equal(2, initialIsotopesCount);

            // Act - حذف فعلي للمصدر عبر الـ DbContext لاختبار قيد الـ Cascade في SQLite
            var dbSource = context.Sources.Find(source.Id);
            Assert.NotNull(dbSource);
            context.Sources.Remove(dbSource);
            context.SaveChanges();
        }

        // Assert - التأكد من حذف جميع سجلات SourceIsotopes المرتبطة تلقائياً بفعل Cascade Delete
        using (var context = _fixture.CreateContext())
        {
            var remainingIsotopesCount = context.SourceIsotopes.Count(si => si.SourceId == source.Id);
            Assert.Equal(0, remainingIsotopesCount);
        }
    }

    [Fact]
    public void DeleteRadioisotope_WhenUsedInSourceIsotope_FailsWithRestrictConstraint()
    {
        // Arrange
        var dedicatedIsotope = TestDataBuilder.CreateRadioisotope("Ir-192", "Iridium-192", 73.83, "days", 316.5);
        using (var context = _fixture.CreateContext())
        {
            context.Radioisotopes.Add(dedicatedIsotope);
            context.SaveChanges();
        }

        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-RESTRICT-001");
        var sourceIsotope = TestDataBuilder.CreateSourceIsotope(source, dedicatedIsotope, _unitBq, initialActivity: 300.0);

        _sourceService.CreateSource(source, new List<SourceIsotope> { sourceIsotope });

        // Act & Assert
        // عند محاولة حذف النظير وهو مستخدم داخل SourceIsotope، يجب أن تفشل العملية بسبب Restrict Foreign Key
        using (var context = _fixture.CreateContext())
        {
            var isoToDelete = context.Radioisotopes.Find(dedicatedIsotope.Id);
            Assert.NotNull(isoToDelete);
            context.Radioisotopes.Remove(isoToDelete);

            // يتوقع حدوث DbUpdateException ناتج عن خرق قيد المفتاح الأجنبي (FOREIGN KEY constraint failed)
            Assert.Throws<DbUpdateException>(() => context.SaveChanges());
        }
    }

    [Fact]
    public void SourceIsotope_DuplicateSameSourceAndIsotope_AllowedByCurrentDesign()
    {
        // Arrange & Act
        // اختبار توثيقي: يوثق أن التصميم الحالي يسمح بإضافة نظيرين لنفس النظير في نفس المصدر
        // لعدم وجود Composite Unique Constraint على (SourceId, RadioisotopeId).
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-DOC-DUP-ISO");
        var isotopeFirst = TestDataBuilder.CreateSourceIsotope(source, _isoCs137, _unitBq, initialActivity: 100.0);
        var isotopeDuplicate = TestDataBuilder.CreateSourceIsotope(source, _isoCs137, _unitBq, initialActivity: 200.0);

        var result = _sourceService.CreateSource(source, new List<SourceIsotope> { isotopeFirst, isotopeDuplicate });

        // Assert
        Assert.True(result.Success);
        using var context = _fixture.CreateContext();
        var duplicatesCount = context.SourceIsotopes
            .Count(si => si.SourceId == source.Id && si.RadioisotopeId == _isoCs137.Id);

        Assert.Equal(2, duplicatesCount);
    }

    [Fact]
    public void UpdateSource_ReplacingIsotopesList_RemovesOldAndAddsNewWithoutDuplicates()
    {
        // Arrange
        var source = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-REPLACE-ISO");
        var initialIsotope = TestDataBuilder.CreateSourceIsotope(source, _isoCs137, _unitBq, initialActivity: 1000.0);
        _sourceService.CreateSource(source, new List<SourceIsotope> { initialIsotope });

        // Act - استبدال النظير بنظيرين جديدين (Co-60 و Am-241)
        var newIsotope1 = TestDataBuilder.CreateSourceIsotope(source, _isoCo60, _unitBq, initialActivity: 500.0);
        var newIsotope2 = TestDataBuilder.CreateSourceIsotope(source, _isoAm241, _unitBq, initialActivity: 750.0);

        var updateResult = _sourceService.UpdateSource(source, new List<SourceIsotope> { newIsotope1, newIsotope2 });
        var retrieved = _sourceService.GetSourceById(source.Id);

        using var context = _fixture.CreateContext();
        var allDbIsotopes = context.SourceIsotopes.Where(si => si.SourceId == source.Id).ToList();

        // Assert
        Assert.True(updateResult.Success);
        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved.SourceIsotopes.Count);
        Assert.Equal(2, allDbIsotopes.Count);

        // التأكد من عدم وجود النظير القديم Cs-137
        Assert.DoesNotContain(allDbIsotopes, si => si.RadioisotopeId == _isoCs137.Id);
        // التأكد من وجود النظيرين الجديدين
        Assert.Contains(allDbIsotopes, si => si.RadioisotopeId == _isoCo60.Id);
        Assert.Contains(allDbIsotopes, si => si.RadioisotopeId == _isoAm241.Id);
    }

    #endregion

    #region ج. صحة استعلامات القراءة (Read Queries)

    [Fact]
    public void GetAllSources_ReturnsSourcesOrderedByCreatedAtDescending_WithAllNavigationsLoaded()
    {
        // Arrange
        var now = DateTime.Now;
        var sourceOld = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, sourceCode: "SRC-ORDER-1");
        sourceOld.CreatedAt = now.AddDays(-10);

        var sourceMid = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, sourceCode: "SRC-ORDER-2");
        sourceMid.CreatedAt = now.AddDays(-5);

        var sourceNew = TestDataBuilder.CreateSource(_isoAm241, _unitBq, _testLocation, sourceCode: "SRC-ORDER-3");
        sourceNew.CreatedAt = now;

        var sourceIsotope = TestDataBuilder.CreateSourceIsotope(sourceNew, _isoAm241, _unitBq);

        _sourceService.CreateSource(sourceOld);
        _sourceService.CreateSource(sourceMid);
        _sourceService.CreateSource(sourceNew, new List<SourceIsotope> { sourceIsotope });

        // Act
        var sources = _sourceService.GetAllSources();

        // Assert
        Assert.True(sources.Count >= 3);
        // التحقق من الترتيب التنازلي حسب CreatedAt
        var ourSources = sources
            .Where(s => s.SourceCode.StartsWith("SRC-ORDER-"))
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        Assert.Equal("SRC-ORDER-3", ourSources[0].SourceCode);
        Assert.Equal("SRC-ORDER-2", ourSources[1].SourceCode);
        Assert.Equal("SRC-ORDER-1", ourSources[2].SourceCode);

        // التحقق من تحميل جميع الكيانات المرتبطة (Eager Loading)
        Assert.All(sources, s =>
        {
            Assert.NotNull(s.Radioisotope);
            Assert.NotNull(s.InitialActivityUnit);
            Assert.NotNull(s.CurrentActivityUnit);
            Assert.NotNull(s.Location);
        });

        // التحقق من تحميل Navigation للنظائر المتعددة
        var detailedSource = sources.First(s => s.SourceCode == "SRC-ORDER-3");
        Assert.Single(detailedSource.SourceIsotopes);
        Assert.NotNull(detailedSource.SourceIsotopes.First().Radioisotope);
        Assert.NotNull(detailedSource.SourceIsotopes.First().ActivityUnit);
    }

    [Fact]
    public void GetLowActivitySources_FiltersAccuratelyAroundThreshold()
    {
        // Arrange
        // عتبة 10%:
        // srcLow (5%) -> مشمول
        var srcLow = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-LOW-5");
        srcLow.InitialActivityValue = 1000.0;
        srcLow.CurrentActivityValue = 50.0; // 5%
        srcLow.Status = "InUse";

        // srcExact (10%) -> مشمول (الشرط <= threshold)
        var srcExact = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-LOW-10");
        srcExact.InitialActivityValue = 1000.0;
        srcExact.CurrentActivityValue = 100.0; // 10.0%
        srcExact.Status = "Storage";

        // srcAbove (10.01%) -> غير مشمول
        var srcAbove = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-LOW-10-PLUS");
        srcAbove.InitialActivityValue = 1000.0;
        srcAbove.CurrentActivityValue = 100.1; // 10.01%
        srcAbove.Status = "InUse";

        // srcHigh (50%) -> غير مشمول
        var srcHigh = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-LOW-50");
        srcHigh.InitialActivityValue = 1000.0;
        srcHigh.CurrentActivityValue = 500.0; // 50%
        srcHigh.Status = "InUse";

        // srcWaste (1%) ولكن حالته Waste -> غير مشمول لأن الدالة تستعلم فقط InUse أو Storage
        var srcWaste = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-LOW-WASTE");
        srcWaste.InitialActivityValue = 1000.0;
        srcWaste.CurrentActivityValue = 10.0; // 1%
        srcWaste.Status = "Waste";

        using (var context = _fixture.CreateContext())
        {
            context.Sources.AddRange(srcLow, srcExact, srcAbove, srcHigh, srcWaste);
            context.SaveChanges();
        }

        // Act
        var lowActivitySources = _sourceService.GetLowActivitySources(thresholdPercent: 10.0);
        var lowSourceCodes = lowActivitySources.Select(s => s.SourceCode).ToList();

        // Assert
        Assert.Contains("SRC-LOW-5", lowSourceCodes);
        Assert.Contains("SRC-LOW-10", lowSourceCodes);
        Assert.DoesNotContain("SRC-LOW-10-PLUS", lowSourceCodes);
        Assert.DoesNotContain("SRC-LOW-50", lowSourceCodes);
        Assert.DoesNotContain("SRC-LOW-WASTE", lowSourceCodes);
    }

    [Fact]
    public void GetTotalSourcesCount_ExcludesSoftDeletedSources()
    {
        // Arrange
        var src1 = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-CNT-1");
        var src2 = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, "SRC-CNT-2");
        var src3 = TestDataBuilder.CreateSource(_isoAm241, _unitBq, _testLocation, "SRC-CNT-3");

        _sourceService.CreateSource(src1);
        _sourceService.CreateSource(src2);
        _sourceService.CreateSource(src3);

        var countBefore = _sourceService.GetTotalSourcesCount();

        // Act
        _sourceService.DeleteSource(src2.Id); // حذف ناعم للمصدر الثاني
        var countAfter = _sourceService.GetTotalSourcesCount();

        // Assert
        Assert.Equal(3, countBefore);
        Assert.Equal(2, countAfter);
    }

    #endregion

    #region د. اختبار Global Query Filter بشكل مباشر

    [Fact]
    public void GlobalQueryFilter_DirectQueryExcludesSoftDeleted_IgnoreQueryFiltersIncludesThem()
    {
        // Arrange
        var activeSource = TestDataBuilder.CreateSource(_isoCs137, _unitBq, _testLocation, "SRC-GQF-ACTIVE");
        var deletedSource = TestDataBuilder.CreateSource(_isoCo60, _unitBq, _testLocation, "SRC-GQF-DELETED");
        deletedSource.IsDeleted = true;

        using (var context = _fixture.CreateContext())
        {
            context.Sources.AddRange(activeSource, deletedSource);
            context.SaveChanges();
        }

        // Act
        using (var context = _fixture.CreateContext())
        {
            var regularQuery = context.Sources.Where(s => s.SourceCode.StartsWith("SRC-GQF-")).ToList();
            var unfilteredQuery = context.Sources.IgnoreQueryFilters().Where(s => s.SourceCode.StartsWith("SRC-GQF-")).ToList();

            // Assert
            Assert.Single(regularQuery);
            Assert.Equal("SRC-GQF-ACTIVE", regularQuery[0].SourceCode);

            Assert.Equal(2, unfilteredQuery.Count);
            Assert.Contains(unfilteredQuery, s => s.SourceCode == "SRC-GQF-ACTIVE" && !s.IsDeleted);
            Assert.Contains(unfilteredQuery, s => s.SourceCode == "SRC-GQF-DELETED" && s.IsDeleted);
        }
    }

    #endregion
}
