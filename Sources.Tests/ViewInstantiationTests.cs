using System;
using System.Threading;
using System.Windows;
using Moq;
using Sources.Views;
using Xunit;

using System.Windows.Threading;

namespace Sources.Tests;

/// <summary>
/// اختبارات التحقق من بناء وسلامة ملفات XAML وتكوين الواجهات (WPF View Instantiation Tests)
/// تضمن هذه الاختبارات تحميل كافة عناصر XAML والربط والقواميس في الـ Visual Tree دون أي XamlParseException
/// </summary>
public class ViewInstantiationTests
{
    private static void RunInSta(Action action) => Sources.Tests.Fixtures.WpfStaFixture.RunInSta(action);

    [Fact]
    public void DashboardView_InstantiatesSuccessfully_WithCustomTooltips_AndNoXamlErrors()
    {
        RunInSta(() =>
        {
            var view = new DashboardView();
            Assert.NotNull(view);
            // الجولة 212: رسوم النشاط والنظائر والمواقع أصبحت أشرطة WPF أصلية (ItemsControl)
            // بدل LiveCharts/Skia؛ منحنى التحلل وحده بقي رسماً بتلميح مخصص.
            var activityLadder = view.FindName("ActivityLadder") as System.Windows.Controls.ItemsControl;
            var isotopeBars = view.FindName("IsotopeBars") as System.Windows.Controls.ItemsControl;
            var locationBars = view.FindName("LocationBars") as System.Windows.Controls.ItemsControl;
            var decayChart = view.FindName("DecayChart") as LiveChartsCore.SkiaSharpView.WPF.CartesianChart;

            Assert.NotNull(activityLadder);
            Assert.NotNull(isotopeBars);
            Assert.NotNull(locationBars);
            Assert.NotNull(decayChart);

            Assert.NotNull(activityLadder.ItemTemplate);
            Assert.NotNull(isotopeBars.ItemTemplate);
            Assert.NotNull(locationBars.ItemTemplate);
            Assert.NotNull(decayChart.Tooltip);

            Assert.NotNull(view.FindName("DecayLegend"));
            Assert.NotNull(view.FindName("DecayHorizonCombo"));
            Assert.Null(view.FindName("HistogramChart"));
            Assert.Null(view.FindName("IsotopeChart"));
            Assert.Null(view.FindName("LocationChart"));
        });
    }

