using System.Windows;

namespace Sources.Helpers;

public static class TranslationHelper
{
    public static string? GetString(string key)
    {
        if (Application.Current != null && Application.Current.Resources.Contains(key))
        {
            return Application.Current.FindResource(key) as string;
        }
        return null;
    }

    public static string GetFormat(string key, params object[] args)
    {
        string? format = GetString(key);
        if (string.IsNullOrEmpty(format))
        {
            return key;
        }

        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format;
        }
    }
}
