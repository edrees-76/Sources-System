using System.Collections.ObjectModel;
using System.Windows;

namespace Sources.Models;

/// <summary>
/// فئات الأدوار المخصصة لمسارات التعلم السريع في دليل المستخدم
/// </summary>
public static class HelpRoles
{
    public const string All = "All";
    public const string Storekeeper = "Storekeeper";
    public const string SafetyOfficer = "SafetyOfficer";
}

/// <summary>
/// الكتلة الأساسية لبناء محتوى المقالة في نمط MVVM
/// </summary>
public abstract class HelpBlock
{
}

/// <summary>
/// فقرة نصية عامة أو افتتاحية بارزة
/// </summary>
public class HelpParagraphBlock : HelpBlock
{
    public string TextKey { get; set; } = string.Empty;
    public bool IsLead { get; set; } = false;
}

/// <summary>
/// خطوة إجرائية مرقمة ضمن سيناريو عمل
/// </summary>
public class HelpStepBlock : HelpBlock
{
    public string StepNumber { get; set; } = "1";
    public string TitleKey { get; set; } = string.Empty;
    public string DescriptionKey { get; set; } = string.Empty;
}

/// <summary>
/// صندوق نصائح وإرشادات باللون الأخضر/النجاح
/// </summary>
public class HelpTipBlock : HelpBlock
{
    public string TextKey { get; set; } = string.Empty;
    public string IconKind { get; set; } = "LightbulbOnOutline";
}

/// <summary>
/// صندوق تنبيهات وتحذيرات باللون البرتقالي/التحذير
/// </summary>
public class HelpWarningBlock : HelpBlock
{
    public string TextKey { get; set; } = string.Empty;
    public string IconKind { get; set; } = "AlertCircleOutline";
}

/// <summary>
/// صندوق أمثلة توضيحية ومحاكاة عملية
/// </summary>
public class HelpExampleBlock : HelpBlock
{
    public string TitleKey { get; set; } = string.Empty;
    public string ContentKey { get; set; } = string.Empty;
}

/// <summary>
/// قائمة نقطية
/// </summary>
public class HelpBulletListBlock : HelpBlock
{
    public string? TitleKey { get; set; }
    public List<string> ItemKeys { get; set; } = new();
    public bool HasTitle => !string.IsNullOrEmpty(TitleKey);
}

/// <summary>
/// عنصر سؤال وجواب شائع FAQ
/// </summary>
public class HelpFaqBlock : HelpBlock
{
    public string QuestionKey { get; set; } = string.Empty;
    public string AnswerKey { get; set; } = string.Empty;
}

/// <summary>
/// عنصر تحديث جديد أو ميزة مضافة
/// </summary>
public class HelpWhatsNewBlock : HelpBlock
{
    public string VersionBadge { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string TitleKey { get; set; } = string.Empty;
    public string DescriptionKey { get; set; } = string.Empty;
    public string TagType { get; set; } = "Feature"; // Feature, Security, Backup
}

/// <summary>
/// قسم منظم يحتوي على عنوان وأيقونة ومجموعة كتل
/// </summary>
public class HelpSection
{
    public string? TitleKey { get; set; }
    public string? IconKind { get; set; }
    public ObservableCollection<HelpBlock> Blocks { get; set; } = new();
    public bool HasTitle => !string.IsNullOrEmpty(TitleKey);
}

/// <summary>
/// موضوع دليل المساعدة الرئيسي
/// </summary>
public class HelpTopic
{
    public string Id { get; set; } = string.Empty;
    public string TitleKey { get; set; } = string.Empty;
    public string SubtitleKey { get; set; } = string.Empty;
    public string IconKind { get; set; } = "HelpCircleOutline";
    public string? TargetViewName { get; set; }
    public string? TargetViewButtonTextKey { get; set; }
    public List<string> Roles { get; set; } = new() { HelpRoles.All };
    public string Keywords { get; set; } = string.Empty;
    public ObservableCollection<HelpSection> Sections { get; set; } = new();

    public bool HasTargetView => !string.IsNullOrEmpty(TargetViewName);

    public string DisplayTitle
    {
        get
        {
            if (Application.Current != null && Application.Current.Resources.Contains(TitleKey))
                return Application.Current.FindResource(TitleKey)?.ToString() ?? TitleKey;
            return TitleKey;
        }
    }

    public string DisplaySubtitle
    {
        get
        {
            if (!string.IsNullOrEmpty(SubtitleKey) && Application.Current != null && Application.Current.Resources.Contains(SubtitleKey))
                return Application.Current.FindResource(SubtitleKey)?.ToString() ?? SubtitleKey;
            return SubtitleKey;
        }
    }
}
