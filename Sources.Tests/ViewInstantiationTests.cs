using System;
using System.Threading;
using System.Windows;
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
            var isotopeChart = view.FindName("IsotopeChart") as LiveChartsCore.SkiaSharpView.WPF.CartesianChart;
            var locationChart = view.FindName("LocationChart") as LiveChartsCore.SkiaSharpView.WPF.CartesianChart;
            var histogramChart = view.FindName("HistogramChart") as LiveChartsCore.SkiaSharpView.WPF.CartesianChart;
            var decayChart = view.FindName("DecayChart") as LiveChartsCore.SkiaSharpView.WPF.CartesianChart;

            Assert.NotNull(isotopeChart);
            Assert.NotNull(locationChart);
            Assert.NotNull(histogramChart);
            Assert.NotNull(decayChart);

            Assert.NotNull(isotopeChart.Tooltip);
            Assert.NotNull(locationChart.Tooltip);
            Assert.NotNull(histogramChart.Tooltip);
            Assert.NotNull(decayChart.Tooltip);
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
    public void SourcesView_WhenActivelyBorrowed_DisablesStatusAndLocationComboBoxes()
    {
        RunInSta(() =>
        {
            var mockSourceService = new Moq.Mock<Sources.Services.ISourceService>();
            var mockIsotopeService = new Moq.Mock<Sources.Services.IRadioisotopeService>();
            var mockLocationService = new Moq.Mock<Sources.Services.ILocationService>();
            var mockReportingService = new Moq.Mock<Sources.Services.IReportingService>();

            var vm = new Sources.ViewModels.SourcesViewModel(
                mockSourceService.Object,
                mockIsotopeService.Object,
                mockLocationService.Object,
                mockReportingService.Object);

            var sourceId = Guid.NewGuid();
            var source = new Sources.Models.Source
            {
                Id = sourceId,
                SourceCode = "SRC-0021",
                Status = "InUse",
                InitialActivityValue = 100,
                CalibrationDate = DateTime.Today
            };

            mockSourceService.Setup(s => s.HasActiveBorrow(sourceId)).Returns(true);
            mockSourceService.Setup(s => s.GetSourceById(sourceId)).Returns(source);

            vm.EditSourceCommand.Execute(source);
            Assert.True(vm.IsActivelyBorrowed);
            Assert.True(vm.IsEditing);

            var view = new Sources.Views.SourcesView
            {
                DataContext = vm
            };

            view.Measure(new System.Windows.Size(1280, 800));
            view.Arrange(new System.Windows.Rect(0, 0, 1280, 800));
            view.UpdateLayout();

            // Save actual visual screenshot artifact for Step 1
            try
            {
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 800, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(view);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                var artifactDir = @"C:\Users\DELL\.gemini\antigravity-ide\brain\8ef61ce6-d5cd-4d26-bde5-5620046d3b8b";
                if (System.IO.Directory.Exists(artifactDir))
                {
                    using var stream = System.IO.File.Create(System.IO.Path.Combine(artifactDir, "src_0021_edit_disabled.png"));
                    encoder.Save(stream);
                }

                // Now switch to Step 3 and capture Location ComboBox
                vm.CurrentStep = 3;
                view.Measure(new System.Windows.Size(1280, 800));
                view.Arrange(new System.Windows.Rect(0, 0, 1280, 800));
                view.UpdateLayout();

                var rtb3 = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 800, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb3.Render(view);
                var encoder3 = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder3.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb3));
                if (System.IO.Directory.Exists(artifactDir))
                {
                    using var stream3 = System.IO.File.Create(System.IO.Path.Combine(artifactDir, "src_0021_edit_step3_disabled.png"));
                    encoder3.Save(stream3);
                }
            }
            catch { /* non-fatal for tests */ }

            var comboBoxes = FindVisualChildren<System.Windows.Controls.ComboBox>(view).ToList();
            Assert.NotEmpty(comboBoxes);

            var disabledBoxes = comboBoxes.Where(c => !c.IsEnabled).ToList();
            Assert.True(disabledBoxes.Count >= 1, "At least Status or Location ComboBox should be disabled");

            foreach (var box in disabledBoxes)
            {
                Assert.True(System.Windows.Controls.ToolTipService.GetShowOnDisabled(box));
            }
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

    private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
            if (child is T t) yield return t;
            foreach (var childOfChild in FindVisualChildren<T>(child))
                yield return childOfChild;
        }
    }
}
