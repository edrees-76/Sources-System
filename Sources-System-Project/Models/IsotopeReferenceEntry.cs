using System.Text.Json.Serialization;

namespace Sources.Models;

public enum ReferenceSourceType
{
    ORNL_RSIC_45_R1,
    ICRP_107
}

public class IsotopeReferenceEntry
{
    [JsonPropertyName("nuclide_symbol")]
    public string NuclideSymbol { get; set; } = string.Empty;

    /// <summary>
    /// نوع المرجع المعتمد للبيانات (ORNL أو ICRP 107)
    /// </summary>
    [JsonIgnore]
    public ReferenceSourceType SourceType { get; set; } = ReferenceSourceType.ORNL_RSIC_45_R1;

    [JsonIgnore]
    public bool IsOrnlSource => SourceType == ReferenceSourceType.ORNL_RSIC_45_R1;

    [JsonIgnore]
    public bool IsIcrpSource => SourceType == ReferenceSourceType.ICRP_107;

    /// <summary>
    /// الرقم التسلسلي الترتيبي للنظير في القائمة المعروضة حالياً (1, 2, 3...)
    /// </summary>
    [JsonIgnore]
    public int ItemIndex { get; set; } = 1;

    /// <summary>
    /// رمز النظير بالصيغة المعيارية للمنظومة (العنصر-الكتلة، مثل Co-60, Cs-137, Tc-99m, Ba-137m, F-18)
    /// </summary>
    [JsonIgnore]
    public string DisplaySymbol
    {
        get
        {
            if (string.IsNullOrWhiteSpace(NuclideSymbol)) return string.Empty;

            // If already in Element-Mass format (e.g. "Cs-137", "H-3")
            if (NuclideSymbol.Contains('-')) return NuclideSymbol;

            var match = System.Text.RegularExpressions.Regex.Match(NuclideSymbol.Trim(), @"^(\d+(?:m\d?)?)([A-Za-z]+)$");
            if (match.Success)
            {
                var mass = match.Groups[1].Value;
                var elem = match.Groups[2].Value;
                return $"{elem}-{mass}";
            }

            return NuclideSymbol;
        }
    }

    [JsonPropertyName("half_life")]
    public string HalfLife { get; set; } = string.Empty;

    /// <summary>
    /// عمر النصف منسق بصيغة علمية واضحة ومقروءة للعين (بما فيها الأسس العلمية مثل 1.28 × 10⁹ y)
    /// </summary>
    [JsonIgnore]
    public string DisplayHalfLife => FormatHalfLife(HalfLife);

    public static string FormatHalfLife(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "—";
        var s = raw.Trim();

        // 1. المطابقة للتدوين العلمي الصريح (ICRP أو القياسي: e.g. "1.251E+9 y", "1.50E+17 y", "2.99E-7 s", "4.923E10 y")
        var matchExp = System.Text.RegularExpressions.Regex.Match(s, @"^([\d\.]+)\s*[eE]([\+\-]?\d+)\s*([a-zA-Z]+)$");
        if (matchExp.Success)
        {
            var baseVal = matchExp.Groups[1].Value;
            var expVal = int.Parse(matchExp.Groups[2].Value);
            var expStr = expVal.ToString();
            var unit = matchExp.Groups[3].Value;
            return $"{baseVal} × 10{ToSuperscript(expStr)} {unit}";
        }

        // 2. المطابقة لتدوين فورتران المضغوط المعتمد في تقرير ORNL الأصلي (e.g. "1.28+9y", "4.2-6s", "7.7+4y", "1.4+10y")
        var matchFortran = System.Text.RegularExpressions.Regex.Match(s, @"^([\d\.]+)\s*([\+\-])(\d+)\s*([a-zA-Z]+)$");
        if (matchFortran.Success)
        {
            var baseVal = matchFortran.Groups[1].Value;
            var sign = matchFortran.Groups[2].Value;
            var expVal = int.Parse(matchFortran.Groups[3].Value);
            var expStr = sign == "-" ? $"-{expVal}" : expVal.ToString();
            var unit = matchFortran.Groups[4].Value;
            return $"{baseVal} × 10{ToSuperscript(expStr)} {unit}";
        }

        // 3. القيم القياسية العادية (e.g. "5.3y", "30.17 y", "6.0h", "74.0d", "15.0m")
        var matchStd = System.Text.RegularExpressions.Regex.Match(s, @"^([\d\.]+)\s*([a-zA-Z]+)$");
        if (matchStd.Success)
        {
            return $"{matchStd.Groups[1].Value} {matchStd.Groups[2].Value}";
        }

        return s;
    }

