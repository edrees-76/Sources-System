using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
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
/// اختبار انحدار تكاملي (الجولة 145) يتحقق من أن نموذج تحرير الموقع (LocationsView)
/// أصبح نافذة WPF حقيقية منفصلة (LocationFormWindow) تُفتح عبر ShowDialog() من كود
/// LocationsView.xaml.cs — بنفس نمط BorrowFormWindow (الجولة 143) — بدلاً من التراكب
/// المنبثق داخل العرض (In-View Modal Overlay) المُستخدَم في الجولة 141. يحل هذا الملف
/// محل LocationsViewOverlayTests.cs الذي كان يتحقق من نمط التراكب القديم (Panel.ZIndex=1000)
/// الذي لم يعد موجوداً بعد هذه الجولة.
/// </summary>
public class LocationsFormWindowTests
{
    private static void RunInSta(System.Action action) => Sources.Tests.Fixtures.WpfStaFixture.RunInSta(action);

    private static LocationsViewModel CreateViewModel()
    {
        var mockService = new Mock<ILocationService>();
        mockService.Setup(s => s.GetAll()).Returns(new List<Location>());
        return new LocationsViewModel(mockService.Object);
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

    [Fact]
    public void LocationsView_HostedInRealWindow_DataGridAlwaysVisible_RegardlessOfIsEditing()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new LocationsView { DataContext = vm };

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
                    window.UpdateLayout();

                    var dataGrid = FindFirstVisualChild<System.Windows.Controls.DataGrid>(view);
                    Assert.NotNull(dataGrid);
                    Assert.Equal(Visibility.Visible, dataGrid!.Visibility);
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

    [Fact]
    public void LocationsView_AddNewCommand_OpensLocationFormWindow_AndDataGridStaysVisibleUnderneath()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new LocationsView { DataContext = vm };

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
                    window.UpdateLayout();

                    var dataGrid = FindFirstVisualChild<System.Windows.Controls.DataGrid>(view);
                    Assert.NotNull(dataGrid);

                    // ShowDialog() الذي يستدعيه LocationsView.xaml.cs عند IsEditing=true يحجب
                    // مسار التنفيذ الحالي بمضخة رسائل متداخلة (nested message pump) خاصة به.
                    // لذا يُجدوَل التحقق والإغلاق عبر Dispatcher.BeginInvoke قبل استدعاء الأمر،
                    // فتُنفَّذ هذه الخطوة أثناء تشغيل حلقة ShowDialog() المتداخلة نفسها، لا بعدها.
                    // R196-B/R10: تُلتقَط القيم فقط داخل رد نداء BeginInvoke (بينما حلقة ShowDialog
                    // المتداخلة ما زالت نشطة)، وتُؤجَّل كل التأكيدات إلى ما بعد عودة Execute — حتى لا
                    // يُخفي فشل تأكيد داخل الحلقة المتداخلة استثناءً يصعب تتبعه.
                    LocationFormWindow? capturedFormWindow = null;
                    bool wasEditingDuringCallback = false;
                    object? capturedDataContext = null;
                    Visibility dataGridVisibilityDuringCallback = Visibility.Collapsed;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        capturedFormWindow = Application.Current.Windows
                            .OfType<LocationFormWindow>()
                            .FirstOrDefault();
                        wasEditingDuringCallback = vm.IsEditing;
                        capturedDataContext = capturedFormWindow?.DataContext;
                        dataGridVisibilityDuringCallback = dataGrid!.Visibility;

                        vm.CancelEditCommand.Execute(null);
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);

                    // بعد عودة AddNewCommand.Execute، تكون LocationFormWindow قد أُغلقت فعلاً
                    // (CancelEditCommand أعاد IsEditing إلى false فأغلق الكود-خلف النافذة).
                    Assert.NotNull(capturedFormWindow);
                    Assert.True(wasEditingDuringCallback);
                    Assert.Equal(vm, capturedDataContext);
                    Assert.Equal(Visibility.Visible, dataGridVisibilityDuringCallback);
                    Assert.False(vm.IsEditing);
                    Assert.DoesNotContain(capturedFormWindow, Application.Current.Windows.OfType<LocationFormWindow>());
                    Assert.Equal(Visibility.Visible, dataGrid!.Visibility);
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
    public void LocationFormWindow_ClosedViaNativeCloseButton_ResetsIsEditing_AndAllowsReopening()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new LocationsView { DataContext = vm };

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
                    window.UpdateLayout();