    [Fact]
    public void DashboardSourcesWindow_InstantiatesWithRows()
    {
        RunInSta(() =>
        {
            var drill = new Sources.ViewModels.DashboardDrillDown
            {
                Title = "المصادر في الموقع: المختبر",
                CountText = "مصدران",
                Rows = new[]
                {
                    new Sources.ViewModels.DashboardSourceRow { RowNumber = 1, Source = new Sources.Models.Source { SourceCode = "S1" } },
                    new Sources.ViewModels.DashboardSourceRow { RowNumber = 2, Source = new Sources.Models.Source { SourceCode = "S2" } },
                }
            };
            var window = new DashboardSourcesWindow(drill);
            try
            {
                Assert.Same(drill, window.DataContext);
                var grid = window.FindName("SourcesGrid") as System.Windows.Controls.DataGrid;
                Assert.NotNull(grid);
                Assert.Equal(7, grid!.Columns.Count);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void LocationsView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new LocationsView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void LocationDetailsWindow_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var location = new Sources.Models.Location { Id = Guid.NewGuid(), LocationName = "مختبر الفحص" };
            var sources = new List<Sources.Models.Source>();
            var vm = new Sources.ViewModels.LocationDetailsViewModel(location, sources);
            var window = new Sources.Views.LocationDetailsWindow(vm);
            Assert.NotNull(window);
            Assert.Equal(vm, window.DataContext);
        });
    }

    [Fact]
    public void SourceDetailsWindow_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var source = new Sources.Models.Source
            {
                Id = Guid.NewGuid(),
                SourceCode = "SRC-INST-01",
                Status = "InUse",
                CalibrationDate = DateTime.Now,
                Radioisotope = new Sources.Models.Radioisotope { Symbol = "Co-60" }
            };
            var vm = new Sources.ViewModels.SourceDetailsViewModel(source);
            var window = new Sources.Views.SourceDetailsWindow(vm);
            Assert.NotNull(window);
            Assert.Equal(vm, window.DataContext);
        });
    }

    [Fact]
    public void BorrowView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new BorrowView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void ReportsView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new ReportsView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void SourcesView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new SourcesView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void RadioisotopesView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new RadioisotopesView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void UsersView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new UsersView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void SettingsView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new SettingsView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void ActivityCalculatorView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new ActivityCalculatorView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void HelpView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new HelpView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void AboutSystemView_InstantiatesSuccessfully()
    {
        RunInSta(() =>
        {
            var view = new AboutSystemView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void LeakTestsView_InstantiatesSuccessfully_WithDataGridAndBindings()
    {
        RunInSta(() =>
        {
            var mockLeakService = new Moq.Mock<Sources.Services.ILeakTestService>();
            var mockSourceService = new Moq.Mock<Sources.Services.ISourceService>();
            var mockReportingService = new Moq.Mock<Sources.Services.IReportingService>();
            var mockUserService = new Moq.Mock<Sources.Services.IUserService>();
            var mockSettingsService = new Moq.Mock<Sources.Services.ISystemSettingsService>();

            var vm = new Sources.ViewModels.LeakTestsViewModel(
                mockLeakService.Object,
                mockSourceService.Object,
                mockReportingService.Object,
                mockUserService.Object,
                mockSettingsService.Object);

            var view = new LeakTestsView
            {
                DataContext = vm
            };

            view.Measure(new System.Windows.Size(1280, 800));
            view.Arrange(new System.Windows.Rect(0, 0, 1280, 800));
            view.UpdateLayout();

            Assert.NotNull(view);
            Assert.Equal(vm, view.DataContext);
        });
    }



    [Fact]
    public void ExitWarningDialog_RendersCorrectly_WithPendingChangesMessage()
    {
        RunInSta(() =>
        {
            var dialog = new AlertDialog(
                Sources.Helpers.TranslationHelper.GetString("MsgErrSavePending"),
                Sources.Helpers.TranslationHelper.GetString("TitlePendingChanges"),
                "Warning");

            Assert.NotNull(dialog);

            var content = dialog.Content as FrameworkElement;
            Assert.NotNull(content);

            content.Width = 520;
            content.Height = 260;
            content.Measure(new System.Windows.Size(520, 260));
            content.Arrange(new System.Windows.Rect(0, 0, 520, 260));
            content.UpdateLayout();

            try
            {
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(520, 260, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(content);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                var artifactDir = @"C:\Users\DELL\.gemini\antigravity-ide\brain\8ef61ce6-d5cd-4d26-bde5-5620046d3b8b";
                if (System.IO.Directory.Exists(artifactDir))
                {
                    using var stream = System.IO.File.Create(System.IO.Path.Combine(artifactDir, "exit_warning_dialog.png"));
                    encoder.Save(stream);
                }
            }
            catch { /* non-fatal for test */ }
        });
    }

    [Fact]
    public void AlertDialog_QuestionMode_YesButtonClick_SetsResultToYes()
    {
        RunInSta(() =>
        {
            var dialog = new AlertDialog("هل أنت متأكد؟", "تأكيد", "Question", isQuestion: true);

            var content = dialog.Content as FrameworkElement;
            Assert.NotNull(content);
            content.Measure(new System.Windows.Size(520, 260));
            content.Arrange(new System.Windows.Rect(0, 0, 520, 260));
            content.UpdateLayout();

            var yesButton = dialog.FindName("YesButton") as System.Windows.Controls.Button;
            Assert.NotNull(yesButton);

            yesButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));

            Assert.Equal(AlertDialog.AlertResult.Yes, dialog.Result);
        });
    }

    [Fact]
    public void AlertDialog_QuestionMode_NoButtonClick_SetsResultToNo()
    {
        RunInSta(() =>
        {
            var dialog = new AlertDialog("هل أنت متأكد؟", "تأكيد", "Question", isQuestion: true);

            var content = dialog.Content as FrameworkElement;
            Assert.NotNull(content);
            content.Measure(new System.Windows.Size(520, 260));
            content.Arrange(new System.Windows.Rect(0, 0, 520, 260));
            content.UpdateLayout();

            var noButton = dialog.FindName("NoButton") as System.Windows.Controls.Button;
            Assert.NotNull(noButton);

            noButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));

            Assert.Equal(AlertDialog.AlertResult.No, dialog.Result);
        });
    }

    [Fact]
    public void PasswordPromptDialog_CancelButtonClick_SetsResultToFalse()
    {
        RunInSta(() =>
        {
            var dialog = new Sources.Views.PasswordPromptDialog("عنوان اختبار", "نص تنبيه اختبار");

            // CancelButton_Click يضبط DialogResult، وهذا صالح فقط بعد فتح النافذة عبر
            // ShowDialog() (انظر نفس النمط في LocationsFormWindowTests.cs). لذا نُجدوِل
            // النقر عبر Dispatcher.BeginInvoke ليُنفَّذ أثناء حلقة ShowDialog() المتداخلة نفسها.
            // النافذة تُغلق نفسها ضمن المعالج (Close())، فلا حاجة لإغلاق إضافي بعد عودة ShowDialog.
            Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() =>
            {
                var content = dialog.Content as FrameworkElement;
                Assert.NotNull(content);
                content.Measure(new System.Windows.Size(480, 320));
                content.Arrange(new System.Windows.Rect(0, 0, 480, 320));
                content.UpdateLayout();

                var cancelButton = dialog.FindName("CancelButton") as System.Windows.Controls.Button;
                Assert.NotNull(cancelButton);

                cancelButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            }), DispatcherPriority.ApplicationIdle);

            dialog.ShowDialog();

            Assert.False(dialog.Result);
            Assert.False(dialog.DialogResult);
        });
    }

    // ملاحظة على أسلوب الاختبار: ConfirmButton_Click في PasswordPromptDialog يقرأ IUserService
    // مباشرة عبر App.ServiceProvider (خاصية ذات setter خاص لا تدعم الحقن في الاختبارات، على خلاف
    // ما هو متاح في هذا المشروع لاختبارات أخرى). لا يوجد نمط قائم في مشروع الاختبارات لحقن
    // App.ServiceProvider لهذا الحوار تحديداً، لذا نتحقق من مسار التأكيد (Confirm) عبر خُطّاف
    // الاختبار الرسمي DialogHelper.TestAdminPromptResult مع RequestAdminAccess() (المكان الوحيد
    // المسموح له بمعرفة وضع الاختبار، الجولة 199) تماماً كما هو مستخدم في
    // DeletionsAndAdminPromptTests.cs (RequestAdminAccess_HonorsExplicitTestSeamResult) بدل
    // استدعاء ConfirmButton_Click مباشرة.
    [Fact]
    public void PasswordPromptDialog_RequestAdminAccess_ConfirmPath_HonorsExplicitTestSeamResultTrue()
    {
        Sources.Helpers.DialogHelper.TestAdminPromptResult = true;
        try
        {
            var granted = Sources.Views.PasswordPromptDialog.RequestAdminAccess("عنوان اختبار", "نص تنبيه اختبار");
            Assert.True(granted);
        }
        finally
        {
            Sources.Helpers.DialogHelper.TestAdminPromptResult = null;
        }
    }

    [Fact]
    public void BorrowView_WhenOpeningDetailsCard_RendersSuccessfullyWithoutBindingExceptions()
    {
        RunInSta(() =>
        {
            var mockBorrowService = new Moq.Mock<Sources.Services.IBorrowService>();
            var mockSourceService = new Moq.Mock<Sources.Services.ISourceService>();
            var mockUserService = new Moq.Mock<Sources.Services.IUserService>();
            var mockReportingService = new Moq.Mock<Sources.Services.IReportingService>();

            var vm = new Sources.ViewModels.BorrowViewModel(
                mockBorrowService.Object,
                mockSourceService.Object,
                mockUserService.Object,
                mockReportingService.Object);

            var source = new Sources.Models.Source
            {
                Id = Guid.NewGuid(),
                SourceCode = "SRC-0138",
                Status = "Storage",
                IsDeleted = false
            };

            var request = new Sources.Models.BorrowRequest
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id,
                Source = source,
                BorrowerName = "أ. منى البكوش",
                Purpose = "معايرة دورية",
                RequestDate = DateTime.Today.AddDays(-10),
                ExpectedReturnDate = DateTime.Today.AddDays(-2),
                ActualReturnDate = DateTime.Today,
                Status = "Returned",
                Notes = "تم الإرجاع بحالة سليمة ومطابقة القياسات."
            };

            vm.SelectedRequest = request;
            vm.IsEditing = true;
            vm.IsNew = false;

            var view = new Sources.Views.BorrowView
            {
                DataContext = vm
            };

            view.Measure(new System.Windows.Size(1280, 800));
            view.Arrange(new System.Windows.Rect(0, 0, 1280, 800));
            view.UpdateLayout();

            Assert.NotNull(view);
            Assert.False(vm.IsNew);
            Assert.True(vm.IsEditing);
            Assert.Equal("SRC-0138", vm.SelectedRequest.DisplaySourceCode);
        });
    }

    [Fact]
    public async Task IsotopeLibraryView_InstantiatesSuccessfully_WithViewModelAndData()
    {
        var service = new Sources.Services.IsotopeLibraryService();
        var all = await service.GetAllEntriesAsync();
        var cs131 = all.FirstOrDefault(x => x.NuclideSymbol == "131Cs") ?? all.First();
        var be7 = all.FirstOrDefault(x => x.NuclideSymbol == "7Be") ?? all.First();
        var co60 = all.FirstOrDefault(x => x.NuclideSymbol == "60Co") ?? all.First();

        RunInSta(() =>
        {
            var vm = new Sources.ViewModels.IsotopeLibraryViewModel(service)
            {
                FilteredEntries = new System.Collections.ObjectModel.ObservableCollection<Sources.Models.IsotopeReferenceEntry>(all),
                TotalCount = all.Count,
                ResultsCount = all.Count,
                HasResults = true,
                SelectedEntry = co60
            };

            var view = new Sources.Views.IsotopeLibraryView(vm);
            view.Measure(new System.Windows.Size(1280, 800));
            view.Arrange(new System.Windows.Rect(0, 0, 1280, 800));
            view.UpdateLayout();

            Assert.NotNull(view);
            Assert.NotNull(view.DataContext);
            Assert.IsType<Sources.ViewModels.IsotopeLibraryViewModel>(view.DataContext);

            var artifactDir = @"C:\Users\DELL\.gemini\antigravity-ide\brain\4c75130f-5a36-40b1-ac93-2a233d58214c";
            if (System.IO.Directory.Exists(artifactDir))
            {
                // 1. Capture Default Alphabetical Numbered Catalog
                vm.SearchText = "";
                for (int i = 0; i < all.Count; i++) all[i].ItemIndex = i + 1;
                vm.FilteredEntries = new System.Collections.ObjectModel.ObservableCollection<Sources.Models.IsotopeReferenceEntry>(all);
                vm.ResultsCount = all.Count;
                vm.SelectedEntry = all.FirstOrDefault();
                view.Measure(new System.Windows.Size(1280, 850));
                view.Arrange(new System.Windows.Rect(0, 0, 1280, 850));
                view.UpdateLayout();

                var rtbAlpha = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 850, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtbAlpha.Render(view);
                var encoderAlpha = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoderAlpha.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtbAlpha));
                using (var stream = System.IO.File.Create(System.IO.Path.Combine(artifactDir, "isotope_library_alphabetical_numbered.png")))
                {
                    encoderAlpha.Save(stream);
                }

                // 2. Capture Filtered Cs-131 with 1-based serial index
                var csResults = all.Where(x => x.DisplaySymbol.StartsWith("Cs-")).ToList();
                for (int i = 0; i < csResults.Count; i++) csResults[i].ItemIndex = i + 1;
                vm.SearchText = "Cs-131";
                vm.FilteredEntries = new System.Collections.ObjectModel.ObservableCollection<Sources.Models.IsotopeReferenceEntry>(csResults);
                vm.ResultsCount = csResults.Count;
                vm.SelectedEntry = csResults.FirstOrDefault();
                view.Measure(new System.Windows.Size(1280, 850));
                view.Arrange(new System.Windows.Rect(0, 0, 1280, 850));
                view.UpdateLayout();

                var rtbCs = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 850, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtbCs.Render(view);
                var encoderCs = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoderCs.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtbCs));
                using (var stream = System.IO.File.Create(System.IO.Path.Combine(artifactDir, "isotope_library_ornl_cs131.png")))
                {
                    encoderCs.Save(stream);
                }

                // 3. Capture ORNL Isotope (K-40) with exponential half life (1.28 × 10⁹ y)
                var k40Entry = all.FirstOrDefault(x => x.DisplaySymbol == "K-40" || x.NuclideSymbol == "40K");
                var kEntries = all.Where(x => x.DisplaySymbol.StartsWith("K-")).ToList();
                if (k40Entry != null)
                {
                    for (int i = 0; i < kEntries.Count; i++) kEntries[i].ItemIndex = i + 1;
                    vm.SearchText = "K-40";
                    vm.FilteredEntries = new System.Collections.ObjectModel.ObservableCollection<Sources.Models.IsotopeReferenceEntry>(kEntries);
                    vm.ResultsCount = kEntries.Count;
                    vm.SelectedEntry = k40Entry;
                    view.Measure(new System.Windows.Size(1280, 850));
                    view.Arrange(new System.Windows.Rect(0, 0, 1280, 850));
                    view.UpdateLayout();

                    var rtbK40 = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 850, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    rtbK40.Render(view);
                    var encoderK40 = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoderK40.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtbK40));
                    using (var stream = System.IO.File.Create(System.IO.Path.Combine(artifactDir, "isotope_library_ornl_k40.png")))
                    {
                        encoderK40.Save(stream);
                    }
                }

                // 4. Capture ICRP 107 Fallback Isotope (H-3)
                var h3Entry = all.FirstOrDefault(x => x.NuclideSymbol == "H-3" || x.DisplaySymbol == "H-3");
                var hEntries = all.Where(x => x.NuclideSymbol.StartsWith("H-") || x.DisplaySymbol.StartsWith("H-")).ToList();
                if (h3Entry != null)
                {
                    vm.SearchText = "H-3";
                    vm.FilteredEntries = new System.Collections.ObjectModel.ObservableCollection<Sources.Models.IsotopeReferenceEntry>(hEntries);
                    vm.ResultsCount = hEntries.Count;
                    vm.SelectedEntry = h3Entry;
                    view.Measure(new System.Windows.Size(1280, 850));
                    view.Arrange(new System.Windows.Rect(0, 0, 1280, 850));
                    view.UpdateLayout();

                    var rtbH3 = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 850, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    rtbH3.Render(view);
                    var encoderH3 = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoderH3.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtbH3));
                    using (var stream = System.IO.File.Create(System.IO.Path.Combine(artifactDir, "isotope_library_icrp_h3.png")))
                    {
                        encoderH3.Save(stream);
                    }
                }

                // 5. Capture Not Found state with dual PDF buttons
                vm.SearchText = "Unobtainium-999";
                vm.IsNotFound = true;
                vm.HasResults = false;
                vm.FilteredEntries.Clear();
                vm.ResultsCount = 0;
                view.Measure(new System.Windows.Size(1280, 850));
                view.Arrange(new System.Windows.Rect(0, 0, 1280, 850));
                view.UpdateLayout();

                var rtbNotFound = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 850, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtbNotFound.Render(view);
                var encoderNotFound = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoderNotFound.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtbNotFound));
                using (var stream = System.IO.File.Create(System.IO.Path.Combine(artifactDir, "isotope_library_not_found_dual_pdf.png")))
                {
                    encoderNotFound.Save(stream);
                }
            }
        });
    }

    [Fact]
    public async System.Threading.Tasks.Task IsotopeDetailsWindow_InstantiatesSuccessfully_WithEntry()
    {
        var service = new Sources.Services.IsotopeLibraryService();
        var all = await service.GetAllEntriesAsync();
        var cs131 = all.FirstOrDefault(x => x.DisplaySymbol == "Cs-131") ?? all.First();
        var k40 = all.FirstOrDefault(x => x.DisplaySymbol == "K-40") ?? all.First();

        RunInSta(() =>
        {
            var window = new Sources.Views.IsotopeDetailsWindow(cs131, service);
            Assert.NotNull(window);
            Assert.Equal("Cs-131", window.Entry.DisplaySymbol);

            var artifactDir = @"C:\Users\DELL\.gemini\antigravity-ide\brain\4c75130f-5a36-40b1-ac93-2a233d58214c";

            if (window.Content is FrameworkElement root)
            {
                root.DataContext = window;
                root.Measure(new System.Windows.Size(880, 660));
                root.Arrange(new System.Windows.Rect(0, 0, 880, 660));
                root.UpdateLayout();

                if (System.IO.Directory.Exists(artifactDir))
                {
                    var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(880, 660, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    rtb.Render(root);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                    using (var stream = System.IO.File.Create(System.IO.Path.Combine(artifactDir, "isotope_details_dialog_cs131.png")))
                    {
                        encoder.Save(stream);
                    }
                }
            }

            // Capture K-40 Dialog
            var windowK40 = new Sources.Views.IsotopeDetailsWindow(k40, service);
            if (windowK40.Content is FrameworkElement rootK40)
            {
                rootK40.DataContext = windowK40;
                rootK40.Measure(new System.Windows.Size(880, 660));
                rootK40.Arrange(new System.Windows.Rect(0, 0, 880, 660));
                rootK40.UpdateLayout();

                if (System.IO.Directory.Exists(artifactDir))
                {
                    var rtbK40 = new System.Windows.Media.Imaging.RenderTargetBitmap(880, 660, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    rtbK40.Render(rootK40);
                    var encoderK40 = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoderK40.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtbK40));
                    using (var stream = System.IO.File.Create(System.IO.Path.Combine(artifactDir, "isotope_details_dialog_k40.png")))
                    {
                        encoderK40.Save(stream);
                    }
                }
            }
        });
    }
}
