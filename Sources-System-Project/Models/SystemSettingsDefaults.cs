using System.Collections.Generic;

namespace Sources.Models;

/// <summary>
/// فئة مركزية موحدة لجميع مفاتيح إعدادات النظام وقيمها الافتراضية.
/// </summary>
public static class SystemSettingsDefaults
{
    public const string LowActivityThresholdPercentKey = "LowActivityThresholdPercent";
    public const string DefaultLowActivityThresholdPercent = "10";

    public const string NotificationCheckIntervalMinutesKey = "NotificationCheckIntervalMinutes";
    public const string DefaultNotificationCheckIntervalMinutes = "60";

    public const string DueSoonDaysThresholdKey = "DueSoonDaysThreshold";
    public const string DefaultDueSoonDaysThreshold = "7";

    public const string AutoBackupEnabledKey = "AutoBackupEnabled";
    public const string DefaultAutoBackupEnabled = "false";

    public const string AutoBackupFrequencyKey = "AutoBackupFrequency";
    public const string DefaultAutoBackupFrequency = "Daily";

    public const string FacilityNameKey = "FacilityName";
    public const string DefaultFacilityName = "";

    public const string FacilityAddressKey = "FacilityAddress";
    public const string DefaultFacilityAddress = "";

    public const string TechnicalDirectorKey = "TechnicalDirector";
    public const string DefaultTechnicalDirector = "";

    public const string BackupPathKey = "BackupPath";
    public const string DefaultBackupPath = "";

    public const string LeakTestIntervalMonthsKey = "LeakTestIntervalMonths";
    public const string DefaultLeakTestIntervalMonths = "6";

    public const string LeakTestWarningDaysThresholdKey = "LeakTestWarningDaysThreshold";
    public const string DefaultLeakTestWarningDaysThreshold = "30";

    /// <summary>
    /// قاموس يجمع كافة الإعدادات بقيمها الافتراضية.
    /// </summary>
    public static readonly Dictionary<string, string> AllDefaults = new()
    {
        { LowActivityThresholdPercentKey, DefaultLowActivityThresholdPercent },
        { NotificationCheckIntervalMinutesKey, DefaultNotificationCheckIntervalMinutes },
        { DueSoonDaysThresholdKey, DefaultDueSoonDaysThreshold },
        { AutoBackupEnabledKey, DefaultAutoBackupEnabled },
        { AutoBackupFrequencyKey, DefaultAutoBackupFrequency },
        { FacilityNameKey, DefaultFacilityName },
        { FacilityAddressKey, DefaultFacilityAddress },
        { TechnicalDirectorKey, DefaultTechnicalDirector },
        { BackupPathKey, DefaultBackupPath },
        { LeakTestIntervalMonthsKey, DefaultLeakTestIntervalMonths },
        { LeakTestWarningDaysThresholdKey, DefaultLeakTestWarningDaysThreshold }
    };
}