                    // الخطوة 1: فتح LocationFormWindow عبر AddNewCommand، ثم محاكاة الإغلاق عبر
                    // زر ✕ الأصلي (Close() مباشرة على النافذة نفسها — وليس عبر CancelEditCommand)،
                    // بنفس أسلوب الجدولة عبر Dispatcher.BeginInvoke.
                    LocationFormWindow? formWindowDuringCallback = null;
                    bool wasEditingDuringFirstCallback = false;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        formWindowDuringCallback = Application.Current.Windows
                            .OfType<LocationFormWindow>()
                            .FirstOrDefault();
                        wasEditingDuringFirstCallback = vm.IsEditing;

                        // محاكاة إغلاق عبر ✕ / Alt+F4: استدعاء Close() مباشرة على النافذة،
                        // وليس عبر CancelEditCommand.
                        formWindowDuringCallback?.Close();
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);

                    // R196-B/R10: التأكيدات بعد عودة Execute مباشرة، لا داخل رد النداء.
                    Assert.NotNull(formWindowDuringCallback);
                    Assert.True(wasEditingDuringFirstCallback);

                    // بعد الإغلاق عبر ✕، يجب أن تُصفَّر IsEditing تلقائياً عبر معالج Closing
                    // في LocationFormWindow (الذي يستدعي CancelEditCommand داخلياً)، دون أن يحتاج
                    // المستخدم لاستدعاء زر الإلغاء يدوياً.
                    Assert.False(vm.IsEditing);
                    Assert.Empty(Application.Current.Windows.OfType<LocationFormWindow>());

                    // الخطوة 2: التأكد من أن نافذة جديدة فعلاً تُفتح عند استدعاء AddNewCommand
                    // مرة أخرى (وليس لا شيء بسبب مرجع نافذة سابق عالق يمنع الفتح).
                    LocationFormWindow? secondFormWindow = null;
                    bool wasEditingDuringSecondCallback = false;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        secondFormWindow = Application.Current.Windows
                            .OfType<LocationFormWindow>()
                            .FirstOrDefault();
                        wasEditingDuringSecondCallback = vm.IsEditing;

                        vm.CancelEditCommand.Execute(null);
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);

                    Assert.NotNull(secondFormWindow);
                    Assert.True(wasEditingDuringSecondCallback);
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
    /// يحاكي ضغطة مفتاح Enter الحقيقية على العنصر المركَّز: يطلق أولاً حدث النفق
    /// Keyboard.PreviewKeyDownEvent (الذي يصل إلى معالج PreviewKeyDown على مستوى النافذة —
    /// إصلاح الجولة 149/196 — قبل وصوله إلى العنصر نفسه)، ثم يطلق حدث الفقاعة
    /// Keyboard.KeyDownEvent (ما لم يكن حدث المعاينة قد عولج/Handled بالفعل)، تماماً كما يحدث
    /// في التوجيه الحقيقي لأحداث لوحة المفاتيح في WPF (Tunnel ثم Bubble). النسخة السابقة
    /// (الجولة 149) كانت تُطلق KeyDownEvent فقط، فلم تكن تختبر معالج PreviewKeyDown إطلاقاً.
    /// </summary>
    private static void SimulateEnterKeyPress(UIElement element)
    {
        Assert.Same(element, Keyboard.FocusedElement);

        var source = PresentationSource.FromVisual(element);

        var previewArgs = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        element.RaiseEvent(previewArgs);

        if (previewArgs.Handled) return;

        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };
        element.RaiseEvent(args);
    }

    /// <summary>
    /// اختبار انحدار (الجولة 196): يحرِّر موقعاً موجوداً فعلاً عبر EditCommand الحقيقي، يكتب قيمة
    /// جديدة في أحد الحقول (UpdateSourceTrigger=PropertyChanged في XAML + معالج PreviewKeyDown في
    /// الكود-خلف)، ثم يضغط Enter والتركيز لا يزال داخل الحقل. يتحقق من القيمة الفعلية التي وصلت
    /// لطبقة الخدمة ILocationService.Update عبر Moq Callback (لا من خاصية الـViewModel وحدها)،
    /// وأن Update استُدعيت مرة واحدة بالضبط، وأن IsEditing أصبحت false (نجاح حقيقي أغلق النافذة).
    /// </summary>
    private static void AssertEnterCommitsNewValue_ForLocationField(
        string bindingPath,
        string newTextValue,
        System.Func<Location, string?> readPersistedValue)
    {
        RunInSta(() =>
        {
            var existing = new Location
            {
                Id = System.Guid.NewGuid(),
                LocationName = "المخزن الرئيسي",
                LocationType = "Storage",
                Building = "المبنى أ",
                Room = "101",
                ResponsiblePerson = "أحمد"
            };

            var mockService = new Mock<ILocationService>();
            mockService.Setup(s => s.GetAll()).Returns(new List<Location> { existing });
            Location? persistedItem = null;
            int updateCallCount = 0;
            mockService.Setup(s => s.Update(It.IsAny<Location>()))
                .Callback<Location>(item => { persistedItem = item; updateCallCount++; })
                .Returns((true, "تم تحديث الموقع"));

            var vm = new LocationsViewModel(mockService.Object);
            try
            {
                vm.LoadData();
                vm.Selected = vm.Locations.First(l => l.Id == existing.Id);
                vm.EditCommand.Execute(null);

                var formWindow = new LocationFormWindow
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
                    Assert.Same(textBox, Keyboard.FocusedElement);

                    textBox.Text = newTextValue;
                    SimulateEnterKeyPress(textBox);

                    Assert.Equal(1, updateCallCount); // حفظ واحد بالضبط، لا حفظ مزدوج
                    Assert.NotNull(persistedItem);
                    Assert.Equal(newTextValue, readPersistedValue(persistedItem!));
                    Assert.False(vm.IsEditing); // النافذة أُغلقت فعلاً — نجاح حقيقي لا رسالة كاذبة
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
    public void LocationFormWindow_EnterAfterTyping_PersistsNewValue_ForEditName()
        => AssertEnterCommitsNewValue_ForLocationField("EditName", "مخزن جديد", l => l.LocationName);

    [Fact]
    public void LocationFormWindow_EnterAfterTyping_PersistsNewValue_ForEditBuilding()
        => AssertEnterCommitsNewValue_ForLocationField("EditBuilding", "المبنى ب", l => l.Building);

    [Fact]
    public void LocationFormWindow_EnterAfterTyping_PersistsNewValue_ForEditRoom()
        => AssertEnterCommitsNewValue_ForLocationField("EditRoom", "202", l => l.Room);

    [Fact]
    public void LocationFormWindow_EnterAfterTyping_PersistsNewValue_ForEditPerson()
        => AssertEnterCommitsNewValue_ForLocationField("EditPerson", "سارة", l => l.ResponsiblePerson);

    /// <summary>
    /// إثبات عزل المعالج (الجولة 196): يعيد ربط EditName صراحةً بمشغّل LostFocus (الافتراضي)
    /// بدلاً من PropertyChanged، لإثبات أن معالج PreviewKeyDown في الكود-خلف وحده — بمعزل تام عن
    /// UpdateSourceTrigger في XAML — كافٍ لتفريغ القيمة الجديدة إلى الربط قبل الحفظ عند Enter.
    /// </summary>
    [Fact]
    public void LocationFormWindow_EnterAfterTyping_PersistsNewValue_EvenWhenTextBoxUsesLostFocusTrigger()
    {
        RunInSta(() =>
        {
            var existing = new Location
            {
                Id = System.Guid.NewGuid(),
                LocationName = "المخزن الرئيسي",
                LocationType = "Storage",
                Building = "المبنى أ",
                Room = "101",
                ResponsiblePerson = "أحمد"
            };

            var mockService = new Mock<ILocationService>();
            mockService.Setup(s => s.GetAll()).Returns(new List<Location> { existing });
            Location? persistedItem = null;
            int updateCallCount = 0;
            mockService.Setup(s => s.Update(It.IsAny<Location>()))
                .Callback<Location>(item => { persistedItem = item; updateCallCount++; })
                .Returns((true, "تم تحديث الموقع"));

            var vm = new LocationsViewModel(mockService.Object);
            try
            {
                vm.LoadData();
                vm.Selected = vm.Locations.First(l => l.Id == existing.Id);
                vm.EditCommand.Execute(null);

                var formWindow = new LocationFormWindow
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

                    var textBox = FindTextBoxByBindingPath(formWindow, "EditName");
                    Assert.NotNull(textBox);

                    BindingOperations.SetBinding(textBox, TextBox.TextProperty,
                        new Binding("EditName") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, Mode = BindingMode.TwoWay });

                    textBox!.Focus();
                    Assert.Same(textBox, Keyboard.FocusedElement);

                    textBox.Text = "اسم بمعزل عن الربط الفوري";
                    SimulateEnterKeyPress(textBox);

                    Assert.Equal(1, updateCallCount);
                    Assert.NotNull(persistedItem);
                    Assert.Equal("اسم بمعزل عن الربط الفوري", persistedItem!.LocationName);
                    Assert.False(vm.IsEditing);
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
}
