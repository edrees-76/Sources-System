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
/// اختبار انحدار تكاملي (الجولة 147) يتحقق من أن نموذج تحرير المستخدم (UsersView)
/// أصبح نافذة WPF حقيقية منفصلة (UserFormWindow) تُفتح عبر ShowDialog() من كود
/// UsersView.xaml.cs — بنفس نمط LocationFormWindow (الجولة 145)/RadioisotopeFormWindow
/// (الجولة 146) — بدلاً من التراكب المنبثق داخل العرض (In-View Modal Overlay).
/// </summary>
public class UserFormWindowTests
{
    private static void RunInSta(System.Action action) => Sources.Tests.Fixtures.WpfStaFixture.RunInSta(action);

    private static UsersViewModel CreateViewModel()
    {
        var mockUserService = new Mock<IUserService>();
        mockUserService.Setup(s => s.GetAllUsers()).Returns(new List<User>());
        mockUserService.Setup(s => s.GetAllRoles()).Returns(new List<Role>());
        mockUserService.Setup(s => s.GetAuditLogs(null, null, null)).Returns(new List<AuditLog>());
        var mockReportingService = new Mock<IReportingService>();
        return new UsersViewModel(mockUserService.Object, mockReportingService.Object);
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
    public void UsersView_HostedInRealWindow_DataGridAlwaysVisible_RegardlessOfIsEditing()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new UsersView { DataContext = vm };

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
    public void UsersView_AddNewCommand_OpensUserFormWindow_AndDataGridStaysVisibleUnderneath()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new UsersView { DataContext = vm };

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

                    // ShowDialog() الذي يستدعيه UsersView.xaml.cs عند IsEditing=true يحجب
                    // مسار التنفيذ الحالي بمضخة رسائل متداخلة (nested message pump) خاصة به.
                    // لذا يُجدوَل التحقق والإغلاق عبر Dispatcher.BeginInvoke قبل استدعاء الأمر،
                    // فتُنفَّذ هذه الخطوة أثناء تشغيل حلقة ShowDialog() المتداخلة نفسها، لا بعدها.
                    UserFormWindow? capturedFormWindow = null;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        capturedFormWindow = Application.Current.Windows
                            .OfType<UserFormWindow>()
                            .FirstOrDefault();

                        Assert.NotNull(capturedFormWindow);
                        Assert.True(vm.IsEditing);
                        Assert.Equal(vm, capturedFormWindow!.DataContext);
                        Assert.Equal(Visibility.Visible, dataGrid!.Visibility);

                        vm.CancelEditCommand.Execute(null);
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);