    private static string ToSuperscript(string numberStr)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in numberStr)
        {
            sb.Append(c switch
            {
                '0' => '⁰',
                '1' => '¹',
                '2' => '²',
                '3' => '³',
                '4' => '⁴',
                '5' => '⁵',
                '6' => '⁶',
                '7' => '⁷',
                '8' => '⁸',
                '9' => '⁹',
                '-' => '⁻',
                '+' => '⁺',
                _ => c
            });
        }
        return sb.ToString();
    }

    // ─── حقول ORNL / RSIC-45 / R1 ───
    [JsonPropertyName("specific_gamma_constant_raw")]
    public string SpecificGammaConstantRaw { get; set; } = string.Empty;

    [JsonPropertyName("specific_gamma_constant_formatted")]
    public string SpecificGammaConstantFormatted { get; set; } = string.Empty;

    [JsonPropertyName("specific_gamma_constant_value")]
    public double? SpecificGammaConstantValue { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "(mSv/h)/MBq at 1 meter";

    [JsonPropertyName("lead_thickness_95_percent_cm")]
    public double? LeadThickness95PercentCm { get; set; }

    [JsonPropertyName("linear_attenuation_coeff_cm_minus_1")]
    public double? LinearAttenuationCoeffCmMinus1 { get; set; }

    [JsonPropertyName("photon_emissions_count")]
    public int PhotonEmissionsCount { get; set; }

    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("source_reference")]
    public string SourceReference { get; set; } = "ORNL/RSIC-45/R1 (Table 2, May 1982)";

    // ─── حقول ICRP Publication 107 ───
    [JsonPropertyName("decay_mode")]
    public string? DecayMode { get; set; }

    [JsonPropertyName("alpha_energy_mev")]
    public double? AlphaEnergyMeV { get; set; }

    [JsonPropertyName("electron_energy_mev")]
    public double? ElectronEnergyMeV { get; set; }

    [JsonPropertyName("photon_energy_mev")]
    public double? PhotonEnergyMeV { get; set; }

    [JsonPropertyName("total_energy_mev")]
    public double? TotalEnergyMeV { get; set; }

    [JsonPropertyName("is_radioactive")]
    public bool IsRadioactive { get; set; } = true;

    // ─── خواص التنسيق المساعدة ───
    [JsonIgnore]
    public double? GammaMremPerUci => SpecificGammaConstantValue.HasValue ? SpecificGammaConstantValue.Value * 3.7 : null;

    [JsonIgnore]
    public string GammaMremPerUciFormatted => GammaMremPerUci.HasValue ? $"{GammaMremPerUci.Value:0.0000E+00}" : "—";

    [JsonIgnore]
    public string DisplayLeadThickness => LeadThickness95PercentCm.HasValue ? $"{LeadThickness95PercentCm.Value:0.###} cm" : "—";

    [JsonIgnore]
    public string DisplayAttenuationCoeff => LinearAttenuationCoeffCmMinus1.HasValue ? $"{LinearAttenuationCoeffCmMinus1.Value:0.###} cm⁻¹" : "—";

    [JsonIgnore]
    public string DisplayPageBadge => PageNumber > 0 ? $"ص {PageNumber}" : "ICRP 107";

    [JsonIgnore]
    public string DisplayAlphaEnergy => AlphaEnergyMeV.HasValue ? $"{AlphaEnergyMeV.Value:0.####} MeV" : "0.0 MeV";

    [JsonIgnore]
    public string DisplayElectronEnergy => ElectronEnergyMeV.HasValue ? $"{ElectronEnergyMeV.Value:0.####} MeV" : "0.0 MeV";

    [JsonIgnore]
    public string DisplayPhotonEnergy => PhotonEnergyMeV.HasValue ? $"{PhotonEnergyMeV.Value:0.####} MeV" : "0.0 MeV";

    [JsonIgnore]
    public string DisplayTotalEnergy => TotalEnergyMeV.HasValue ? $"{TotalEnergyMeV.Value:0.####} MeV" : "0.0 MeV";

    [JsonIgnore]
    public string DisplayDecayMode => !string.IsNullOrWhiteSpace(DecayMode) ? DecayMode : "—";

    /// <summary>
    /// توليد نص منسق وشامل لجميع بيانات النظير للنسخ المباشر إلى الحافظة
    /// </summary>
    public string GetFormattedDetailsText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"رمز النظير: {DisplaySymbol}");
        sb.AppendLine($"نصف العمر: {DisplayHalfLife}");

        if (IsOrnlSource)
        {
            sb.AppendLine($"ثابت غاما (الوحدة الدولية SI): {SpecificGammaConstantFormatted} (mSv/h)/MBq عند 1 متر");
            sb.AppendLine($"ثابت غاما (الوحدة المرجعية): {GammaMremPerUciFormatted} (mrem/h)/µCi عند 1 متر (× 3.7)");
            sb.AppendLine($"سُمك درع الرصاص لتوهين 95% (T95%): {DisplayLeadThickness}");
            sb.AppendLine($"معامل التوهين الخطي المتوسط (µ): {DisplayAttenuationCoeff}");
            sb.AppendLine($"عدد خطوط الانبعاث الفوتونية: {PhotonEmissionsCount}");
            sb.AppendLine($"المرجع: {SourceReference} - {DisplayPageBadge}");
            if (!string.IsNullOrWhiteSpace(Notes))
            {
                sb.AppendLine($"ملاحظات: {Notes}");
            }
        }
        else if (IsIcrpSource)
        {
            sb.AppendLine($"نمط الانحلال الإشعاعي: {DisplayDecayMode}");
            sb.AppendLine($"طاقة ألفا (Alpha): {DisplayAlphaEnergy}");
            sb.AppendLine($"طاقة الإلكترونات (Electron): {DisplayElectronEnergy}");
            sb.AppendLine($"طاقة الفوتونات (Photon): {DisplayPhotonEnergy}");
            sb.AppendLine($"إجمالي الطاقة المنبعثة: {DisplayTotalEnergy}");
            sb.AppendLine($"المرجع: {SourceReference}");
            sb.AppendLine("ملاحظة: البيانات المعروضة من ICRP Publication 107 لأغراض مرجعية عامة فقط، ولا تُستخدم لحساب معدل الجرعة.");
        }

        return sb.ToString().TrimEnd();
    }
}
