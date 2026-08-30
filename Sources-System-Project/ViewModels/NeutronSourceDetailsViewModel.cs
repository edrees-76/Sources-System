using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Models;

namespace Sources.ViewModels;

public partial class NeutronSourceDetailsViewModel : ObservableObject
{
    [ObservableProperty] private NeutronSource _neutronSource;

    public NeutronSourceDetailsViewModel(NeutronSource source)
    {
        _neutronSource = source;
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
    public string LocationDisplay => NeutronSource.Location?.LocationName ?? "غير محدد";
    public string LocationDetails => NeutronSource.Location != null ? $"مبنى: {NeutronSource.Location.Building ?? "-"} | غرفة: {NeutronSource.Location.Room ?? "-"}" : "-";
    public string StatusArabic => NeutronSource.ArabicStatus;
    public string StatusColor => NeutronSource.StatusColor;
    public string Notes => !string.IsNullOrWhiteSpace(NeutronSource.Notes) ? NeutronSource.Notes : "-";
    public string CreatedAtFormatted => NeutronSource.CreatedAt.ToString("yyyy/MM/dd HH:mm");
    public string AddedBy => NeutronSource.AddedBy.HasValue ? NeutronSource.AddedBy.Value.ToString() : "-";
}
