using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Sources.Helpers;
using Sources.Interfaces;
using Sources.Models;

namespace Sources.Services;

public class IsotopeLibraryService : IIsotopeLibraryService
{
    private List<IsotopeReferenceEntry>? _cachedEntries;
    private readonly object _lock = new();

    private class IcrpJsonEntry
    {
        [JsonPropertyName("nuclide")]
        public string Nuclide { get; set; } = string.Empty;

        [JsonPropertyName("is_radioactive")]
        public bool IsRadioactive { get; set; } = true;

        [JsonPropertyName("half_life_raw")]
        public string HalfLifeRaw { get; set; } = string.Empty;

        [JsonPropertyName("half_life_seconds")]
        public double? HalfLifeSeconds { get; set; }

        [JsonPropertyName("decay_mode_raw")]
        public string DecayModeRaw { get; set; } = string.Empty;

        [JsonPropertyName("alpha_energy_mev")]
        public double? AlphaEnergyMeV { get; set; }

        [JsonPropertyName("electron_energy_mev")]
        public double? ElectronEnergyMeV { get; set; }

        [JsonPropertyName("photon_energy_mev")]
        public double? PhotonEnergyMeV { get; set; }

        [JsonPropertyName("total_energy_mev")]
        public double? TotalEnergyMeV { get; set; }

        [JsonPropertyName("source_reference")]
        public string SourceReference { get; set; } = "ICRP Publication 107 (Annex A, 2008)";
    }

    public string GetIndexJsonPath()
    {
        string[] candidatePaths =
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "References", "gamma_constants_index.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "References", "gamma_constants_index.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Sources-System-Project", "Resources", "References", "gamma_constants_index.json"),
            @"d:\Sources-System\Sources-System-Project\Resources\References\gamma_constants_index.json"
        };

        foreach (var p in candidatePaths)
        {
            try
            {
                var full = Path.GetFullPath(p);
                if (File.Exists(full))
                    return full;
            }
            catch { }
        }

