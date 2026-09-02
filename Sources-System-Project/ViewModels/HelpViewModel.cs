using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Sources.ViewModels;

public class RoleFilterOption
{
    public string RoleKey { get; set; } = HelpRoles.All;
    public string TitleKey { get; set; } = "HelpFilterAll";

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

public partial class HelpViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<HelpTopic> _topics = new();

    [ObservableProperty]
    private ObservableCollection<HelpTopic> _filteredTopics = new();

    [ObservableProperty]
    private HelpTopic? _selectedTopic;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<RoleFilterOption> _roleFilterOptions = new();

    [ObservableProperty]
    private RoleFilterOption? _selectedRoleFilter;

    [ObservableProperty]
    private bool _hasSearchResults = true;

    public HelpViewModel()
    {
        InitializeFilterOptions();
        InitializeTopics();
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedRoleFilterChanged(RoleFilterOption? value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    public void GoToTargetView(string? viewName)
    {
        if (string.IsNullOrWhiteSpace(viewName)) return;
        var mainVm = App.ServiceProvider.GetService(typeof(MainViewModel)) as MainViewModel;
        mainVm?.NavigateTo(viewName);
    }

    [RelayCommand]
    public void OpenReferencePdf()
    {
        var libraryService = App.ServiceProvider.GetService(typeof(IIsotopeLibraryService)) as IIsotopeLibraryService;
        var success = libraryService?.OpenReferencePdf() ?? false;
        if (!success)
        {
            DialogHelper.ShowWarning(
                TranslationHelper.GetString("MsgErrOpenRefPdf") ?? "تعذر فتح ملف المرجع الأصلي. تأكد من وجود ملف 14724519.pdf وتوفر برنامج لقراءة ملفات PDF على جهازك.",
                TranslationHelper.GetString("TitleReferencePdf") ?? "الملف المرجعي ORNL"
            );
        }
    }

    [RelayCommand]
    public void SelectTopic(HelpTopic? topic)
    {
        if (topic != null)
        {
            SelectedTopic = topic;
        }
    }

    private void InitializeFilterOptions()
    {
        RoleFilterOptions = new ObservableCollection<RoleFilterOption>
        {
            new RoleFilterOption { RoleKey = HelpRoles.All, TitleKey = "HelpFilterAll" },
            new RoleFilterOption { RoleKey = HelpRoles.Storekeeper, TitleKey = "HelpFilterStorekeeper" },
            new RoleFilterOption { RoleKey = HelpRoles.SafetyOfficer, TitleKey = "HelpFilterSafetyOfficer" }
        };
        SelectedRoleFilter = RoleFilterOptions[0];
    }

    private void ApplyFilter()
    {
        var role = SelectedRoleFilter?.RoleKey ?? HelpRoles.All;
        var query = SearchText?.Trim() ?? string.Empty;

        var results = Topics.Where(t =>
        {
            // 1. فلترة الدور
            bool roleMatch = role == HelpRoles.All || t.Roles.Contains(role) || t.Roles.Contains(HelpRoles.All);
            if (!roleMatch) return false;

            // 2. فلترة البحث
            if (string.IsNullOrEmpty(query)) return true;

            return t.DisplayTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   t.DisplaySubtitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   t.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        FilteredTopics = new ObservableCollection<HelpTopic>(results);
        HasSearchResults = FilteredTopics.Count > 0;

        if (SelectedTopic == null || !FilteredTopics.Contains(SelectedTopic))
        {
            SelectedTopic = FilteredTopics.FirstOrDefault();
        }
    }

    private void InitializeTopics()
    {
        Topics = new ObservableCollection<HelpTopic>
        {
            // ════════════════════════════════════════════════════════
            // 1. المقدمة ونظرة عامة
            // ════════════════════════════════════════════════════════
            new HelpTopic
            {
                Id = "Intro",
                TitleKey = "HelpTopicIntroTitle",
                SubtitleKey = "HelpTopicIntroSubtitle",
                IconKind = "InformationOutline",
                TargetViewName = "Dashboard",
                TargetViewButtonTextKey = "HelpBtnGoToDashboard",
                Roles = new() { HelpRoles.All, HelpRoles.Storekeeper, HelpRoles.SafetyOfficer },
                Keywords = "مقدمة دخول نظام واجهة تنقل بداية login overview dashboard",
                Sections = new()
                {
                    new HelpSection
                    {
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpIntroLeadText", IsLead = true }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpIntroSectionWorkflow",
                        IconKind = "Navigation",
                        Blocks = new()
                        {
                            new HelpStepBlock { StepNumber = "1", TitleKey = "HelpIntroStep1Title", DescriptionKey = "HelpIntroStep1Desc" },
                            new HelpStepBlock { StepNumber = "2", TitleKey = "HelpIntroStep2Title", DescriptionKey = "HelpIntroStep2Desc" },
                            new HelpStepBlock { StepNumber = "3", TitleKey = "HelpIntroStep3Title", DescriptionKey = "HelpIntroStep3Desc" },
                            new HelpTipBlock { TextKey = "HelpIntroTip1" }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpIntroSectionUI",
                        IconKind = "Tune",
                        Blocks = new()
                        {
                            new HelpBulletListBlock
                            {
                                ItemKeys = new()
                                {
                                    "HelpIntroBullet1",
                                    "HelpIntroBullet2",
                                    "HelpIntroBullet3"
                                }
                            }
                        }
                    }
                }
            },

            // ════════════════════════════════════════════════════════
            // 2. لوحة القيادة
            // ════════════════════════════════════════════════════════
            new HelpTopic
            {
                Id = "Dashboard",
                TitleKey = "HelpTopicDashboardTitle",
                SubtitleKey = "HelpTopicDashboardSubtitle",
                IconKind = "ViewDashboard",
                TargetViewName = "Dashboard",
                TargetViewButtonTextKey = "HelpBtnGoToDashboard",
                Roles = new() { HelpRoles.All, HelpRoles.SafetyOfficer },
                Keywords = "لوحة القيادة مؤشرات kpi إحصائيات رسوم بيانية تنبيهات dashboard charts",
                Sections = new()
                {
                    new HelpSection
                    {
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpDashboardLeadText", IsLead = true }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpDashboardSectionKpi",
                        IconKind = "ChartBox",
                        Blocks = new()
                        {
                            new HelpBulletListBlock
                            {
                                ItemKeys = new()
                                {
                                    "HelpDashboardBullet1",
                                    "HelpDashboardBullet2",
                                    "HelpDashboardBullet3"
                                }
                            },
                            new HelpTipBlock { TextKey = "HelpDashboardTip1" }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpDashboardSectionCharts",
                        IconKind = "ChartLine",
                        Blocks = new()
                        {
                            new HelpStepBlock { StepNumber = "1", TitleKey = "HelpDashboardStep1Title", DescriptionKey = "HelpDashboardStep1Desc" },
                            new HelpStepBlock { StepNumber = "2", TitleKey = "HelpDashboardStep2Title", DescriptionKey = "HelpDashboardStep2Desc" }
                        }
                    }
                }
            },

            // ════════════════════════════════════════════════════════
            // 3. النظائر المشعة ومنطق التنبيهات
            // ════════════════════════════════════════════════════════
            new HelpTopic
            {
                Id = "Radioisotopes",
                TitleKey = "HelpTopicRadioisotopesTitle",
                SubtitleKey = "HelpTopicRadioisotopesSubtitle",
                IconKind = "Atom",
                TargetViewName = "Radioisotopes",
                TargetViewButtonTextKey = "HelpBtnGoToRadioisotopes",
                Roles = new() { HelpRoles.All, HelpRoles.SafetyOfficer },
                Keywords = "نظائر مشعة نصف عمر اضمحلال تنبيهات Cs-137 Co-60 isotopes half-life",
                Sections = new()
                {
                    new HelpSection
                    {
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpRadioisotopesLeadText", IsLead = true }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpRadioisotopesSectionAlerts",
                        IconKind = "AlertDecagram",
                        Blocks = new()
                        {
                            new HelpStepBlock { StepNumber = "1", TitleKey = "HelpRadioisotopesStep1Title", DescriptionKey = "HelpRadioisotopesStep1Desc" },
                            new HelpStepBlock { StepNumber = "2", TitleKey = "HelpRadioisotopesStep2Title", DescriptionKey = "HelpRadioisotopesStep2Desc" },
                            new HelpStepBlock { StepNumber = "3", TitleKey = "HelpRadioisotopesStep3Title", DescriptionKey = "HelpRadioisotopesStep3Desc" },
                            new HelpWarningBlock { TextKey = "HelpRadioisotopesWarning1" }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpSectionGammaReferenceTitle",
                        IconKind = "BookOpenPageVariantOutline",
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpGammaRefDescription" },
                            new HelpStepBlock { StepNumber = "1", TitleKey = "HelpGammaRefAuthorsTitle", DescriptionKey = "HelpGammaRefAuthorsDesc" },
                            new HelpStepBlock { StepNumber = "2", TitleKey = "HelpGammaRefUnitsTitle", DescriptionKey = "HelpGammaRefUnitsDesc" },
                            new HelpTipBlock { TextKey = "HelpGammaRefTip" }
                        }
                    }
                }
            },

            // ════════════════════════════════════════════════════════
            // 4. جرد المصادر وسيناريوهات الاستلام
            // ════════════════════════════════════════════════════════
            new HelpTopic
            {
                Id = "Sources",
                TitleKey = "HelpTopicSourcesTitle",
                SubtitleKey = "HelpTopicSourcesSubtitle",
                IconKind = "Radioactive",
                TargetViewName = "Sources",
                TargetViewButtonTextKey = "HelpBtnGoToSources",
                Roles = new() { HelpRoles.All, HelpRoles.Storekeeper, HelpRoles.SafetyOfficer },
                Keywords = "مصادر جرد إضافة فحص استلام معايرة نشاط ابتدائي serial number inventory sources",
                Sections = new()
                {
                    new HelpSection
                    {
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpSourcesLeadText", IsLead = true }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpSourcesSectionScenario",
                        IconKind = "ClipboardCheckMultiple",
                        Blocks = new()
                        {
                            new HelpStepBlock { StepNumber = "1", TitleKey = "HelpSourcesStep1Title", DescriptionKey = "HelpSourcesStep1Desc" },
                            new HelpStepBlock { StepNumber = "2", TitleKey = "HelpSourcesStep2Title", DescriptionKey = "HelpSourcesStep2Desc" },
                            new HelpStepBlock { StepNumber = "3", TitleKey = "HelpSourcesStep3Title", DescriptionKey = "HelpSourcesStep3Desc" },
                            new HelpStepBlock { StepNumber = "4", TitleKey = "HelpSourcesStep4Title", DescriptionKey = "HelpSourcesStep4Desc" },
                            new HelpTipBlock { TextKey = "HelpSourcesTip1" }
                        }
                    }
                }
            },

            // ════════════════════════════════════════════════════════
            // 5. المواقع ومستودعات التخزين
            // ════════════════════════════════════════════════════════
            new HelpTopic
            {
                Id = "Locations",
                TitleKey = "HelpTopicLocationsTitle",
                SubtitleKey = "HelpTopicLocationsSubtitle",
                IconKind = "MapMarker",
                TargetViewName = "Locations",
                TargetViewButtonTextKey = "HelpBtnGoToLocations",
                Roles = new() { HelpRoles.All, HelpRoles.Storekeeper },
                Keywords = "مواقع قاصة مستودع رف سعة تخزين أمان locations vaults safe",
                Sections = new()
                {
                    new HelpSection
                    {
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpLocationsLeadText", IsLead = true }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpLocationsSectionGuide",
                        IconKind = "Domain",
                        Blocks = new()
                        {
                            new HelpStepBlock { StepNumber = "1", TitleKey = "HelpLocationsStep1Title", DescriptionKey = "HelpLocationsStep1Desc" },
                            new HelpStepBlock { StepNumber = "2", TitleKey = "HelpLocationsStep2Title", DescriptionKey = "HelpLocationsStep2Desc" },
                            new HelpStepBlock { StepNumber = "3", TitleKey = "HelpLocationsStep3Title", DescriptionKey = "HelpLocationsStep3Desc" },
                            new HelpTipBlock { TextKey = "HelpLocationsTip1" }
                        }
                    }
                }
            },

            // ════════════════════════════════════════════════════════
            // 6. نظام ودورة الاستعارة
            // ════════════════════════════════════════════════════════
            new HelpTopic
            {
                Id = "Borrowing",
                TitleKey = "HelpTopicBorrowingTitle",
                SubtitleKey = "HelpTopicBorrowingSubtitle",
                IconKind = "BookArrowRight",
                TargetViewName = "Borrowing",
                TargetViewButtonTextKey = "HelpBtnGoToBorrowing",
                Roles = new() { HelpRoles.All, HelpRoles.Storekeeper },
                Keywords = "استعارة صرف إرجاع طلب موافقة مشرف تسليم تأخير borrowing circulation",
                Sections = new()
                {
                    new HelpSection
                    {
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpBorrowingLeadText", IsLead = true }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpBorrowingSectionWorkflow",
                        IconKind = "ProgressClock",
                        Blocks = new()
                        {
                            new HelpStepBlock { StepNumber = "1", TitleKey = "HelpBorrowingStep1Title", DescriptionKey = "HelpBorrowingStep1Desc" },
                            new HelpStepBlock { StepNumber = "2", TitleKey = "HelpBorrowingStep2Title", DescriptionKey = "HelpBorrowingStep2Desc" },
                            new HelpStepBlock { StepNumber = "3", TitleKey = "HelpBorrowingStep3Title", DescriptionKey = "HelpBorrowingStep3Desc" },
                            new HelpStepBlock { StepNumber = "4", TitleKey = "HelpBorrowingStep4Title", DescriptionKey = "HelpBorrowingStep4Desc" },
                            new HelpWarningBlock { TextKey = "HelpBorrowingWarning1" }
                        }
                    }
                }
            },

            // ════════════════════════════════════════════════════════
            // 7. حاسبة النشاط وتتبع الاضمحلال
            // ════════════════════════════════════════════════════════
            new HelpTopic
            {
                Id = "ActivityCalculator",
                TitleKey = "HelpTopicCalculatorTitle",
                SubtitleKey = "HelpTopicCalculatorSubtitle",
                IconKind = "Calculator",
                TargetViewName = "ActivityCalculator",
                TargetViewButtonTextKey = "HelpBtnGoToCalculator",
                Roles = new() { HelpRoles.All, HelpRoles.Storekeeper, HelpRoles.SafetyOfficer },
                Keywords = "حاسبة نشاط اضمحلال زمن معادلة فيزياء نووية becquerel curie calculator decay",
                Sections = new()
                {
                    new HelpSection
                    {
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpCalculatorLeadText", IsLead = true }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpCalculatorSectionFeatures",
                        IconKind = "FunctionVariant",
                        Blocks = new()
                        {
                            new HelpStepBlock { StepNumber = "1", TitleKey = "HelpCalculatorStep1Title", DescriptionKey = "HelpCalculatorStep1Desc" },
                            new HelpStepBlock { StepNumber = "2", TitleKey = "HelpCalculatorStep2Title", DescriptionKey = "HelpCalculatorStep2Desc" },
                            new HelpStepBlock { StepNumber = "3", TitleKey = "HelpCalculatorStep3Title", DescriptionKey = "HelpCalculatorStep3Desc" },
                            new HelpExampleBlock { TitleKey = "HelpCalculatorExample1Title", ContentKey = "HelpCalculatorExample1Content" }
                        }
                    }
                }
            },

            // ════════════════════════════════════════════════════════
            // 8. مركز التقارير والرقابة
            // ════════════════════════════════════════════════════════
            new HelpTopic
            {
                Id = "Reports",
                TitleKey = "HelpTopicReportsTitle",
                SubtitleKey = "HelpTopicReportsSubtitle",
                IconKind = "FileChart",
                TargetViewName = "Reports",
                TargetViewButtonTextKey = "HelpBtnGoToReports",
                Roles = new() { HelpRoles.All, HelpRoles.SafetyOfficer },
                Keywords = "تقارير تصدير pdf excel طباعة جرد رقابة استعارة reports export",
                Sections = new()
                {
                    new HelpSection
                    {
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpReportsLeadText", IsLead = true }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpReportsSectionTypes",
                        IconKind = "FileDocumentMultipleOutline",
                        Blocks = new()
                        {
                            new HelpBulletListBlock
                            {
                                ItemKeys = new()
                                {
                                    "HelpReportsBullet1",
                                    "HelpReportsBullet2",
                                    "HelpReportsBullet3",
                                    "HelpReportsBullet4",
                                    "HelpReportsBullet5"
                                }
                            },
                            new HelpTipBlock { TextKey = "HelpReportsTip1" }
                        }
                    }
                }
            },

            // ════════════════════════════════════════════════════════
            // 9. الإعدادات وإدارة المستخدمين
            // ════════════════════════════════════════════════════════
            new HelpTopic
            {
                Id = "Settings",
                TitleKey = "HelpTopicSettingsTitle",
                SubtitleKey = "HelpTopicSettingsSubtitle",
                IconKind = "Cog",
                TargetViewName = "Settings",
                TargetViewButtonTextKey = "HelpBtnGoToSettings",
                Roles = new() { HelpRoles.All, HelpRoles.SafetyOfficer },
                Keywords = "إعدادات مستخدمين صلاحيات نسخ احتياطي تلقائي أمان ثيم settings users backup security",
                Sections = new()
                {
                    new HelpSection
                    {
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpSettingsLeadText", IsLead = true }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpSettingsSectionUsers",
                        IconKind = "AccountGroup",
                        Blocks = new()
                        {
                            new HelpStepBlock { StepNumber = "1", TitleKey = "HelpSettingsStep1Title", DescriptionKey = "HelpSettingsStep1Desc" },
                            new HelpStepBlock { StepNumber = "2", TitleKey = "HelpSettingsStep2Title", DescriptionKey = "HelpSettingsStep2Desc" }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpSettingsSectionBackup",
                        IconKind = "CloudUpload",
                        Blocks = new()
                        {
                            new HelpStepBlock { StepNumber = "3", TitleKey = "HelpSettingsStep3Title", DescriptionKey = "HelpSettingsStep3Desc" },
                            new HelpWarningBlock { TextKey = "HelpSettingsWarning1" }
                        }
                    }
                }
            },

            // ════════════════════════════════════════════════════════
            // 10. الأسئلة الشائعة (FAQ)
            // ════════════════════════════════════════════════════════
            new HelpTopic
            {
                Id = "Faq",
                TitleKey = "HelpTopicFaqTitle",
                SubtitleKey = "HelpTopicFaqSubtitle",
                IconKind = "HelpCircleOutline",
                Roles = new() { HelpRoles.All, HelpRoles.Storekeeper, HelpRoles.SafetyOfficer },
                Keywords = "أسئلة شائعة استفسارات نصف عمر تعديل قفل خمول faq questions answers",
                Sections = new()
                {
                    new HelpSection
                    {
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpFaqLeadText", IsLead = true }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpFaqSectionGeneral",
                        IconKind = "ForumOutline",
                        Blocks = new()
                        {
                            new HelpFaqBlock { QuestionKey = "HelpFaqQ1", AnswerKey = "HelpFaqA1" },
                            new HelpFaqBlock { QuestionKey = "HelpFaqQ2", AnswerKey = "HelpFaqA2" },
                            new HelpFaqBlock { QuestionKey = "HelpFaqQ3", AnswerKey = "HelpFaqA3" },
                            new HelpFaqBlock { QuestionKey = "HelpFaqQ4", AnswerKey = "HelpFaqA4" },
                            new HelpFaqBlock { QuestionKey = "HelpFaqQ5", AnswerKey = "HelpFaqA5" }
                        }
                    }
                }
            },

            // ════════════════════════════════════════════════════════
            // 11. آخر التحديثات والميزات
            // ════════════════════════════════════════════════════════
            new HelpTopic
            {
                Id = "WhatsNew",
                TitleKey = "HelpTopicWhatsNewTitle",
                SubtitleKey = "HelpTopicWhatsNewSubtitle",
                IconKind = "NewspaperVariantOutline",
                Roles = new() { HelpRoles.All, HelpRoles.SafetyOfficer },
                Keywords = "تحديثات ميزات جديدة نسخ احتياطي حاسبة نشاط جديد whats new updates",
                Sections = new()
                {
                    new HelpSection
                    {
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpWhatsNewLeadText", IsLead = true }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpWhatsNewSectionRecent",
                        IconKind = "StarCircleOutline",
                        Blocks = new()
                        {
                            new HelpWhatsNewBlock { VersionBadge = "v2.5", TagType = "Backup", TitleKey = "HelpWhatsNew1Title", DescriptionKey = "HelpWhatsNew1Desc" },
                            new HelpWhatsNewBlock { VersionBadge = "v2.4", TagType = "Feature", TitleKey = "HelpWhatsNew2Title", DescriptionKey = "HelpWhatsNew2Desc" },
                            new HelpWhatsNewBlock { VersionBadge = "v2.3", TagType = "Security", TitleKey = "HelpWhatsNew3Title", DescriptionKey = "HelpWhatsNew3Desc" },
                            new HelpWhatsNewBlock { VersionBadge = "v2.2", TagType = "UI", TitleKey = "HelpWhatsNew4Title", DescriptionKey = "HelpWhatsNew4Desc" }
                        }
                    }
                }
            },

            // ════════════════════════════════════════════════════════
            // 12. مكتبة النظائر المرجعية وثوابت الإشعاع
            // ════════════════════════════════════════════════════════
            new HelpTopic
            {
                Id = "IsotopeLibrary",
                TitleKey = "HelpTopicIsotopeLibraryTitle",
                SubtitleKey = "HelpTopicIsotopeLibrarySubtitle",
                IconKind = "BookOpenPageVariantOutline",
                Roles = new() { HelpRoles.All, HelpRoles.SafetyOfficer },
                Keywords = "مكتبة نظائر مرجع ثوابت غاما تدريع رصاص icrp ornl gamma constants shielding",
                Sections = new()
                {
                    new HelpSection
                    {
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpIsotopeLibraryLead", IsLead = true }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpIsotopeLibrarySectionOrnl",
                        IconKind = "ShieldCheckOutline",
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpIsotopeLibrarySectionOrnlDesc" }
                        }
                    },
                    new HelpSection
                    {
                        TitleKey = "HelpIsotopeLibrarySectionIcrp",
                        IconKind = "BookInformationVariant",
                        Blocks = new()
                        {
                            new HelpParagraphBlock { TextKey = "HelpIsotopeLibrarySectionIcrpDesc" }
                        }
                    }
                }
            }
        };
    }
}
