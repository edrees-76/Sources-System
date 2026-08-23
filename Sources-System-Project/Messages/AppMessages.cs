namespace Sources.Messages;

/// <summary>
/// رسالة إشعار بتحديث أو إضافة أو حذف مصدر مشع لإعادة فحص وتوليد التنبيهات فورياً
/// </summary>
public class SourcesUpdatedMessage
{
}

/// <summary>
/// رسالة لطلب التركيز على شريط البحث الموحّد في لوحة التحكم (عند الضغط على Ctrl+K)
/// </summary>
public class FocusDashboardSearchMessage
{
}

/// <summary>
/// رسالة للانتقال وتحديد/فتح عنصر نتيجة بحث موحّد في الشاشة المستهدفة
/// </summary>
public class NavigateToSearchResultMessage
{
    public Sources.Models.SearchCategory Category { get; }
    public System.Guid EntityId { get; }

    public NavigateToSearchResultMessage(Sources.Models.SearchCategory category, System.Guid entityId)
    {
        Category = category;
        EntityId = entityId;
    }
}