        return candidatePaths[0];
    }

    public string GetIcrpJsonPath()
    {
        string[] candidatePaths =
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "References", "icrp107_decay_index.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "References", "icrp107_decay_index.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Sources-System-Project", "Resources", "References", "icrp107_decay_index.json"),
            @"d:\Sources-System\Sources-System-Project\Resources\References\icrp107_decay_index.json"
        };

        foreach (var p in candidatePaths)
        {
            try
            {
                var full = Path.GetFullPath(p);
                if (File.Exists(full))
                    return full;
            }
            catch { }
        }

        return candidatePaths[0];
    }

    public string GetReferencePdfPath()
    {
        string[] candidatePaths =
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "References", "14724519.pdf"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "References", "14724519.pdf"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Sources-System-Project", "Resources", "References", "14724519.pdf"),
            @"d:\Sources-System\Sources-System-Project\Resources\References\14724519.pdf"
        };

        foreach (var p in candidatePaths)
        {
            try
            {
                var full = Path.GetFullPath(p);
                if (File.Exists(full))
                    return full;
            }
            catch { }
        }

        return candidatePaths[0];
    }

    public string GetIcrpPdfPath()
    {
        string[] candidatePaths =
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "References", "ANIB_38_3.pdf"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "References", "ANIB_38_3.pdf"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Sources-System-Project", "Resources", "References", "ANIB_38_3.pdf"),
            @"d:\Sources-System\Sources-System-Project\Resources\References\ANIB_38_3.pdf"
        };

        foreach (var p in candidatePaths)
        {
            try
            {
                var full = Path.GetFullPath(p);
                if (File.Exists(full))
                    return full;
            }
            catch { }
        }

        return candidatePaths[0];
    }

    public async Task<IReadOnlyList<IsotopeReferenceEntry>> GetAllEntriesAsync()
    {
        if (_cachedEntries != null)
            return _cachedEntries;

        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (_cachedEntries != null)
                    return _cachedEntries;

                var combinedList = new List<IsotopeReferenceEntry>();
                var ornlSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. تحميل فهرس ORNL (المصدر الأساسي لثوابت غاما)
                var ornlJsonPath = GetIndexJsonPath();
                if (File.Exists(ornlJsonPath))
                {
                    try
                    {
                        var json = File.ReadAllText(ornlJsonPath);
                        var ornlList = JsonSerializer.Deserialize<List<IsotopeReferenceEntry>>(json) ?? new List<IsotopeReferenceEntry>();
                        foreach (var item in ornlList)
                        {
                            item.SourceType = ReferenceSourceType.ORNL_RSIC_45_R1;
                            combinedList.Add(item);
                            ornlSymbols.Add(NormalizeSymbolKey(item.DisplaySymbol));
                            ornlSymbols.Add(NormalizeSymbolKey(item.NuclideSymbol));
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogError($"IsotopeLibraryService: Failed to parse ORNL JSON at {ornlJsonPath}", ex);
                    }
                }
                else
                {
                    LoggerService.LogWarning($"IsotopeLibraryService: ORNL JSON not found at {ornlJsonPath}");
                }

                // 2. تحميل فهرس ICRP 107 (المصدر البديل للنظائر المشعة)
                var icrpJsonPath = GetIcrpJsonPath();
                if (File.Exists(icrpJsonPath))
                {
                    try
                    {
                        var json = File.ReadAllText(icrpJsonPath);
                        var icrpRawList = JsonSerializer.Deserialize<List<IcrpJsonEntry>>(json) ?? new List<IcrpJsonEntry>();
                        foreach (var raw in icrpRawList)
                        {
                            var normalizedKey = NormalizeSymbolKey(raw.Nuclide);
                            // إن كان النظير مسجلاً بالفعل في ORNL مع ثابت غاما، لا نكرره كمدخل ICRP منفصل
                            if (ornlSymbols.Contains(normalizedKey))
                                continue;

                            var icrpEntry = new IsotopeReferenceEntry
                            {
                                NuclideSymbol = raw.Nuclide,
                                SourceType = ReferenceSourceType.ICRP_107,
                                HalfLife = raw.HalfLifeRaw,
                                DecayMode = raw.DecayModeRaw,
                                AlphaEnergyMeV = raw.AlphaEnergyMeV,
                                ElectronEnergyMeV = raw.ElectronEnergyMeV,
                                PhotonEnergyMeV = raw.PhotonEnergyMeV,
                                TotalEnergyMeV = raw.TotalEnergyMeV,
                                IsRadioactive = raw.IsRadioactive,
                                SourceReference = raw.SourceReference
                            };
                            combinedList.Add(icrpEntry);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogError($"IsotopeLibraryService: Failed to parse ICRP JSON at {icrpJsonPath}", ex);
                    }
                }
                else
                {
                    LoggerService.LogWarning($"IsotopeLibraryService: ICRP JSON not found at {icrpJsonPath}");
                }

                // ترتيب القائمة الموحدة أبجدياً بالرمز الكيميائي أولاً ثم بالرقم الكتلي تصاعدياً (مثال: Co-57 قبل Co-60، و Co-60 قبل Cs-131)
                combinedList = combinedList
                    .OrderBy(e => GetNuclideSortKey(e.DisplaySymbol).Element, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => GetNuclideSortKey(e.DisplaySymbol).Mass)
                    .ThenBy(e => GetNuclideSortKey(e.DisplaySymbol).Suffix, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _cachedEntries = combinedList;
                return _cachedEntries;
            }
        });
    }

    public static (string Element, int Mass, string Suffix) GetNuclideSortKey(string displaySymbol)
    {
        if (string.IsNullOrWhiteSpace(displaySymbol))
            return (string.Empty, 0, string.Empty);

        var match = System.Text.RegularExpressions.Regex.Match(displaySymbol.Trim(), @"^([A-Za-z]+)-(\d+)([A-Za-z\d]*)$");
        if (match.Success)
        {
            var elem = match.Groups[1].Value;
            _ = int.TryParse(match.Groups[2].Value, out int mass);
            var suffix = match.Groups[3].Value;
            return (elem, mass, suffix);
        }

        return (displaySymbol, 0, string.Empty);
    }

    private static string NormalizeSymbolKey(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return string.Empty;
        return symbol.Replace("-", "").Replace(" ", "").Trim().ToLowerInvariant();
    }

    public static string? GetReversedNuclideKey(string compactKey)
    {
        if (string.IsNullOrWhiteSpace(compactKey)) return null;

        // 1. Metastable starting with digits + m (e.g. "99mtc", "133mba", "134m1cs")
        var matchMetaPrefix = System.Text.RegularExpressions.Regex.Match(compactKey, @"^(\d+)(m\d?)([a-z]+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (matchMetaPrefix.Success)
        {
            return $"{matchMetaPrefix.Groups[3].Value}{matchMetaPrefix.Groups[1].Value}{matchMetaPrefix.Groups[2].Value}";
        }

        // 2. Metastable with mass + element + m (e.g. "99tcm", "133bam")
        var matchMetaSuffix = System.Text.RegularExpressions.Regex.Match(compactKey, @"^(\d+)([a-z]+)(m\d?)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (matchMetaSuffix.Success)
        {
            return $"{matchMetaSuffix.Groups[2].Value}{matchMetaSuffix.Groups[1].Value}{matchMetaSuffix.Groups[3].Value}";
        }

        // 3. Element first (e.g. "tc99m", "co60", "cs137", "ba133m")
        var matchElemMass = System.Text.RegularExpressions.Regex.Match(compactKey, @"^([a-z]+?)(\d+(?:m\d?)?)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (matchElemMass.Success)
        {
            return $"{matchElemMass.Groups[2].Value}{matchElemMass.Groups[1].Value}";
        }

        // 4. Standard Mass first (e.g. "60co", "137cs")
        var matchMassElem = System.Text.RegularExpressions.Regex.Match(compactKey, @"^(\d+)([a-z]+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (matchMassElem.Success)
        {
            return matchMassElem.Groups[2].Value + matchMassElem.Groups[1].Value;
        }

        return null;
    }

    public async Task<IReadOnlyList<IsotopeReferenceEntry>> SearchAsync(string query)
    {
        var all = await GetAllEntriesAsync();
        if (string.IsNullOrWhiteSpace(query))
            return all;

        var normalizedQuery = TextNormalizer.Normalize(query);
        var compactQuery = normalizedQuery.Replace("-", "").Replace(" ", "");

        // دعم البحث بالأسماء العربية الشائعة
        var arabicToSymbolMap = new Dictionary<string, string>
        {
            ["كوبالت"] = "co",
            ["سيزيوم"] = "cs",
            ["يود"] = "i",
            ["صوديوم"] = "na",
            ["اريديوم"] = "ir",
            ["تكنسيوم"] = "tc",
            ["راديوم"] = "ra",
            ["امريسيوم"] = "am",
            ["كالسيوم"] = "ca",
            ["غاليوم"] = "ga",
            ["جاليوم"] = "ga",
            ["نيكل"] = "ni",
            ["الومنيوم"] = "al",
            ["ارغون"] = "ar",
            ["ارجون"] = "ar",
            ["جرمانيوم"] = "ge",
            ["زنك"] = "zn",
            ["منغنيز"] = "mn",
            ["سيلينيوم"] = "se",
            ["باريوم"] = "ba",
            ["يورانيوم"] = "u",
            ["بلوتونيوم"] = "pu",
            ["هيدروجين"] = "h",
            ["كربون"] = "c",
            ["سترونشيوم"] = "sr",
            ["فسفور"] = "p"
        };

        foreach (var kvp in arabicToSymbolMap)
        {
            if (normalizedQuery.Contains(kvp.Key))
            {
                compactQuery = compactQuery.Replace(kvp.Key, kvp.Value);
            }
        }

        // استخراج الأنماط المعكوسة (مثال: co60 <-> 60co, cs137 <-> 137cs, 99mtc <-> tc99m)
        string? reversedQuery = GetReversedNuclideKey(compactQuery);

        var results = all.Where(entry =>
        {
            var s1 = entry.NuclideSymbol.Replace("-", "").Replace(" ", "").ToLowerInvariant();
            var s2 = entry.DisplaySymbol.Replace("-", "").Replace(" ", "").ToLowerInvariant();

            // 1. تطابق تام
            if (s1.Equals(compactQuery, StringComparison.OrdinalIgnoreCase) ||
                s2.Equals(compactQuery, StringComparison.OrdinalIgnoreCase))
                return true;

            if (reversedQuery != null && (s1.Equals(reversedQuery, StringComparison.OrdinalIgnoreCase) || s2.Equals(reversedQuery, StringComparison.OrdinalIgnoreCase)))
                return true;

            // 2. يبدأ بـ
            if (s1.StartsWith(compactQuery, StringComparison.OrdinalIgnoreCase) ||
                s2.StartsWith(compactQuery, StringComparison.OrdinalIgnoreCase))
                return true;

            if (reversedQuery != null && (s1.StartsWith(reversedQuery, StringComparison.OrdinalIgnoreCase) || s2.StartsWith(reversedQuery, StringComparison.OrdinalIgnoreCase)))
                return true;

            // 3. يحتوي على
            if (s1.Contains(compactQuery, StringComparison.OrdinalIgnoreCase) ||
                s2.Contains(compactQuery, StringComparison.OrdinalIgnoreCase))
                return true;

            if (reversedQuery != null && (s1.Contains(reversedQuery, StringComparison.OrdinalIgnoreCase) || s2.Contains(reversedQuery, StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        })
        .OrderByDescending(entry =>
        {
            // إعطاء أولوية أولى لمدخلات ORNL المعتمدة بثابت غاما
            int sourcePriority = entry.IsOrnlSource ? 100 : 0;

            var s1 = entry.NuclideSymbol.Replace("-", "").Replace(" ", "").ToLowerInvariant();
            var s2 = entry.DisplaySymbol.Replace("-", "").Replace(" ", "").ToLowerInvariant();

            if (s1.Equals(compactQuery, StringComparison.OrdinalIgnoreCase) || s2.Equals(compactQuery, StringComparison.OrdinalIgnoreCase))
                return sourcePriority + 50;

            if (reversedQuery != null && (s1.Equals(reversedQuery, StringComparison.OrdinalIgnoreCase) || s2.Equals(reversedQuery, StringComparison.OrdinalIgnoreCase)))
                return sourcePriority + 45;

            if (s1.StartsWith(compactQuery, StringComparison.OrdinalIgnoreCase) || s2.StartsWith(compactQuery, StringComparison.OrdinalIgnoreCase))
                return sourcePriority + 30;

            if (reversedQuery != null && (s1.StartsWith(reversedQuery, StringComparison.OrdinalIgnoreCase) || s2.StartsWith(reversedQuery, StringComparison.OrdinalIgnoreCase)))
                return sourcePriority + 25;

            return sourcePriority + 10;
        })
        .ThenBy(e => GetNuclideSortKey(e.DisplaySymbol).Element, StringComparer.OrdinalIgnoreCase)
        .ThenBy(e => GetNuclideSortKey(e.DisplaySymbol).Mass)
        .ThenBy(e => GetNuclideSortKey(e.DisplaySymbol).Suffix, StringComparer.OrdinalIgnoreCase)
        .ToList();

        return results;
    }

    public bool OpenReferencePdf(int pageNumber = 0)
    {
        try
        {
            var pdfPath = GetReferencePdfPath();
            if (!File.Exists(pdfPath))
            {
                LoggerService.LogWarning($"IsotopeLibraryService: Reference PDF not found at {pdfPath}");
                return false;
            }

            // إذا كان هناك رقم صفحة محدد، نفتح بصيغة file:///path#page=XX
            if (pageNumber > 0)
            {
                try
                {
                    var fileUri = new Uri(pdfPath).AbsoluteUri + $"#page={pageNumber}";
                    var pagePsi = new ProcessStartInfo
                    {
                        FileName = fileUri,
                        UseShellExecute = true
                    };
                    Process.Start(pagePsi);
                    return true;
                }
                catch (Exception ex)
                {
                    LoggerService.LogWarning($"IsotopeLibraryService: Failed to open with #page={pageNumber}, falling back to file path: {ex.Message}");
                }
            }

            var psi = new ProcessStartInfo
            {
                FileName = pdfPath,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            LoggerService.LogError("IsotopeLibraryService: Failed to open reference PDF", ex);
            return false;
        }
    }

    public bool OpenIcrpPdf()
    {
        try
        {
            var pdfPath = GetIcrpPdfPath();
            if (!File.Exists(pdfPath))
            {
                LoggerService.LogWarning($"IsotopeLibraryService: ICRP PDF not found at {pdfPath}");
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = pdfPath,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            LoggerService.LogError("IsotopeLibraryService: Failed to open ICRP PDF", ex);
            return false;
        }
    }
}
