using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبارات وحدة شاملة لخدمة SystemSettingsService
/// تغطي القراءة، الكتابة، تحويل الأنواع، عزل وإبطال الكاش، والإعدادات الافتراضية للنظام
/// </summary>
public class SystemSettingsServiceTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;
    private readonly SystemSettingsService _sut;

    public SystemSettingsServiceTests()
    {
        _fixture = new SqliteInMemoryFixture();
        _sut = new SystemSettingsService(_fixture.ContextFactory);

        // إبطال وتصفير الكاش الثابت (Static Cache) وقاعدة البيانات قبل كل اختبار
        // استدعاء SaveSetting يضع _cache = null داخلياً، ثم ResetDatabase ينظف الجداول
        _sut.SaveSetting("__init_reset__", "1");
        _fixture.ResetDatabase();
    }

    public void Dispose()
    {
        // تنظيف الكاش عند انتهاء كل اختبار لمنع تسرب الحالة للاختبارات التالية
        try
        {
            _sut.SaveSetting("__dispose_reset__", "1");
            _fixture.ResetDatabase();
        }
        catch { }

        _fixture.Dispose();
    }

    #region أ. القراءة الأساسية (GetSetting)

    [Fact]
    public void GetSetting_ExistingKey_ReturnsStoredValue()
    {
        // Arrange
        _sut.SaveSetting("ApplicationTitle", "نظام إدارة المصادر المشعة");

        // Act
        var result = _sut.GetSetting("ApplicationTitle", "DefaultTitle");

        // Assert
        Assert.Equal("نظام إدارة المصادر المشعة", result);
    }

    [Fact]
    public void GetSetting_NonExistentKey_ReturnsDefaultValue()
    {
        // Act
        var result = _sut.GetSetting("NonExistentKey_XYZ", "FallbackValue");

        // Assert
        Assert.Equal("FallbackValue", result);
    }

    [Fact]
    public void GetSetting_NonExistentKey_WhenDefaultOmitted_ReturnsEmptyString()
    {
        // Act
        var result = _sut.GetSetting("NonExistentKey_XYZ");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetSetting_KeyExistsWithEmptyValue_ReturnsEmptyStringNotFallback()
    {
        // Arrange: مفتاح موجود في القاعدة لكن قيمته فارغة
        _sut.SaveSetting("EmptyField", string.Empty);

        // Act: دالة GetSetting تبحث عبر TryGetValue فإذا وُجد المفتاح تُرجع قيمته المخزنة (الفارغة)
        var result = _sut.GetSetting("EmptyField", "DefaultFallback");

        // Assert: يجب أن تُرجع القيمة المخزنة فعلياً (نص فارغ) وليس الـ fallback
        Assert.Equal(string.Empty, result);
    }

    #endregion

    #region ب. القراءة العامة مع تحويل النوع (Generic GetSetting<T>)

    [Fact]
    public void GetSettingGeneric_Int_ValidString_ReturnsParsedInt()
    {
        // Arrange
        _sut.SaveSetting("NotificationCheckIntervalMinutes", "60");

        // Act
        var result = _sut.GetSetting<int>("NotificationCheckIntervalMinutes", 10);

        // Assert
        Assert.Equal(60, result);
    }

    [Fact]
    public void GetSettingGeneric_Bool_ValidString_ReturnsParsedBool()
    {
        // Arrange & Act (True)
        _sut.SaveSetting("AutoBackupEnabled", "True");
        var trueResult = _sut.GetSetting<bool>("AutoBackupEnabled", false);

        // Arrange & Act (False)
        _sut.SaveSetting("AutoBackupEnabled", "False");
        var falseResult = _sut.GetSetting<bool>("AutoBackupEnabled", true);

        // Assert
        Assert.True(trueResult);
        Assert.False(falseResult);
    }

    [Fact]
    public void GetSettingGeneric_Double_ValidString_ReturnsParsedDouble()
    {
        // Arrange
        _sut.SaveSetting("LowActivityThresholdPercent", "10.5");

        // Act
        var result = _sut.GetSetting<double>("LowActivityThresholdPercent", 1.0);

        // Assert
        Assert.Equal(10.5, result, precision: 4);
    }

    [Fact]
    public void GetSettingGeneric_Decimal_ValidString_ReturnsParsedDecimal()
    {
        // Arrange
        _sut.SaveSetting("ThresholdDecimal", "25.75");

        // Act
        var result = _sut.GetSetting<decimal>("ThresholdDecimal", 0m);

        // Assert
        Assert.Equal(25.75m, result);
    }

    [Fact]
    public void GetSettingGeneric_CorruptedInt_DoesNotThrow_ReturnsDefaultValue()
    {
        // Arrange: نص غير رقمي في حقل يُقرأ كـ int
        _sut.SaveSetting("CorruptedIntKey", "abc_invalid_number");

        // Act: try/catch يبتلع الخطأ بأمان
        var result = _sut.GetSetting<int>("CorruptedIntKey", 42);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void GetSettingGeneric_CorruptedBool_DoesNotThrow_ReturnsDefaultValue()
    {
        // Arrange: نص غير صالح كـ boolean
        _sut.SaveSetting("CorruptedBoolKey", "NotABoolean");

        // Act
        var result = _sut.GetSetting<bool>("CorruptedBoolKey", true);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetSettingGeneric_NonExistentKey_ReturnsDefaultValue()
    {
        // Act
        var intResult = _sut.GetSetting<int>("MissingIntKey", 99);
        var boolResult = _sut.GetSetting<bool>("MissingBoolKey", true);

        // Assert
        Assert.Equal(99, intResult);
        Assert.True(boolResult);
    }

    [Fact]
    public void GetSettingGeneric_NonExistentKey_DefaultValueOmitted_ReturnsTypeDefault()
    {
        // Act
        var intResult = _sut.GetSetting<int>("MissingIntKey");
        var boolResult = _sut.GetSetting<bool>("MissingBoolKey");
        var doubleResult = _sut.GetSetting<double>("MissingDoubleKey");

        // Assert
        Assert.Equal(default(int), intResult);
        Assert.Equal(default(bool), boolResult);
        Assert.Equal(default(double), doubleResult);
    }

    [Fact]
    public void GetSettingGeneric_EmptyStringStored_ReturnsDefaultValue()
    {
        // Arrange: تخزين قيمة فارغة
        _sut.SaveSetting("EmptyNumericSetting", string.Empty);

        // Act: في GetSetting<T> التحقق string.IsNullOrEmpty(value) يُرجع defaultValue فوراً
        var result = _sut.GetSetting<int>("EmptyNumericSetting", 55);

        // Assert
        Assert.Equal(55, result);
    }

    #endregion

    #region ج. الكتابة (SaveSetting / SaveSettings)

    [Fact]
    public void SaveSetting_NewKey_CreatesNewRecordAndPersistsToDatabase()
    {
        // Act
        _sut.SaveSetting("BackupPath", "D:\\SourcesBackups");

        // Assert via Service
        var retrieved = _sut.GetSetting("BackupPath");
        Assert.Equal("D:\\SourcesBackups", retrieved);

        // Assert directly in Database
        using var db = _fixture.CreateContext();
        var record = db.AppSettings.Find("BackupPath");
        Assert.NotNull(record);
        Assert.Equal("D:\\SourcesBackups", record.Value);
    }

    [Fact]
    public void SaveSetting_ExistingKey_UpdatesRecordWithoutCreatingDuplicate()
    {
        // Arrange: حفظ قيمة أولية
        _sut.SaveSetting("SelectedTheme", "Light");

        // Act: تحديث نفس المفتاح
        _sut.SaveSetting("SelectedTheme", "Dark");

        // Assert directly in Database
        using var db = _fixture.CreateContext();
        var records = db.AppSettings.Where(s => s.Key == "SelectedTheme").ToList();

        Assert.Single(records);
        Assert.Equal("Dark", records[0].Value);

        // Assert via Service
        Assert.Equal("Dark", _sut.GetSetting("SelectedTheme"));
    }

    [Fact]
    public void SaveSettings_BatchInsertAndUpdates_PersistsAllCorrectly()
    {
        // Arrange: إضافة إعداد موجود مسبقاً
        _sut.SaveSetting("Key1", "OldValue1");

        var batch = new Dictionary<string, string>
        {
            { "Key1", "UpdatedValue1" },
            { "Key2", "NewValue2" },
            { "Key3", "NewValue3" }
        };

        // Act
        _sut.SaveSettings(batch);

        // Assert
        Assert.Equal("UpdatedValue1", _sut.GetSetting("Key1"));
        Assert.Equal("NewValue2", _sut.GetSetting("Key2"));
        Assert.Equal("NewValue3", _sut.GetSetting("Key3"));

        using var db = _fixture.CreateContext();
        Assert.Equal(3, db.AppSettings.Count());
        Assert.Equal("UpdatedValue1", db.AppSettings.Find("Key1")?.Value);
        Assert.Equal("NewValue2", db.AppSettings.Find("Key2")?.Value);
        Assert.Equal("NewValue3", db.AppSettings.Find("Key3")?.Value);
    }

    #endregion

    #region د. إبطال الكاش (Cache Invalidation)

    [Fact]
    public void SaveSetting_InvalidatesCache_SubsequentGetSettingReturnsNewValueImmediately()
    {
        // 1. حفظ قيمة أولية
        _sut.SaveSetting("CachedKey", "InitialValue");

        // 2. قراءة الإعداد لتحميل الكاش
        var cachedVal = _sut.GetSetting("CachedKey");
        Assert.Equal("InitialValue", cachedVal);

        // 3. تعديل الإعداد عبر SaveSetting
        _sut.SaveSetting("CachedKey", "UpdatedValue");

        // 4. قراءة الإعداد مرة أخرى للتأكد من أن الكاش أُبطل وأرجع القيمة المحدثة
        var updatedVal = _sut.GetSetting("CachedKey");
        Assert.Equal("UpdatedValue", updatedVal);
    }

    [Fact]
    public void SaveSettings_Batch_InvalidatesCache_SubsequentGetSettingReturnsNewValues()
    {
        // 1. حفظ وقراءة لتحميل الكاش
        _sut.SaveSetting("BatchCacheTest", "OldBatchVal");
        Assert.Equal("OldBatchVal", _sut.GetSetting("BatchCacheTest"));

        // 2. تحديث عبر SaveSettings الدفعية
        _sut.SaveSettings(new Dictionary<string, string>
        {
            { "BatchCacheTest", "NewBatchVal" }
        });

        // 3. التحقق من إبطال الكاش
        Assert.Equal("NewBatchVal", _sut.GetSetting("BatchCacheTest"));
    }

    [Fact]
    public void GetAllSettings_ReturnsAllStoredSettingsAsDictionary()
    {
        // Arrange
        _sut.SaveSetting("SettingA", "ValA");
        _sut.SaveSetting("SettingB", "ValB");
        _sut.SaveSetting("SettingC", "ValC");

        // Act
        var allSettings = _sut.GetAllSettings();

        // Assert
        Assert.NotNull(allSettings);
        Assert.True(allSettings.Count >= 3);
        Assert.Equal("ValA", allSettings["SettingA"]);
        Assert.Equal("ValB", allSettings["SettingB"]);
        Assert.Equal("ValC", allSettings["SettingC"]);
    }

    [Fact]
    public void GetAllSettings_UsesCache_UntilInvalidatedBySaveSetting()
    {
        // 1. وضع إعداد أولي وتحميل الكاش
        _sut.SaveSetting("BaseKey", "BaseVal");
        var initialDict = _sut.GetAllSettings();
        Assert.True(initialDict.ContainsKey("BaseKey"));

        // 2. إضافة سجل مباشرة في قاعدة البيانات متجاوزين الخدمة (دون إبطال الكاش)
        using (var db = _fixture.CreateContext())
        {
            db.AppSettings.Add(new AppSetting { Key = "DirectDbKey", Value = "DirectDbVal" });
            db.SaveChanges();
        }

        // 3. القراءة من الخدمة يجب أن ترجع الكاش القديم (لا تحتوي على DirectDbKey بعد)
        var cachedDict = _sut.GetAllSettings();
        Assert.False(cachedDict.ContainsKey("DirectDbKey"));

        // 4. استدعاء SaveSetting يُبطل الكاش (_cache = null)
        _sut.SaveSetting("TriggerInvalidation", "1");

        // 5. الآن GetAllSettings ستعيد القراءة من قاعدة البيانات وتجد DirectDbKey
        var refreshedDict = _sut.GetAllSettings();
        Assert.True(refreshedDict.ContainsKey("DirectDbKey"));
        Assert.Equal("DirectDbVal", refreshedDict["DirectDbKey"]);
    }

    #endregion

    #region هـ. الإعدادات الفعلية المستخدمة في المنظومة (System Integration Defaults)

    [Fact]
    public void SystemDefaults_WhenDatabaseIsEmpty_LowActivityThresholdPercentReturnsFallback()
    {
        // في قاعدة بيانات فارغة قبل الـ Seed:
        // التحقق من أن القراءة ترجع القيمة الافتراضية المحددة في النظام (10 أو 10.0)
        var stringResult = _sut.GetSetting("LowActivityThresholdPercent", "10");
        var doubleResult = _sut.GetSetting<double>("LowActivityThresholdPercent", 10.0);

        Assert.Equal("10", stringResult);
        Assert.Equal(10.0, doubleResult);
    }

    [Fact]
    public void SystemDefaults_WhenDatabaseIsEmpty_NotificationCheckIntervalMinutesReturnsFallback()
    {
        // التحقق من القيمة الافتراضية لفحص التنبيهات (60 دقيقة)
        var stringResult = _sut.GetSetting("NotificationCheckIntervalMinutes", "60");
        var intResult = _sut.GetSetting<int>("NotificationCheckIntervalMinutes", 60);

        Assert.Equal("60", stringResult);
        Assert.Equal(60, intResult);
    }

    [Fact]
    public void SystemDefaults_WhenDatabaseIsEmpty_AutoBackupSettingsReturnFallbacks()
    {
        // التحقق من القيم الافتراضية للنسخ الاحتياطي التلقائي
        var isEnabled = _sut.GetSetting<bool>("AutoBackupEnabled", false);
        var frequency = _sut.GetSetting("AutoBackupFrequency", "Daily");
        var backupPath = _sut.GetSetting("BackupPath", string.Empty);

        Assert.False(isEnabled);
        Assert.Equal("Daily", frequency);
        Assert.Equal(string.Empty, backupPath);
    }

    [Fact]
    public void SystemDefaults_WhenDatabaseIsEmpty_FacilitySettingsReturnFallbacks()
    {
        // التحقق من القيم الافتراضية لمعلومات المنشأة
        var facilityName = _sut.GetSetting("FacilityName", string.Empty);
        var facilityAddress = _sut.GetSetting("FacilityAddress", string.Empty);
        var technicalDirector = _sut.GetSetting("TechnicalDirector", string.Empty);

        Assert.Equal(string.Empty, facilityName);
        Assert.Equal(string.Empty, facilityAddress);
        Assert.Equal(string.Empty, technicalDirector);
    }

    #endregion
}
