using System.Windows;

namespace Sources.Models;

public class HelpTopic
{
    public string TitleKey { get; set; } = string.Empty;
    public string ContentKey { get; set; } = string.Empty;
    public string IconKind { get; set; } = "HelpCircleOutline";

    /// <summary>
    /// Returns the localized title by resolving the resource key at runtime.
    /// </summary>
    public string DisplayTitle
    {
        get
        {
            if (Application.Current != null && Application.Current.Resources.Contains(TitleKey))
                return Application.Current.FindResource(TitleKey)?.ToString() ?? TitleKey;
            return TitleKey;
        }
    }
}
