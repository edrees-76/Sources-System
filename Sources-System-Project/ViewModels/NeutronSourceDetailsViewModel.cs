using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;

namespace Sources.ViewModels;

/// <summary>
/// نموذج عرض تفاصيل مصدر نيتروني
/// </summary>
public partial class NeutronSourceDetailsViewModel : ObservableObject
{
    private readonly IUserService? _userService;

    [ObservableProperty] private NeutronSource _neutronSource;

    public NeutronSourceDetailsViewModel(NeutronSource source, IUserService? userService = null)
    {
        _neutronSource = source ?? throw new ArgumentNullException(nameof(source));
        _userService = userService ?? (App.ServiceProvider?.GetService(typeof(IUserService)) as IUserService);
    }

    public string SourceCode => NeutronSource.SourceCode;
    public string SerialNumber => !string.IsNullOrWhiteSpace(NeutronSource.SerialNumber) ? NeutronSource.SerialNumber : "-";
    public string TypeCode => NeutronSource.NeutronSourceType?.Code ?? "-";
    public string TypeNameAr => !string.IsNullOrWhiteSpace(NeutronSource.NeutronSourceType?.NameAr) ? NeutronSource.NeutronSourceType.NameAr : (NeutronSource.NeutronSourceType?.NameEn ?? "-");
    public string TypeNameEn => !string.IsNullOrWhiteSpace(NeutronSource.NeutronSourceType?.NameEn) ? NeutronSource.NeutronSourceType.NameEn : "-";
    public string ReactionType => !string.IsNullOrWhiteSpace(NeutronSource.NeutronSourceType?.ReactionType) ? NeutronSource.NeutronSourceType.ReactionType : "-";
    public string TargetMaterial => !string.IsNullOrWhiteSpace(NeutronSource.NeutronSourceType?.TargetMaterial) ? NeutronSource.NeutronSourceType.TargetMaterial : "-";
    public string ParentNuclide => !string.IsNullOrWhiteSpace(NeutronSource.NeutronSourceType?.ParentNuclide) ? NeutronSource.NeutronSourceType.ParentNuclide : "-";
    public string HalfLifeDisplay => NeutronSource.NeutronSourceType != null && NeutronSource.NeutronSourceType.HalfLife > 0 ? $"{NeutronSource.NeutronSourceType.HalfLife} {NeutronSource.NeutronSourceType.HalfLifeUnit}" : "-";
    public string EnergyDisplay => NeutronSource.NeutronSourceType?.AverageNeutronEnergyMeV.HasValue == true ? $"{NeutronSource.NeutronSourceType.AverageNeutronEnergyMeV.Value:N2} MeV" : "-";
    public string YieldDisplay => NeutronSource.NeutronSourceType?.TypicalNeutronYield.HasValue == true ? $"{NeutronSource.NeutronSourceType.TypicalNeutronYield.Value:E2} n/s" : "-";

    public string EmissionRateFormatted => $"{NeutronSource.EmissionRate:N2} n/s";
    public string UncertaintyFormatted => NeutronSource.RelativeExpandedUncertaintyPercent.HasValue ? $"{NeutronSource.RelativeExpandedUncertaintyPercent.Value:N1}%" : "-";
    public string CalibrationDateFormatted => NeutronSource.CalibrationDate?.ToString("yyyy/MM/dd") ?? "-";
    public string LocationDisplay => NeutronSource.Location?.LocationName ?? (TranslationHelper.GetString("TextUnspecified") ?? "غير محدد");
    public string LocationDetails
    {
        get
        {
            if (NeutronSource.Location == null) return "-";
            string bldg = TranslationHelper.GetString("ColBuilding") ?? "مبنى";
            string rm = TranslationHelper.GetString("ColRoom") ?? "غرفة";
            return $"{bldg}: {NeutronSource.Location.Building ?? "-"} | {rm}: {NeutronSource.Location.Room ?? "-"}";
        }
    }
    public string StatusArabic => NeutronSource.ArabicStatus;
    public string StatusColor => NeutronSource.StatusColor;
    public string Notes => !string.IsNullOrWhiteSpace(NeutronSource.Notes) ? NeutronSource.Notes : "-";
    public string CreatedAtFormatted => NeutronSource.CreatedAt.ToString("yyyy/MM/dd HH:mm");
    public string UpdatedAtFormatted => "-";
    
    public string AddedBy
    {
        get
        {
            if (!NeutronSource.AddedBy.HasValue) return "-";
            try
            {
                var user = _userService?.GetUserById(NeutronSource.AddedBy.Value);
                if (user != null && !string.IsNullOrWhiteSpace(user.FullName))
                {
                    return user.FullName;
                }
                if (user != null && !string.IsNullOrWhiteSpace(user.Username))
                {
                    return user.Username;
                }
            }
            catch { }
            return NeutronSource.AddedBy.Value.ToString();
        }
    }
}
