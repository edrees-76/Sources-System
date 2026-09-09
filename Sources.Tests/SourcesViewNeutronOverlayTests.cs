using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Sources.Views;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبار انحدار تكاملي (الجولة 144) يتحقق من أن شاشة إدارة الأنواع النيترونية المرجعية
/// أصبحت نافذة WPF حقيقية منفصلة (NeutronSourceTypesWindow) تُفتح عبر ShowDialog() من كود
/// SourcesView.xaml.cs — بنفس نمط round 143 (BorrowFormWindow/BorrowView) — بدلاً من التراكب
/// المنبثق داخل العرض (In-View Modal Overlay) المُستخدَم سابقاً (الجولة 139). يستضيف الاختبار
/// SourcesView فعلياً داخل Window حقيقية (وليس فقط Measure/Arrange) للتأكد من أن جدول المصادر
/// يبقى ظاهراً دائماً، وأن فتح/إغلاق NeutronSourceTypesWindow يعكس فعلياً حالة
/// IsManagingNeutronTypes على SourcesViewModel الأب.
/// </summary>
public class SourcesViewNeutronOverlayTests
{
    private static void RunInSta(System.Action action) => Sources.Tests.Fixtures.WpfStaFixture.RunInSta(action);

    /// <summary>
    /// يُفرِّغ طابور Dispatcher حتى أولوية Background مرة واحدة فقط.
    /// </summary>
    private static void DrainDispatcherOnce()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new System.Action(() =>
        {
            frame.Continue = false;
        }));
        Dispatcher.PushFrame(frame);
    }

    /// <summary>
    /// ينتظر اكتمال التحميل غير المتزامن لـ SourcesViewModel عبر استطلاع دوري (polling) بدلاً من
    /// تفريغ واحد للطابور. ضروري لأن مُنشئ SourcesViewModel يستدعي LoadDataAsync() بأسلوب
    /// "fire-and-forget" (`_ = LoadDataAsync();`) الذي ينتظر Task.Run(...) على خيط من مجمّع الخيوط
    /// (ThreadPool)؛ توقيت انتهاء ذلك الخيط غير محدد بالنسبة لتفريغ واحد لطابور Dispatcher مباشرة
    /// بعد Show()، مما يجعل تفريغاً واحداً عرضة لسباق زمني (قد يُنفَّذ قبل أن يُجدوِل الاستئناف
    /// نفسه). الاستطلاع الدوري مع مهلة قصوى يزيل هذا السباق دون الاعتماد على توقيت غير مضمون.
    /// (هذا سباق زمني خاص بتحميل SourcesViewModel غير المتزامن داخل الاختبار، وليس خللاً في
    /// ربط Visibility نفسه في SourcesView.xaml).
    /// </summary>
    private static void WaitForSourcesLoaded(SourcesViewModel vm)
    {
        const int maxIterations = 200; // ~2 ثانية كحد أقصى بفاصل 10ms
        for (int i = 0; i < maxIterations && vm.Sources.Count == 0; i++)
        {
            DrainDispatcherOnce();
            System.Threading.Thread.Sleep(10);
        }
    }

    private static SourcesViewModel CreateViewModel()
    {
        WeakReferenceMessenger.Default.Reset();

        var mockSourceService = new Mock<ISourceService>();
        var mockIsotopeService = new Mock<IRadioisotopeService>();
        var mockLocationService = new Mock<ILocationService>();
        var mockReportingService = new Mock<IReportingService>();
        var mockNeutronSourceService = new Mock<INeutronSourceService>();
        var mockNeutronSourceTypeService = new Mock<INeutronSourceTypeService>();

        // مصدر واحد كافٍ لجعل قائمة البطاقات الرئيسية (SourceCardsPanel) ظاهرة فعلياً؛
        // القائمة الفارغة تُخفيها ضمن حالة "لا توجد مصادر" بمعزل تماماً عن حالة نافذة
        // إدارة الأنواع النيترونية، مما يجعل الاختبار غير ذي معنى إن تُرك فارغاً.
        mockSourceService.Setup(s => s.GetAllSources()).Returns(new List<Source>
        {
            new Source { SourceCode = "TEST-SRC-01" }
        });
        // ضروري: LoadDataAsync يستدعي GetDeletedSources() فوراً بعد GetAllSources() (السطر 437 في
        // SourcesViewModel) ويستخدم النتيجة مباشرة (deletedList.Count) دون فحص Null. بدون هذا
        // الـ Stub تُعيد Moq قيمة null افتراضياً (MockBehavior.Loose مع نوع مرجعي)، فيُرمى
        // NullReferenceException غير مُلتقَط داخل LoadDataAsync() قبل الوصول لسطر "Sources = ..."،
        // فتبقى Sources فارغة إلى الأبد ويظهر SourceCardsPanel كـ Collapsed خطأً — وهو ما كان يُغرق
        // اختبارات هذا الملف في فشل ظاهره سباق زمني بينما هو في الحقيقة إعداد Mock ناقص.
        mockSourceService.Setup(s => s.GetDeletedSources()).Returns(new List<Source>());
        mockIsotopeService.Setup(s => s.GetAll()).Returns(new List<Radioisotope>());
        mockLocationService.Setup(s => s.GetAll()).Returns(new List<Location>());
        mockNeutronSourceTypeService.Setup(s => s.GetAll()).Returns(new List<NeutronSourceType>());

        return new SourcesViewModel(
            mockSourceService.Object,
            mockIsotopeService.Object,
            mockLocationService.Object,
            mockReportingService.Object,
            null,
            mockNeutronSourceService.Object,
            mockNeutronSourceTypeService.Object);
    }

    [Fact]
    public void SourcesView_HostedInRealWindow_SourcesTableAlwaysVisible_RegardlessOfIsManagingNeutronTypes()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new SourcesView { DataContext = vm };

                var window = new Window
                {
                    Content = view,
                    Width = 1280,
                    Height = 800,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Left = -5000,
                    Top = -5000
                };

                window.Show();
                try
                {
                    WaitForSourcesLoaded(vm);
                    window.UpdateLayout();

                    var sourceCardsPanel = view.FindName("SourceCardsPanel") as System.Windows.Controls.ItemsControl;
                    Assert.NotNull(sourceCardsPanel);
                    Assert.Equal(Visibility.Visible, sourceCardsPanel!.Visibility);
                    Assert.False(vm.IsManagingNeutronTypes);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(vm);
            }
        });
    }

    [Fact]
    public void SourcesView_OpenNeutronSourceTypesManagementCommand_OpensNeutronSourceTypesWindow_AndTableStaysVisibleUnderneath()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new SourcesView { DataContext = vm };

                var window = new Window
                {
                    Content = view,
                    Width = 1280,
                    Height = 800,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Left = -5000,
                    Top = -5000
                };

                window.Show();
                try
                {
                    WaitForSourcesLoaded(vm);
                    window.UpdateLayout();

                    var sourceCardsPanel = view.FindName("SourceCardsPanel") as System.Windows.Controls.ItemsControl;
                    Assert.NotNull(sourceCardsPanel);

                    // ShowDialog() الذي يستدعيه SourcesView.xaml.cs عند IsManagingNeutronTypes=true
                    // يحجب مسار التنفيذ الحالي بمضخة رسائل متداخلة (nested message pump) خاصة به.
                    // لذا يُجدوَل التحقق والإغلاق عبر Dispatcher.BeginInvoke قبل استدعاء الأمر،
                    // فتُنفَّذ هذه الخطوة أثناء تشغيل حلقة ShowDialog() المتداخلة نفسها، لا بعدها.
                    NeutronSourceTypesWindow? capturedWindow = null;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        capturedWindow = Application.Current.Windows
                            .OfType<NeutronSourceTypesWindow>()
                            .FirstOrDefault();

                        Assert.NotNull(capturedWindow);
                        Assert.True(vm.IsManagingNeutronTypes);
                        Assert.Equal(Visibility.Visible, sourceCardsPanel!.Visibility);

                        vm.NeutronTypesManagementViewModel!.CloseCommand.Execute(null);
                    }), DispatcherPriority.ApplicationIdle);

                    vm.OpenNeutronSourceTypesManagementCommand.Execute(null);

                    // بعد عودة Execute، تكون NeutronSourceTypesWindow قد أُغلقت فعلاً
                    // (CloseCommand استدعى OnClose الذي صفّر IsManagingNeutronTypes، فأغلق
                    // الكود-خلف الخاص بـ SourcesView النافذة استجابة لذلك).
                    Assert.False(vm.IsManagingNeutronTypes);
                    Assert.DoesNotContain(capturedWindow, Application.Current.Windows.OfType<NeutronSourceTypesWindow>());
                    Assert.Equal(Visibility.Visible, sourceCardsPanel!.Visibility);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(vm);
            }
        });
    }

    [Fact]
    public void NeutronSourceTypesWindow_ClosedViaNativeCloseButton_ResetsIsManagingNeutronTypes_AndAllowsReopening()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new SourcesView { DataContext = vm };

                var window = new Window
                {
                    Content = view,
                    Width = 1280,
                    Height = 800,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Left = -5000,
                    Top = -5000
                };

                window.Show();
                try
                {
                    WaitForSourcesLoaded(vm);
                    window.UpdateLayout();

                    // الخطوة 1: فتح NeutronSourceTypesWindow عبر الأمر، ثم محاكاة الإغلاق عبر
                    // زر ✕ الأصلي (Close() مباشرة على النافذة نفسها — وليس عبر CloseCommand)،
                    // بنفس أسلوب الجدولة عبر Dispatcher.BeginInvoke المستخدم في الاختبار الآخر.
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        var typesWindow = Application.Current.Windows
                            .OfType<NeutronSourceTypesWindow>()
                            .FirstOrDefault();

                        Assert.NotNull(typesWindow);
                        Assert.True(vm.IsManagingNeutronTypes);

                        // محاكاة إغلاق عبر ✕ / Alt+F4: استدعاء Close() مباشرة على النافذة،
                        // وليس عبر CloseCommand.
                        typesWindow!.Close();
                    }), DispatcherPriority.ApplicationIdle);

                    vm.OpenNeutronSourceTypesManagementCommand.Execute(null);

                    // بعد الإغلاق عبر ✕، يجب أن تُصفَّر IsManagingNeutronTypes تلقائياً عبر
                    // معالج Closing في NeutronSourceTypesWindow (الذي يستدعي CloseCommand داخلياً)،
                    // دون أن يحتاج المستخدم لاستدعاء زر الإغلاق يدوياً.
                    Assert.False(vm.IsManagingNeutronTypes);
                    Assert.Empty(Application.Current.Windows.OfType<NeutronSourceTypesWindow>());

                    // الخطوة 2: التأكد من أن نافذة جديدة فعلاً تُفتح عند استدعاء الأمر مرة أخرى
                    // (وليس لا شيء بسبب مرجع نافذة سابق عالق يمنع الفتح).
                    NeutronSourceTypesWindow? secondWindow = null;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        secondWindow = Application.Current.Windows
                            .OfType<NeutronSourceTypesWindow>()
                            .FirstOrDefault();

                        Assert.NotNull(secondWindow);
                        Assert.True(vm.IsManagingNeutronTypes);

                        vm.NeutronTypesManagementViewModel!.CloseCommand.Execute(null);
                    }), DispatcherPriority.ApplicationIdle);

                    vm.OpenNeutronSourceTypesManagementCommand.Execute(null);

                    Assert.NotNull(secondWindow);
                    Assert.False(vm.IsManagingNeutronTypes);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(vm);
            }
        });
    }

    private static T? FindFirstVisualChild<T>(System.Windows.DependencyObject root) where T : System.Windows.DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T typed) return typed;
            var nested = FindFirstVisualChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }
}
