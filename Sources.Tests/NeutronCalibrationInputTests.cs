using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class NeutronCalibrationInputTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly IServiceProvider _sp;
    private readonly Guid _typeId = Guid.NewGuid();
    private readonly Guid _locId = Guid.NewGuid();

    public NeutronCalibrationInputTests()
    {
        _fixture = new SqliteInMemoryFixture();

        using (var db = _fixture.CreateContext())
        {
            db.NeutronSourceTypes.Add(new NeutronSourceType
            {
                Id = _typeId,
                Code = "Am-241/Be-Test",
                NameAr = "أمريسيوم-بريليوم",
                NameEn = "Am-Be",
                ReactionType = "(α,n)",
                HalfLife = 432.2,
                HalfLifeUnit = "years",
                MeanNeutronEnergyMeV = 4.2
            });
            db.Locations.Add(new Location
            {
                Id = _locId,
                LocationName = "Calibration Lab"
            });
            db.SaveChanges();
        }

        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(_fixture.ContextFactory);
        _sp = services.BuildServiceProvider();
        typeof(App).GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, _sp);
    }

    public void Dispose()
    {
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Reset();
        _fixture.Dispose();
    }

    private SourcesViewModel CreateViewModel(
        INeutronSourceService neutronService,
        INeutronSourceTypeService? typeService = null)
    {
        var mockSourceService = new Mock<ISourceService>();
        var mockIsotopeService = new Mock<IRadioisotopeService>();
        var mockLocationService = new Mock<ILocationService>();
        var mockReportingService = new Mock<IReportingService>();

        mockLocationService.Setup(l => l.GetAll()).Returns(new List<Location>
        {
            new Location { Id = _locId, LocationName = "Calibration Lab" }
        });

        if (typeService == null)
        {
            var mockTypeService = new Mock<INeutronSourceTypeService>();
            mockTypeService.Setup(t => t.GetAll()).Returns(new List<NeutronSourceType>
            {
                new NeutronSourceType { Id = _typeId, Code = "Am-241/Be-Test" }
            });
            typeService = mockTypeService.Object;
        }

        return new SourcesViewModel(
            mockSourceService.Object,
            mockIsotopeService.Object,
            mockLocationService.Object,
            mockReportingService.Object,
            null,
            neutronService,
            typeService);
    }

    /// <summary>
    /// 1. اختبار فقدان البيانات: تعديل وحفظ مصدر نيتروني يحافظ على حقول المعايرة الثلاثة ولا يمحوها
    /// </summary>
    [Fact]
    public async Task Update_PreservesCalibrationData_PreventingSilentDataLoss()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var initialEmissionCalibDate = new DateTime(2024, 6, 15);
        var initialRef = "CAL-CERT-999";
        var initialAnisotropy = 1.08;

        using (var db = _fixture.CreateContext())
        {
            db.NeutronSources.Add(new NeutronSource
            {
                Id = sourceId,
                SourceCode = "NS-LOSS-TEST",
                NeutronSourceTypeId = _typeId,
                LocationId = _locId,
                CalibratedEmissionRate = 5000000,
                RelativeExpandedUncertaintyPercent = 2.5,
                CalibrationDate = new DateTime(2024, 6, 15),
                EmissionCalibrationDate = initialEmissionCalibDate,
                CalibrationReference = initialRef,
                AnisotropyFactor = initialAnisotropy,
                Status = "Storage",
                CreatedAt = DateTime.Now
            });
            db.SaveChanges();
        }

        var mockAudit = new Mock<IAuditService>();
        var mockUser = new Mock<IUserService>();
        var realNeutronService = new NeutronSourceService(_fixture.ContextFactory, mockAudit.Object, mockUser.Object);

        var vm = CreateViewModel(realNeutronService);

        var existingSource = realNeutronService.GetById(sourceId);
        Assert.NotNull(existingSource);

        vm.EditNeutronSource(existingSource);

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert: Database row must retain calibration values
        using (var db = _fixture.CreateContext())
        {
            var updatedInDb = db.NeutronSources.Find(sourceId);
            Assert.NotNull(updatedInDb);
            Assert.Equal(initialEmissionCalibDate, updatedInDb.EmissionCalibrationDate);
            Assert.Equal(initialRef, updatedInDb.CalibrationReference);
            Assert.Equal(initialAnisotropy, updatedInDb.AnisotropyFactor);
        }
    }

    /// <summary>
    /// 2. SaveAsync يمرر الحقول الثلاثة إلى خدمة INeutronSourceService
    /// </summary>
    [Fact]
    public async Task SaveAsync_PassesCalibrationFieldsToNeutronService_OnCreateAndOnUpdate()
    {
        // Case A: Create new neutron source
        NeutronSource? capturedCreatedSource = null;
        var mockNeutronService = new Mock<INeutronSourceService>();
        mockNeutronService
            .Setup(s => s.Create(It.IsAny<NeutronSource>()))
            .Callback<NeutronSource>(ns => capturedCreatedSource = ns)
            .Returns((true, "Created successfully"));

        var vm = CreateViewModel(mockNeutronService.Object);
        vm.AddNewNeutron();

        var calibDate = new DateTime(2024, 8, 20);
        vm.EditSourceCode = "NS-CREATE-TEST";
        vm.EditNeutronTypeId = _typeId;
        vm.EditEmissionRateText = "4.5E6";
        vm.EditRelativeUncertaintyText = "3.2";
        vm.EditCalibrationDate = new DateTime(2024, 8, 20);
        vm.EditEmissionCalibrationDate = calibDate;
        vm.EditCalibrationReference = "NIST-2024-TEST ";
        vm.EditAnisotropyFactorText = "1.04";
        vm.EditLocationId = _locId;
        vm.EditStatus = "Storage";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(capturedCreatedSource);
        Assert.Equal("NS-CREATE-TEST", capturedCreatedSource.SourceCode);
        Assert.Equal(calibDate, capturedCreatedSource.EmissionCalibrationDate);
        Assert.Equal("NIST-2024-TEST", capturedCreatedSource.CalibrationReference);
        Assert.Equal(1.04, capturedCreatedSource.AnisotropyFactor);

        // Case B: Update existing neutron source
        NeutronSource? capturedUpdatedSource = null;
        mockNeutronService
            .Setup(s => s.Update(It.IsAny<NeutronSource>()))
            .Callback<NeutronSource>(ns => capturedUpdatedSource = ns)
            .Returns((true, "Updated successfully"));

        var existing = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-UPDATE-TEST",
            NeutronSourceTypeId = _typeId,
            LocationId = _locId,
            CalibratedEmissionRate = 2000000,
            CalibrationDate = new DateTime(2024, 1, 1),
            Status = "Storage"
        };

        vm.EditNeutronSource(existing);
        var newCalibDate = new DateTime(2024, 5, 10);
        vm.EditEmissionCalibrationDate = newCalibDate;
        vm.EditCalibrationReference = "CERT-UPDATED-001";
        vm.EditAnisotropyFactorText = "1.12";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(capturedUpdatedSource);
        Assert.Equal(existing.Id, capturedUpdatedSource.Id);
        Assert.Equal(newCalibDate, capturedUpdatedSource.EmissionCalibrationDate);
        Assert.Equal("CERT-UPDATED-001", capturedUpdatedSource.CalibrationReference);
        Assert.Equal(1.12, capturedUpdatedSource.AnisotropyFactor);
    }

    /// <summary>
    /// 3. EditNeutronSource يحمّل الحقول الثلاثة من الكيان إلى خصائص التحرير
    /// </summary>
    [Fact]
    public void EditNeutronSource_LoadsCalibrationFieldsIntoEditProperties()
    {
        var mockNeutronService = new Mock<INeutronSourceService>();
        var vm = CreateViewModel(mockNeutronService.Object);

        var target = new NeutronSource
        {
            Id = Guid.NewGuid(),
            SourceCode = "NS-EDIT-LOAD",
            NeutronSourceTypeId = _typeId,
            LocationId = _locId,
            CalibratedEmissionRate = 3500000,
            RelativeExpandedUncertaintyPercent = 1.9,
            CalibrationDate = new DateTime(2023, 11, 1),
            EmissionCalibrationDate = new DateTime(2023, 11, 1),
            CalibrationReference = "NPL-CERT-77",
            AnisotropyFactor = 1.06,
            Status = "InUse"
        };

        vm.EditNeutronSource(target);

        Assert.Equal(target.EmissionCalibrationDate, vm.EditEmissionCalibrationDate);
        Assert.Equal("NPL-CERT-77", vm.EditCalibrationReference);
        Assert.Equal(1.06, vm.EditAnisotropyFactor);
        Assert.Equal("1.06", vm.EditAnisotropyFactorText);
    }

    /// <summary>
    /// 4. ClearForm عبر AddNewNeutron يصفّر الحقول الأربعة
    /// </summary>
    [Fact]
    public void AddNewNeutron_ClearsCalibrationFields()
    {
        var mockNeutronService = new Mock<INeutronSourceService>();
        var vm = CreateViewModel(mockNeutronService.Object);

        // Pre-fill fields with dirty values
        vm.EditEmissionCalibrationDate = DateTime.Today.AddDays(-10);
        vm.EditCalibrationReference = "SOME-REF";
        vm.EditAnisotropyFactor = 1.25;
        vm.EditAnisotropyFactorText = "1.25";

        // Act
        vm.AddNewNeutron();

        // Assert
        Assert.Null(vm.EditEmissionCalibrationDate);
        Assert.Equal(string.Empty, vm.EditCalibrationReference);
        Assert.Null(vm.EditAnisotropyFactor);
        Assert.Equal(string.Empty, vm.EditAnisotropyFactorText);
    }

    /// <summary>
    /// 5. تاريخ معايرة انبعاث في المستقبل -> رفض بالرسالة الصحيحة
    /// </summary>
    [Fact]
    public async Task SaveAsync_FutureEmissionCalibrationDate_RejectsWithErrorMessage()
    {
        var mockNeutronService = new Mock<INeutronSourceService>();
        var vm = CreateViewModel(mockNeutronService.Object);
        vm.AddNewNeutron();

        vm.EditSourceCode = "NS-FUTURE-DATE";
        vm.EditNeutronTypeId = _typeId;
        vm.EditEmissionRateText = "1000000";
        vm.EditCalibrationDate = DateTime.Today;
        vm.EditEmissionCalibrationDate = DateTime.Today.AddDays(5); // Future date!
        vm.EditLocationId = _locId;
        vm.EditStatus = "Storage";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.HasMessage);
        Assert.Contains(TranslationHelper.GetString("MsgErrEmissionCalibrationDateFuture") ?? "تاريخ معايرة الانبعاث لا يمكن أن يكون في المستقبل", vm.Message);
        mockNeutronService.Verify(s => s.Create(It.IsAny<NeutronSource>()), Times.Never);
        mockNeutronService.Verify(s => s.Update(It.IsAny<NeutronSource>()), Times.Never);
    }

    /// <summary>
    /// 6. AnisotropyFactor صفر أو سالب أو نص غير رقمي -> رفض
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1.5")]
    [InlineData("-0.01")]
    [InlineData("not-a-number")]
    [InlineData("xyz")]
    public async Task SaveAsync_InvalidAnisotropyFactor_RejectsWithErrorMessage(string invalidAnisotropyText)
    {
        var mockNeutronService = new Mock<INeutronSourceService>();
        var vm = CreateViewModel(mockNeutronService.Object);
        vm.AddNewNeutron();

        vm.EditSourceCode = "NS-INVALID-ANIS";
        vm.EditNeutronTypeId = _typeId;
        vm.EditEmissionRateText = "1000000";
        vm.EditCalibrationDate = DateTime.Today;
        vm.EditAnisotropyFactorText = invalidAnisotropyText;
        vm.EditLocationId = _locId;
        vm.EditStatus = "Storage";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.HasMessage);
        Assert.Contains(TranslationHelper.GetString("MsgErrInvalidAnisotropyFactor") ?? "معامل اللاتماثل الزاوي يجب أن يكون رقماً أكبر من صفر", vm.Message);
        mockNeutronService.Verify(s => s.Create(It.IsAny<NeutronSource>()), Times.Never);
        mockNeutronService.Verify(s => s.Update(It.IsAny<NeutronSource>()), Times.Never);
    }

    /// <summary>
    /// 7. EmissionCalibrationDate فارغ -> الحفظ ينجح ولا يُرفض
    /// </summary>
    [Fact]
    public async Task SaveAsync_NullEmissionCalibrationDate_Succeeds()
    {
        NeutronSource? createdSource = null;
        var mockNeutronService = new Mock<INeutronSourceService>();
        mockNeutronService
            .Setup(s => s.Create(It.IsAny<NeutronSource>()))
            .Callback<NeutronSource>(ns => createdSource = ns)
            .Returns((true, "Saved"));

        var vm = CreateViewModel(mockNeutronService.Object);
        vm.AddNewNeutron();

        vm.EditSourceCode = "NS-NULL-CALIB-DATE";
        vm.EditNeutronTypeId = _typeId;
        vm.EditEmissionRateText = "1000000";
        vm.EditCalibrationDate = DateTime.Today;
        vm.EditEmissionCalibrationDate = null; // Explicitly null
        vm.EditCalibrationReference = "";
        vm.EditAnisotropyFactorText = "";
        vm.EditLocationId = _locId;
        vm.EditStatus = "Storage";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(createdSource);
        Assert.Null(createdSource.EmissionCalibrationDate);
        Assert.Null(createdSource.CalibrationReference);
        Assert.Null(createdSource.AnisotropyFactor);
        mockNeutronService.Verify(s => s.Create(It.IsAny<NeutronSource>()), Times.Once);
    }

    /// <summary>
    /// 8. اختبار تكاملي: إنشاء مصدر بتاريخ معايرة انبعاث وحساب الاضمحلال عبر INeutronDecayCalculationService والتحقق أن النتيجة محسوبة
    /// </summary>
    [Fact]
    public async Task Integration_CreateSourceWithEmissionCalibrationDate_DecayCalculatedSuccessfully()
    {
        var mockAudit = new Mock<IAuditService>();
        var mockUser = new Mock<IUserService>();
        var realNeutronService = new NeutronSourceService(_fixture.ContextFactory, mockAudit.Object, mockUser.Object);

        var vm = CreateViewModel(realNeutronService);
        vm.AddNewNeutron();

        var calibDate = DateTime.Today.AddYears(-2);
        vm.EditSourceCode = "NS-INTEGRATION-01";
        vm.EditNeutronTypeId = _typeId;
        vm.EditEmissionRateText = "1.0E7";
        vm.EditCalibrationDate = calibDate;
        vm.EditEmissionCalibrationDate = calibDate;
        vm.EditCalibrationReference = "INTEG-CERT-2024";
        vm.EditAnisotropyFactorText = "1.05";
        vm.EditLocationId = _locId;
        vm.EditStatus = "Storage";

        await vm.SaveCommand.ExecuteAsync(null);

        // Retrieve from database
        var created = realNeutronService.GetByCode("NS-INTEGRATION-01");
        Assert.NotNull(created);
        Assert.Equal(calibDate, created.EmissionCalibrationDate);
        Assert.Equal("INTEG-CERT-2024", created.CalibrationReference);
        Assert.Equal(1.05, created.AnisotropyFactor);

        // Calculate decay using INeutronDecayCalculationService
        INeutronDecayCalculationService decayService = new NeutronDecayCalculationService();
        var decayResult = decayService.CalculateCurrentEmissionRate(created);

        Assert.True(decayResult.IsCalculated);
        Assert.Equal(NeutronDecayCalculationStatus.Calculated, decayResult.Status);
        Assert.NotNull(decayResult.CurrentEmissionRate);
        Assert.True(decayResult.CurrentEmissionRate.Value > 0);
        Assert.True(decayResult.CurrentEmissionRate.Value < 1.0E7); // Decayed over 2 years for Am-Be (T1/2 = 432.2y)
    }
}
