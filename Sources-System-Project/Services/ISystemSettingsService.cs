using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

public interface ISystemSettingsService
{
    Dictionary<string, string> GetAllSettings();
    string GetSetting(string key, string defaultValue = "");
    T GetSetting<T>(string key, T defaultValue = default!);
    void SaveSetting(string key, string value);
    void SaveSettings(Dictionary<string, string> settings);
    void ResetToDefaults();
    void ClearCache();
}
