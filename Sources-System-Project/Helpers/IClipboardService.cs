namespace Sources.Helpers;

/// <summary>فاصل حول حافظة النظام ليمكن اختبار مسار الفشل.</summary>
public interface IClipboardService
{
    void SetText(string text);
}

public class ClipboardService : IClipboardService
{
    public void SetText(string text) => System.Windows.Clipboard.SetText(text);
}
