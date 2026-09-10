using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Sources.Data;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Sources.ViewModels;
using Sources.Views;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبار انحدار تكاملي (الجولة 148) يتحقق من أن معالج إضافة/تعديل المصدر في SourcesView
/// أصبح نافذة WPF حقيقية منفصلة (SourceFormWindow) تُفتح عبر ShowDialog() من كود
/// SourcesView.xaml.cs — بنفس نمط LocationFormWindow (145)/RadioisotopeFormWindow (146)/
/// UserFormWindow (147) — بدلاً من التراكب المنبثق داخل العرض (In-View Modal Overlay).
///
/// ملاحظة بنيوية مهمة: هذه الشاشة تستخدم معالجاً واحداً متعدد الخطوات ذا مفتاح تبديل داخلي
/// (IsNeutronForm) يخدم المصدر العادي والمصدر النيتروني معاً — وليست معالجَين مستقلين.
/// لذلك تغطي الاختبارات هنا كلا نوعي المصدر داخل نفس النافذة.
/// </summary>
public class SourceFormWindowTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fixture;

    public SourceFormWindowTests()
    {
        _fixture = new SqliteInMemoryFixture();
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(_fixture.ContextFactory);
        typeof(App).GetProperty("ServiceProvider", BindingFlags.Public | BindingFlags.Static)?
            .SetValue(null, services.BuildServiceProvider());
    }

    public void Dispose() => _fixture.Dispose();

    private static void RunInSta(Action action) => WpfStaFixture.RunInSta(action);

    private static SourcesViewModel CreateViewModel()
    {
        var mockSourceService = new Mock<ISourceService>();
        mockSourceService.Setup(s => s.GetAllSources()).Returns(new List<Source>());
        mockSourceService.Setup(s => s.GetDeletedSources()).Returns(new List<Source>());

        var mockIsotopeService = new Mock<IRadioisotopeService>();
        mockIsotopeService.Setup(s => s.GetAll()).Returns(new List<Radioisotope>());

        var mockLocationService = new Mock<ILocationService>();
        mockLocationService.Setup(s => s.GetAll()).Returns(new List<Location>());

        var mockReportingService = new Mock<IReportingService>();

        var mockNeutronService = new Mock<INeutronSourceService>();
        mockNeutronService.Setup(s => s.GetAll()).Returns(new List<NeutronSource>());

        var mockNeutronTypeService = new Mock<INeutronSourceTypeService>();
        mockNeutronTypeService.Setup(s => s.GetAll()).Returns(new List<NeutronSourceType>());

        return new SourcesViewModel(
            mockSourceService.Object,
            mockIsotopeService.Object,
            mockLocationService.Object,
            mockReportingService.Object,
            null,
            mockNeutronService.Object,
            mockNeutronTypeService.Object,
            null);
    }

    private static Window CreateHostWindow(SourcesView view) => new()
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

    [Fact]
    public void SourcesView_HostedInRealWindow_HasNoInlineEditingOverlay()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new SourcesView { DataContext = vm };
                var window = CreateHostWindow(view);
                window.Show();
                try
                {
                    window.UpdateLayout();

                    // لم يعد المعالج جزءاً من شجرة SourcesView المرئية إطلاقاً.
                    Assert.False(vm.IsEditing);
                    Assert.Empty(Application.Current.Windows.OfType<SourceFormWindow>());
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
    public void SourcesView_AddNewCommand_OpensSourceFormWindow_ForStandardSource()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new SourcesView { DataContext = vm };
                var window = CreateHostWindow(view);
                window.Show();
                try
                {
                    window.UpdateLayout();

                    // ShowDialog() الذي يستدعيه SourcesView.xaml.cs عند IsEditing=true يحجب
                    // مسار التنفيذ الحالي بمضخة رسائل متداخلة (nested message pump) خاصة به.
                    // لذا يُجدوَل التحقق والإغلاق عبر Dispatcher.BeginInvoke قبل استدعاء الأمر.
                    SourceFormWindow? captured = null;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                    {
                        captured = Application.Current.Windows.OfType<SourceFormWindow>().FirstOrDefault();

                        Assert.NotNull(captured);
                        Assert.True(vm.IsEditing);
                        Assert.False(vm.IsNeutronForm); // مصدر عادي
                        Assert.True(vm.IsNew);
                        Assert.Equal(1, vm.CurrentStep);
                        Assert.Equal(vm, captured!.DataContext);

                        vm.CancelEditCommand.Execute(null);
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);

                    Assert.False(vm.IsEditing);
                    Assert.DoesNotContain(captured, Application.Current.Windows.OfType<SourceFormWindow>());
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
    public void SourcesView_AddNewNeutronCommand_OpensSameSourceFormWindow_ForNeutronSource()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new SourcesView { DataContext = vm };
                var window = CreateHostWindow(view);
                window.Show();
                try
                {
                    window.UpdateLayout();

                    SourceFormWindow? captured = null;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                    {
                        captured = Application.Current.Windows.OfType<SourceFormWindow>().FirstOrDefault();

                        // نفس النافذة تخدم المصدر النيتروني، عبر مفتاح التبديل الداخلي.
                        Assert.NotNull(captured);
                        Assert.True(vm.IsEditing);
                        Assert.True(vm.IsNeutronForm); // مصدر نيتروني
                        Assert.Equal(vm, captured!.DataContext);

                        vm.CancelEditCommand.Execute(null);
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewNeutronCommand.Execute(null);

                    Assert.False(vm.IsEditing);
                    Assert.DoesNotContain(captured, Application.Current.Windows.OfType<SourceFormWindow>());
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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SourceFormWindow_ClosedViaNativeCloseButton_AtAnyStep_ResetsIsEditing(int stepToCloseAt)
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new SourcesView { DataContext = vm };
                var window = CreateHostWindow(view);
                window.Show();
                try
                {
                    window.UpdateLayout();

                    Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                    {
                        var formWindow = Application.Current.Windows.OfType<SourceFormWindow>().FirstOrDefault();
                        Assert.NotNull(formWindow);
                        Assert.True(vm.IsEditing);

                        // التقدّم إلى الخطوة المطلوبة عبر أمر التنقل الحقيقي للمعالج.
                        while (vm.CurrentStep < stepToCloseAt)
                        {
                            vm.NextStepCommand.Execute(null);
                        }
                        Assert.Equal(stepToCloseAt, vm.CurrentStep);

                        // محاكاة إغلاق عبر ✕ / Alt+F4: استدعاء Close() مباشرة على النافذة،
                        // وليس عبر CancelEditCommand.
                        formWindow!.Close();
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);

                    // بغض النظر عن الخطوة التي أُغلقت عندها النافذة، يجب ألا تعلق الحالة.
                    Assert.False(vm.IsEditing);
                    Assert.Empty(Application.Current.Windows.OfType<SourceFormWindow>());
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

    /// <summary>
    /// مُرحَّل من ViewInstantiationTests.SourcesView_WhenActivelyBorrowed_DisablesStatusAndLocationComboBoxes
    /// (الجولة 148): السيناريو نفسه لم يتغيّر، لكن قائمتَي "الحالة" (الخطوة 1) و"الموقع" (الخطوة 3)
    /// لم تعودا داخل شجرة SourcesView المرئية بعد نقل المعالج إلى SourceFormWindow، لذا يجري
    /// التحقق الآن على النافذة الحقيقية بدل العرض.
    /// </summary>
    [Fact]
    public void SourceFormWindow_WhenActivelyBorrowed_DisablesStatusAndLocationComboBoxes()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var sourceId = Guid.NewGuid();
                var source = new Source
                {
                    Id = sourceId,
                    SourceCode = "SRC-0021",
                    Status = "InUse",
                    InitialActivityValue = 100,
                    CalibrationDate = DateTime.Today
                };

                var mockSourceService = Mock.Get(GetSourceService(vm));
                mockSourceService.Setup(s => s.HasActiveBorrow(sourceId)).Returns(true);
                mockSourceService.Setup(s => s.GetSourceById(sourceId)).Returns(source);

                vm.EditSourceCommand.Execute(source);
                Assert.True(vm.IsActivelyBorrowed);
                Assert.True(vm.IsEditing);

                var formWindow = new SourceFormWindow
                {
                    DataContext = vm,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Left = -5000,
                    Top = -5000
                };

                formWindow.Show();
                try
                {
                    formWindow.UpdateLayout();
                    vm.CurrentStep = 3; // لإخراج قائمة "الموقع" إلى شجرة العرض كذلك
                    formWindow.UpdateLayout();

                    var comboBoxes = FindVisualChildren<System.Windows.Controls.ComboBox>(formWindow).ToList();
                    Assert.NotEmpty(comboBoxes);

                    var disabledBoxes = comboBoxes.Where(c => !c.IsEnabled).ToList();
                    Assert.True(disabledBoxes.Count >= 1, "At least Status or Location ComboBox should be disabled");

                    foreach (var box in disabledBoxes)
                    {
                        Assert.True(System.Windows.Controls.ToolTipService.GetShowOnDisabled(box));
                    }
                }
                finally
                {
                    formWindow.Close();
                }
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(vm);
            }
        });
    }

    private static ISourceService GetSourceService(SourcesViewModel vm) =>
        (ISourceService)typeof(SourcesViewModel)
            .GetField("_sourceService", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(vm)!;

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }

    /// <summary>
    /// يبحث في الشجرة المرئية عن أول TextBox مربوط (Binding) بمسار الخاصية المحدَّدة على
    /// TextBox.TextProperty — يُستخدَم لإيجاد الحقل الفعلي دون الاعتماد على x:Name.
    /// </summary>
    private static TextBox? FindTextBoxByBindingPath(DependencyObject root, string propertyPath)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is TextBox textBox)
            {
                var expression = BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty);
                if (expression?.ParentBinding?.Path?.Path == propertyPath) return textBox;
            }
            var nested = FindTextBoxByBindingPath(child, propertyPath);
            if (nested != null) return nested;
        }
        return null;
    }

    /// <summary>
    /// يحاكي ضغطة مفتاح Enter (KeyDown) على العنصر المركَّز دون استدعاء LostFocus أو Focus()
    /// على عنصر آخر — بنفس أسلوب المستخدم الفعلي الذي يضغط Enter مباشرة بعد الكتابة.
    /// </summary>
    private static void SimulateEnterKeyPress(UIElement element)
    {
        var source = PresentationSource.FromVisual(element);
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };
        element.RaiseEvent(args);
    }

    /// <summary>
    /// اختبار انحدار مشترك (الجولة 149) لخلل فقدان القيمة عند الضغط على Enter مباشرة بعد الكتابة
    /// دون فقدان التركيز يدويًا أولاً، لكل حقل من الحقول السبعة في SourceFormWindow. الحقول
    /// الستة الخاصة بالمصدر النيتروني تظهر في الخطوة 2 (Visibility مربوطة بـ IsNeutronForm ضمن
    /// StackPanel الخطوة 2 — وليس الخطوة 3 كما ورد افتراضًا أوليًا في العقد؛ زر الحفظ IsDefault
    /// نفسه غير مرئي إلا في الخطوة 3، لكن الحقول القابلة للتركيز البؤري (Focusable) والتي يمكن
    /// محاكاة الكتابة والضغط على Enter عليها فعليًا موجودة في الخطوة 2 فقط. راجع قسم "الانحرافات"
    /// في تقرير الالتزام لتفصيل هذا التصحيح مقابل نص العقد الحرفي).
    /// </summary>
    private static void AssertEnterCommitsNewValue_ForSourceField(
        bool isNeutronForm,
        string bindingPath,
        string newTextValue,
        Func<SourcesViewModel, string> readBoundText)
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                if (isNeutronForm) vm.AddNewNeutronCommand.Execute(null);
                else vm.AddNewCommand.Execute(null);

                vm.NextStepCommand.Execute(null); // الانتقال إلى الخطوة 2 حيث تظهر الحقول المستهدفة

                var formWindow = new SourceFormWindow
                {
                    DataContext = vm,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Left = -5000,
                    Top = -5000
                };

                formWindow.Show();
                try
                {
                    formWindow.UpdateLayout();

                    var textBox = FindTextBoxByBindingPath(formWindow, bindingPath);
                    Assert.NotNull(textBox);

                    textBox!.Focus();
                    textBox.Text = newTextValue;
                    SimulateEnterKeyPress(textBox);

                    Assert.Equal(newTextValue, readBoundText(vm));
                }
                finally
                {
                    formWindow.Close();
                }
            }
            finally
            {
                WeakReferenceMessenger.Default.UnregisterAll(vm);
            }
        });
    }

    [Fact]
    public void SourceFormWindow_EnterAfterTyping_CommitsNewValue_ForEditEmissionRateText()
        => AssertEnterCommitsNewValue_ForSourceField(true, "EditEmissionRateText", "3.7E7", vm => vm.EditEmissionRateText);

    [Fact]
    public void SourceFormWindow_EnterAfterTyping_CommitsNewValue_ForEditRelativeUncertaintyText()
        => AssertEnterCommitsNewValue_ForSourceField(true, "EditRelativeUncertaintyText", "2.5", vm => vm.EditRelativeUncertaintyText);

    [Fact]
    public void SourceFormWindow_EnterAfterTyping_CommitsNewValue_ForEditAnisotropyFactorText()
        => AssertEnterCommitsNewValue_ForSourceField(true, "EditAnisotropyFactorText", "1.05", vm => vm.EditAnisotropyFactorText);

    [Fact]
    public void SourceFormWindow_EnterAfterTyping_CommitsNewValue_ForEditCapsuleLengthText()
        => AssertEnterCommitsNewValue_ForSourceField(true, "EditCapsuleLengthText", "25.4", vm => vm.EditCapsuleLengthText);

    [Fact]
    public void SourceFormWindow_EnterAfterTyping_CommitsNewValue_ForEditCapsuleDiameterText()
        => AssertEnterCommitsNewValue_ForSourceField(true, "EditCapsuleDiameterText", "6.35", vm => vm.EditCapsuleDiameterText);

    [Fact]
    public void SourceFormWindow_EnterAfterTyping_CommitsNewValue_ForEditActivityText()
        => AssertEnterCommitsNewValue_ForSourceField(true, "EditActivityText", "3.7", vm => vm.EditActivityText);

    [Fact]
    public void SourceFormWindow_EnterAfterTyping_CommitsNewValue_ForEditInitialActivityText()
        => AssertEnterCommitsNewValue_ForSourceField(false, "EditInitialActivityText", "3.7", vm => vm.EditInitialActivityText);

    [Fact]
    public void SourceFormWindow_AfterNativeClose_CanBeReopened_StartingFromFirstStep()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new SourcesView { DataContext = vm };
                var window = CreateHostWindow(view);
                window.Show();
                try
                {
                    window.UpdateLayout();

                    // الخطوة 1: فتح النافذة، التقدّم للخطوة 3، ثم الإغلاق عبر ✕.
                    Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                    {
                        var formWindow = Application.Current.Windows.OfType<SourceFormWindow>().FirstOrDefault();
                        Assert.NotNull(formWindow);

                        vm.NextStepCommand.Execute(null);
                        vm.NextStepCommand.Execute(null);
                        Assert.Equal(3, vm.CurrentStep);

                        formWindow!.Close();
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);
                    Assert.False(vm.IsEditing);

                    // الخطوة 2: التأكد من أن نافذة جديدة فعلاً تُفتح مرة أخرى (وليس لا شيء
                    // بسبب مرجع نافذة سابق عالق يمنع الفتح)، وأنها تبدأ من الخطوة الأولى.
                    SourceFormWindow? second = null;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                    {
                        second = Application.Current.Windows.OfType<SourceFormWindow>().FirstOrDefault();

                        Assert.NotNull(second);
                        Assert.True(vm.IsEditing);
                        Assert.Equal(1, vm.CurrentStep);

                        vm.CancelEditCommand.Execute(null);
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);

                    Assert.NotNull(second);
                    Assert.False(vm.IsEditing);
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
}
