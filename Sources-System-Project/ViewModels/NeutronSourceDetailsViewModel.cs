using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
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
    private readonly ISourceCertificateService? _certificateService;
    private readonly INeutronDecayCalculationService _decayService;

    [ObservableProperty] private NeutronSource _neutronSource;
    [ObservableProperty] private int _selectedTabIndex = 0;

    public ObservableCollection<SourceCertificate> Certificates { get; } = new();
    [ObservableProperty] private bool _hasCertificates;

    public NeutronSourceDetailsViewModel(
        NeutronSource source,
        IUserService? userService = null,
        ISourceCertificateService? certificateService = null,
        INeutronDecayCalculationService? decayService = null)
    {
        _neutronSource = source ?? throw new ArgumentNullException(nameof(source));
        _userService = userService ?? (App.ServiceProvider?.GetService(typeof(IUserService)) as IUserService);
        _certificateService = certificateService ?? (App.ServiceProvider?.GetService(typeof(ISourceCertificateService)) as ISourceCertificateService);
        _decayService = decayService ?? (App.ServiceProvider?.GetService(typeof(INeutronDecayCalculationService)) as INeutronDecayCalculationService) ?? new NeutronDecayCalculationService();

        LoadCertificates();
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
    public string EnergyDisplay => NeutronSource.NeutronSourceType?.MeanNeutronEnergyMeV.HasValue == true ? $"{NeutronSource.NeutronSourceType.MeanNeutronEnergyMeV.Value:N2} MeV" : "-";

    // ─── معدل الانبعاث المُعاير ومعدل الانبعاث الحالي وحقول الشهادة ───
    public string CalibratedEmissionRateFormatted => NeutronSource.EmissionRateFormatted;
    public string EmissionRateFormatted => CalibratedEmissionRateFormatted; // للتوافق العكسي

    public string EmissionCalibrationDateFormatted => NeutronSource.EmissionCalibrationDate.HasValue 
        ? NeutronSource.EmissionCalibrationDate.Value.ToString("yyyy/MM/dd") 
        : "تاريخ المعايرة غير مسجّل";

    public string CalibrationDateFormatted => NeutronSource.CalibrationDate?.ToString("yyyy/MM/dd") ?? "-";

    public NeutronDecayResult DecayResult => _decayService.CalculateCurrentEmissionRate(NeutronSource);
    public string CurrentEmissionRateDisplay => DecayResult.DisplayRate;
    public bool IsCurrentEmissionRateCalculated => DecayResult.IsCalculated;

    public string CalibrationReferenceDisplay => !string.IsNullOrWhiteSpace(NeutronSource.CalibrationReference) 
        ? NeutronSource.CalibrationReference 
        : "غير مسجّل";

    public string AnisotropyFactorDisplay => NeutronSource.AnisotropyFactor.HasValue 
        ? NeutronSource.AnisotropyFactor.Value.ToString("F3") 
        : "غير مقاس";

    public string UncertaintyFormatted => NeutronSource.RelativeExpandedUncertaintyPercent.HasValue ? $"{NeutronSource.RelativeExpandedUncertaintyPercent.Value:N1}%" : "-";
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
    
    public string AddedByName => NeutronSource.AddedByName;

    public void LoadCertificates()
    {
        Certificates.Clear();
        if (_certificateService != null)
        {
            try
            {
                var list = _certificateService.GetCertificates(NeutronSource.Id, "Neutron");
                foreach (var cert in list)
                {
                    Certificates.Add(cert);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("NeutronSourceDetailsViewModel: Failed to load certificates", ex);
            }
        }
        HasCertificates = Certificates.Count > 0;
    }

    [RelayCommand]
    private void AttachCertificate()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = TranslationHelper.GetString("BtnAttachCertificate") ?? "إرفاق شهادة أو مستند",
                Filter = "كل الملفات (*.*)|*.*|ملفات PDF (*.pdf)|*.pdf|مستندات Word (*.docx;*.doc)|*.docx;*.doc|صور (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            if (_certificateService == null)
            {
                DialogHelper.ShowError("خدمة الشهادات غير متوفرة", "خطأ");
                return;
            }

            var attachedBy = _userService?.CurrentUser?.FullName;
            if (string.IsNullOrWhiteSpace(attachedBy))
            {
                attachedBy = "غير معروف";
                LoggerService.LogWarning($"NeutronSourceDetailsViewModel.AttachCertificate: Current user is null or empty when attaching certificate for NeutronSource {NeutronSource.Id}. Falling back to '{attachedBy}'.");
            }
            _certificateService.AttachCertificate(NeutronSource.Id, "Neutron", dialog.FileName, attachedBy);
            LoadCertificates();

            DialogHelper.ShowInfo(
                TranslationHelper.GetString("MsgCertificateAttachedSuccess") ?? "تم إرفاق الشهادة بنجاح.",
                TranslationHelper.GetString("TabCertificates") ?? "الشهادات");
        }
        catch (Exception ex)
        {
            LoggerService.LogError("NeutronSourceDetailsViewModel: Failed to attach certificate", ex);
            DialogHelper.ShowError($"تعذر إرفاق الشهادة: {ex.Message}", "خطأ");
        }
    }

    [RelayCommand]
    private void OpenCertificate(SourceCertificate? cert)
    {
        if (cert == null || _certificateService == null) return;

        try
        {
            var fullPath = Path.Combine(_certificateService.GetCertificatesFolder(), cert.StoredFileName);
            if (!File.Exists(fullPath))
            {
                DialogHelper.ShowWarning("ملف الشهادة غير موجود على القرص.", "تنبيه");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LoggerService.LogError("NeutronSourceDetailsViewModel: Failed to open certificate", ex);
            DialogHelper.ShowError($"تعذر فتح الشهادة: {ex.Message}", "خطأ");
        }
    }

    [RelayCommand]
    private void DownloadCertificate(SourceCertificate? cert)
    {
        if (cert == null || _certificateService == null) return;

        try
        {
            var ext = Path.GetExtension(cert.OriginalFileName);
            var dialog = new SaveFileDialog
            {
                Title = TranslationHelper.GetString("BtnDownloadCertificate") ?? "تنزيل نسخة من الشهادة",
                FileName = cert.OriginalFileName,
                Filter = !string.IsNullOrEmpty(ext) ? $"ملف (*{ext})|*{ext}|كل الملفات (*.*)|*.*" : "كل الملفات (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true) return;

            var success = _certificateService.DownloadCertificate(cert.Id, dialog.FileName);
            if (success)
            {
                DialogHelper.ShowInfo(
                    TranslationHelper.GetString("MsgCertificateDownloadedSuccess") ?? "تم تنزيل نسخة من الشهادة بنجاح.",
                    TranslationHelper.GetString("TabCertificates") ?? "الشهادات");
            }
            else
            {
                DialogHelper.ShowError("تعذر تنزيل الشهادة. تأكد من وجود الملف الأصلي.", "خطأ");
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError("NeutronSourceDetailsViewModel: Failed to download certificate", ex);
            DialogHelper.ShowError($"تعذر تنزيل الشهادة: {ex.Message}", "خطأ");
        }
    }

    [RelayCommand]
    private void DeleteCertificate(SourceCertificate? cert)
    {
        if (cert == null || _certificateService == null) return;

        var confirmed = DialogHelper.ShowConfirmation(
            TranslationHelper.GetString("MsgConfirmDeleteCertificate") ?? "هل أنت متأكد من حذف هذه الشهادة نهائياً؟",
            TranslationHelper.GetString("BtnDeleteCertificate") ?? "حذف الشهادة");

        if (!confirmed) return;

        try
        {
            var deletedBy = _userService?.CurrentUser?.FullName;
            if (string.IsNullOrWhiteSpace(deletedBy))
            {
                deletedBy = "غير معروف";
                LoggerService.LogWarning($"NeutronSourceDetailsViewModel.DeleteCertificate: Current user is null or empty when deleting certificate {cert.Id} for NeutronSource {NeutronSource.Id}. Falling back to '{deletedBy}'.");
            }
            _certificateService.DeleteCertificate(cert.Id, deletedBy);
            LoadCertificates();

            DialogHelper.ShowInfo(
                TranslationHelper.GetString("MsgCertificateDeletedSuccess") ?? "تم حذف الشهادة بنجاح.",
                TranslationHelper.GetString("TabCertificates") ?? "الشهادات");
        }
        catch (Exception ex)
        {
            LoggerService.LogError("NeutronSourceDetailsViewModel: Failed to delete certificate", ex);
            DialogHelper.ShowError($"تعذر حذف الشهادة: {ex.Message}", "خطأ");
        }
    }
}
