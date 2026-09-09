using System.ComponentModel;
using System.Windows;
using Sources.ViewModels;

namespace Sources.Views;

public partial class NeutronSourceTypesWindow : Window
{
    /// <summary>
    /// يشير إلى أن هذه النافذة بصدد الإغلاق فعلياً (سواء عبر زر الإغلاق الأصلي ✕، Alt+F4،
    /// أو إغلاق برمجي مُستدعى من SourcesView). يُستخدَم لمنع SourcesView من استدعاء Close()
    /// مرة أخرى بشكل متكرر (reentrant) أثناء معالجة حدث Closing نفسه.
    /// </summary>
    internal bool IsClosingInProgress { get; private set; }

    public NeutronSourceTypesWindow()
    {
        InitializeComponent();
        Closing += NeutronSourceTypesWindow_Closing;
    }

    private void NeutronSourceTypesWindow_Closing(object? sender, CancelEventArgs e)
    {
        IsClosingInProgress = true;

        if (DataContext is NeutronSourceTypesViewModel vm)
        {
            if (vm.CloseCommand.CanExecute(null))
            {
                vm.CloseCommand.Execute(null);
            }
        }
    }
}
