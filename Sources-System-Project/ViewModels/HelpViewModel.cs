using CommunityToolkit.Mvvm.ComponentModel;
using Sources.Models;
using System.Collections.ObjectModel;

namespace Sources.ViewModels;

public partial class HelpViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<HelpTopic> _topics = new();

    [ObservableProperty]
    private HelpTopic? _selectedTopic;

    public HelpViewModel()
    {
        InitializeTopics();
        if (_topics.Count > 0)
        {
            SelectedTopic = _topics[0];
        }
    }

    private void InitializeTopics()
    {
        Topics = new ObservableCollection<HelpTopic>
        {
            new HelpTopic { TitleKey = "HelpTopicIntroTitle", ContentKey = "HelpTopicIntroContent", IconKind = "InformationOutline" },
            new HelpTopic { TitleKey = "HelpTopicDashboardTitle", ContentKey = "HelpTopicDashboardContent", IconKind = "ViewDashboard" },
            new HelpTopic { TitleKey = "HelpTopicRadioisotopesTitle", ContentKey = "HelpTopicRadioisotopesContent", IconKind = "Atom" },
            new HelpTopic { TitleKey = "HelpTopicSourcesTitle", ContentKey = "HelpTopicSourcesContent", IconKind = "Radioactive" },
            new HelpTopic { TitleKey = "HelpTopicBorrowingTitle", ContentKey = "HelpTopicBorrowingContent", IconKind = "BookArrowRight" },
            new HelpTopic { TitleKey = "HelpTopicReportsTitle", ContentKey = "HelpTopicReportsContent", IconKind = "FileChart" },
            new HelpTopic { TitleKey = "HelpTopicSettingsTitle", ContentKey = "HelpTopicSettingsContent", IconKind = "Cog" }
        };
    }
}
