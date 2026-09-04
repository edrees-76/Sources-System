using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Sources.Tests.Fixtures;

public static class WpfStaFixture
{
    private static readonly Lazy<Dispatcher> StaDispatcher = new(() =>
    {
        Dispatcher? dispatcher = null;
        var readyEvent = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            if (Application.Current == null)
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var appResources = app.Resources;
                appResources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Colors.xaml", UriKind.Absolute)
                });
                appResources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Strings.ar.xaml", UriKind.Absolute)
                });
                appResources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/LightTheme.xaml", UriKind.Absolute)
                });
                appResources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml", UriKind.Absolute)
                });
                appResources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Styles.xaml", UriKind.Absolute)
                });
                appResources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Converters.xaml", UriKind.Absolute)
                });
            }
            dispatcher = Dispatcher.CurrentDispatcher;
            readyEvent.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        readyEvent.Wait();
        return dispatcher!;
    });

    public static void RunInSta(Action action)
    {
        Exception? exception = null;
        StaDispatcher.Value.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        if (exception != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