                    // بعد عودة AddNewCommand.Execute، تكون UserFormWindow قد أُغلقت فعلاً
                    // (CancelEditCommand أعاد IsEditing إلى false فأغلق الكود-خلف النافذة).
                    Assert.False(vm.IsEditing);
                    Assert.DoesNotContain(capturedFormWindow, Application.Current.Windows.OfType<UserFormWindow>());
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
    public void UserFormWindow_ClosedViaNativeCloseButton_ResetsIsEditing_AndAllowsReopening()
    {
        RunInSta(() =>
        {
            var vm = CreateViewModel();
            try
            {
                var view = new UsersView { DataContext = vm };

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

                    // الخطوة 1: فتح UserFormWindow عبر AddNewCommand، ثم محاكاة الإغلاق
                    // عبر زر ✕ الأصلي (Close() مباشرة على النافذة نفسها — وليس عبر
                    // CancelEditCommand)، بنفس أسلوب الجدولة عبر Dispatcher.BeginInvoke.
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        var formWindow = Application.Current.Windows
                            .OfType<UserFormWindow>()
                            .FirstOrDefault();

                        Assert.NotNull(formWindow);
                        Assert.True(vm.IsEditing);

                        // محاكاة إغلاق عبر ✕ / Alt+F4: استدعاء Close() مباشرة على النافذة،
                        // وليس عبر CancelEditCommand.
                        formWindow!.Close();
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);

                    // بعد الإغلاق عبر ✕، يجب أن تُصفَّر IsEditing تلقائياً عبر معالج Closing
                    // في UserFormWindow (الذي يستدعي CancelEditCommand داخلياً)، دون أن
                    // يحتاج المستخدم لاستدعاء زر الإلغاء يدوياً.
                    Assert.False(vm.IsEditing);
                    Assert.Empty(Application.Current.Windows.OfType<UserFormWindow>());

                    // الخطوة 2: التأكد من أن نافذة جديدة فعلاً تُفتح عند استدعاء AddNewCommand
                    // مرة أخرى (وليس لا شيء بسبب مرجع نافذة سابق عالق يمنع الفتح).
                    UserFormWindow? secondFormWindow = null;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
                    {
                        secondFormWindow = Application.Current.Windows
                            .OfType<UserFormWindow>()
                            .FirstOrDefault();

                        Assert.NotNull(secondFormWindow);
                        Assert.True(vm.IsEditing);

                        vm.CancelEditCommand.Execute(null);
                    }), DispatcherPriority.ApplicationIdle);

                    vm.AddNewCommand.Execute(null);

                    Assert.NotNull(secondFormWindow);
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
    /// في التوجيه الحقيقي لأحداث لوحة المفاتيح في WPF (Tunnel ثم Bubble).
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

    private static Role CreateRole() => new Role { Id = System.Guid.NewGuid(), RoleName = "مستخدم عادي" };

    /// <summary>
    /// اختبار انحدار (الجولة 196): يحرِّر مستخدماً موجوداً فعلاً عبر EditCommand الحقيقي، يكتب
    /// قيمة جديدة في أحد الحقول النصية (UpdateSourceTrigger=PropertyChanged في XAML + معالج
    /// PreviewKeyDown في الكود-خلف)، ثم يضغط Enter والتركيز لا يزال داخل الحقل. يتحقق من القيمة
    /// الفعلية التي وصلت لطبقة الخدمة IUserService.UpdateUser عبر Moq Callback (لا من خاصية
    /// الـViewModel وحدها)، وأن UpdateUser استُدعيت مرة واحدة بالضبط، وأن IsEditing أصبحت false.
    /// </summary>
    private static void AssertEnterCommitsNewValue_ForUserField(
        string bindingPath,
        string newTextValue,
        System.Func<User, string?> readPersistedValue)
    {
        RunInSta(() =>
        {
            var role = CreateRole();
            var existing = new User
            {
                Id = System.Guid.NewGuid(),
                FullName = "مستخدم قائم",
                Username = "existinguser",
                Email = "existing@example.com",
                RoleId = role.Id,
                Role = role,
                IsActive = true,
                IsEditor = true,
                Permissions = "Sources"
            };

            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(s => s.GetAllUsers()).Returns(new List<User> { existing });
            mockUserService.Setup(s => s.GetAllRoles()).Returns(new List<Role> { role });
            mockUserService.Setup(s => s.GetAuditLogs(null, null, null)).Returns(new List<AuditLog>());
            User? persistedItem = null;
            int updateCallCount = 0;
            mockUserService.Setup(s => s.UpdateUser(It.IsAny<User>()))
                .Callback<User>(item => { persistedItem = item; updateCallCount++; })
                .Returns((true, "تم تحديث المستخدم"));
            var mockReportingService = new Mock<IReportingService>();

            var vm = new UsersViewModel(mockUserService.Object, mockReportingService.Object);
            try
            {
                vm.LoadData();
                vm.Selected = vm.Users.First(u => u.Id == existing.Id);
                vm.EditCommand.Execute(null);

                var formWindow = new UserFormWindow
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
    public void UserFormWindow_EnterAfterTyping_PersistsNewValue_ForEditFullName()
        => AssertEnterCommitsNewValue_ForUserField("EditFullName", "اسم جديد بعد التعديل", u => u.FullName);

    [Fact]
    public void UserFormWindow_EnterAfterTyping_PersistsNewValue_ForEditEmail()
        => AssertEnterCommitsNewValue_ForUserField("EditEmail", "new-email@example.com", u => u.Email);

    /// <summary>
    /// إثبات عزل المعالج (الجولة 196): يعيد ربط EditFullName صراحةً بمشغّل LostFocus (الافتراضي)
    /// بدلاً من PropertyChanged، لإثبات أن معالج PreviewKeyDown في الكود-خلف وحده — بمعزل تام عن
    /// UpdateSourceTrigger في XAML — كافٍ لتفريغ القيمة الجديدة إلى الربط قبل الحفظ عند Enter.
    /// </summary>
    [Fact]
    public void UserFormWindow_EnterAfterTyping_PersistsNewValue_EvenWhenTextBoxUsesLostFocusTrigger()
    {
        RunInSta(() =>
        {
            var role = CreateRole();
            var existing = new User
            {
                Id = System.Guid.NewGuid(),
                FullName = "مستخدم قائم",
                Username = "existinguser",
                Email = "existing@example.com",
                RoleId = role.Id,
                Role = role,
                IsActive = true,
                IsEditor = true,
                Permissions = "Sources"
            };

            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(s => s.GetAllUsers()).Returns(new List<User> { existing });
            mockUserService.Setup(s => s.GetAllRoles()).Returns(new List<Role> { role });
            mockUserService.Setup(s => s.GetAuditLogs(null, null, null)).Returns(new List<AuditLog>());
            User? persistedItem = null;
            int updateCallCount = 0;
            mockUserService.Setup(s => s.UpdateUser(It.IsAny<User>()))
                .Callback<User>(item => { persistedItem = item; updateCallCount++; })
                .Returns((true, "تم تحديث المستخدم"));
            var mockReportingService = new Mock<IReportingService>();

            var vm = new UsersViewModel(mockUserService.Object, mockReportingService.Object);
            try
            {
                vm.LoadData();
                vm.Selected = vm.Users.First(u => u.Id == existing.Id);
                vm.EditCommand.Execute(null);

                var formWindow = new UserFormWindow
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

                    var textBox = FindTextBoxByBindingPath(formWindow, "EditFullName");
                    Assert.NotNull(textBox);

                    BindingOperations.SetBinding(textBox, TextBox.TextProperty,
                        new Binding("EditFullName") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, Mode = BindingMode.TwoWay });

                    textBox!.Focus();
                    Assert.Same(textBox, Keyboard.FocusedElement);

                    textBox.Text = "اسم بمعزل عن الربط الفوري";
                    SimulateEnterKeyPress(textBox);

                    Assert.Equal(1, updateCallCount);
                    Assert.NotNull(persistedItem);
                    Assert.Equal("اسم بمعزل عن الربط الفوري", persistedItem!.FullName);
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

    /// <summary>
    /// اختبار انحدار (الجولة 196) على مسار المستخدم الجديد: حقل EditUsername مُعطَّل أثناء
    /// التعديل (IsEnabled="{Binding IsNew}") ولا يُرسَل عبر UpdateUser على الإطلاق، لذا يُغطَّى
    /// هنا عبر IUserService.CreateUser بدلاً من UpdateUser — دون لمس UserService الحقيقية.
    /// </summary>
    [Fact]
    public void UserFormWindow_EnterAfterTyping_PersistsNewValue_ForEditUsername_OnNewUserPath()
    {
        RunInSta(() =>
        {
            var role = CreateRole();
            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(s => s.GetAllUsers()).Returns(new List<User>());
            mockUserService.Setup(s => s.GetAllRoles()).Returns(new List<Role> { role });
            mockUserService.Setup(s => s.GetAuditLogs(null, null, null)).Returns(new List<AuditLog>());
            User? persistedItem = null;
            int createCallCount = 0;
            mockUserService.Setup(s => s.CreateUser(It.IsAny<User>(), It.IsAny<string>()))
                .Callback<User, string>((item, pwd) => { persistedItem = item; createCallCount++; })
                .Returns((true, "تم إنشاء المستخدم"));
            var mockReportingService = new Mock<IReportingService>();

            var vm = new UsersViewModel(mockUserService.Object, mockReportingService.Object);
            try
            {
                vm.LoadData();
                vm.AddNewCommand.Execute(null);
                vm.EditFullName = "مستخدم تجريبي جديد";
                vm.EditRoleId = role.Id;
                vm.EditPassword = "Aa123456#";

                var formWindow = new UserFormWindow
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

                    var textBox = FindTextBoxByBindingPath(formWindow, "EditUsername");
                    Assert.NotNull(textBox);
                    Assert.True(textBox!.IsEnabled); // IsEnabled="{Binding IsNew}" ويجب أن يكون true هنا

                    textBox.Focus();
                    Assert.Same(textBox, Keyboard.FocusedElement);

                    textBox.Text = "newusername196";
                    SimulateEnterKeyPress(textBox);

                    Assert.Equal(1, createCallCount);
                    Assert.NotNull(persistedItem);
                    Assert.Equal("newusername196", persistedItem!.Username);
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
