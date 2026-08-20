using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Moq;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

public class AppearanceSettingsTests : IDisposable
{
    public AppearanceSettingsTests()
    {
        SettingsHelper.ClearAllUserSettingsForTesting();
    }

    public void Dispose()
    {
        SettingsHelper.ClearAllUserSettingsForTesting();
    }

    #region 1. اختبارات حفظ واسترجاع تفضيلات المستخدم بشكل مستقل (Per-User Settings)

    [Fact]
    public void SettingsHelper_Theme_IsPerUser_AndIsolated()
    {
        // Arrange & Act
        SettingsHelper.SetUserTheme("Alice", true);
        SettingsHelper.SetUserTheme("Bob", false);

        // Assert
        Assert.True(SettingsHelper.GetUserTheme("Alice"));
        Assert.False(SettingsHelper.GetUserTheme("Bob"));
    }

    [Fact]
    public void SettingsHelper_AccentColor_IsPerUser_AndIsolated()
    {
        // Arrange & Act
        SettingsHelper.SetUserAccentColor("User1", "#1E3F66"); // Royal Navy
        SettingsHelper.SetUserAccentColor("User2", "#3D5A47"); // Forest Green
        SettingsHelper.SetUserAccentColor("User3", "#433E52"); // Slate Graphite

        // Assert
        Assert.Equal("#1E3F66", SettingsHelper.GetUserAccentColor("User1"));
        Assert.Equal("#3D5A47", SettingsHelper.GetUserAccentColor("User2"));
        Assert.Equal("#433E52", SettingsHelper.GetUserAccentColor("User3"));
        Assert.Equal(SettingsHelper.DefaultAccentColor, SettingsHelper.GetUserAccentColor("NewUserWithoutPref"));
    }

    [Fact]
    public void SettingsViewModel_ThemeAndAccentChanges_PersistToCurrentUser()
    {
        // Arrange
        var mockBackupService = new Mock<IBackupService>();
        var mockSettingsService = new Mock<ISystemSettingsService>();
        mockSettingsService.Setup(s => s.GetSetting(It.IsAny<string>(), It.IsAny<string>())).Returns(string.Empty);
        mockSettingsService.Setup(s => s.GetSetting(It.IsAny<string>(), It.IsAny<bool>())).Returns(false);
        mockSettingsService.Setup(s => s.GetSetting(It.IsAny<string>(), It.IsAny<double>())).Returns(10.0);
        mockSettingsService.Setup(s => s.GetSetting(It.IsAny<string>(), It.IsAny<int>())).Returns(60);

        var mockUserService = new Mock<IUserService>();
        var testUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "RadiationOfficer1",
            FullName = "ضابط الوقاية الإشعاعية"
        };
        mockUserService.Setup(u => u.CurrentUser).Returns(testUser);
        mockUserService.Setup(u => u.IsLoggedIn).Returns(true);

        var vm = new SettingsViewModel(
            mockBackupService.Object,
            mockSettingsService.Object,
            null,
            mockUserService.Object);

        // Act
        vm.SelectAccentColorCommand.Execute("#3D5A47");
        vm.SetDarkModeCommand.Execute(null);

        // Assert
        Assert.Equal("#3D5A47", vm.SelectedAccentColor);
        Assert.True(vm.IsDarkMode);
        Assert.Equal("#3D5A47", SettingsHelper.GetUserAccentColor("RadiationOfficer1"));
        Assert.True(SettingsHelper.GetUserTheme("RadiationOfficer1"));
    }

    #endregion

    #region 2. اختبارات التأثير على الموارد المركزية واستقلال السايدبار الثابت

    [Fact]
    public void ApplyAccentColor_UpdatesPrimaryBrush_WithoutAffectingSidebarBackground()
    {
        Sources.Tests.Fixtures.WpfStaFixture.RunInSta(() =>
        {
            var app = Application.Current;
            Assert.NotNull(app);

            var originalSidebarColor = (Color)ColorConverter.ConvertFromString("#123840");
            app.Resources["SidebarBackground"] = new SolidColorBrush(originalSidebarColor);

            // Act: تطبيق لون تمييز جديد (Royal Navy #1E3F66)
            App.ApplyAccentColor("#1E3F66");

            // Assert:
            // 1. تم تحديث PrimaryBrush إلى اللون الجديد
            var updatedPrimaryBrush = app.Resources["PrimaryBrush"] as SolidColorBrush;
            Assert.NotNull(updatedPrimaryBrush);
            Assert.Equal((Color)ColorConverter.ConvertFromString("#1E3F66"), updatedPrimaryBrush.Color);

            // 2. السايدبار لم يتأثر وظل محتفظاً بلونه الثابت (#123840)
            var sidebarBrush = app.Resources["SidebarBackground"] as SolidColorBrush;
            Assert.NotNull(sidebarBrush);
            Assert.Equal(originalSidebarColor, sidebarBrush.Color);
        });
    }

    [Fact]
    public void Consecutive_AccentColor_Switches_UpdateAccurately_WithoutReversion()
    {
        Sources.Tests.Fixtures.WpfStaFixture.RunInSta(() =>
        {
            var app = Application.Current;
            Assert.NotNull(app);

            var colors = new[] { "#1F5A66", "#1E3F66", "#3D5A47", "#433E52", "#1F5A66" };

            foreach (var hex in colors)
            {
                // Act
                App.ApplyAccentColor(hex);

                // Assert
                var expected = (Color)ColorConverter.ConvertFromString(hex);
                var actualBrush = app.Resources["PrimaryBrush"] as SolidColorBrush;
                Assert.NotNull(actualBrush);
                Assert.Equal(expected, actualBrush.Color);
            }
        });
    }

    #endregion

    #region 3. اختبارات التحقق من التباعد اللوني والرياضي للخيارات

    [Fact]
    public void AccentColors_HaveDistinctHues_AndMeetSeparationConstraints()
    {
        var navyHex = "#1E3F66";
        var slateHex = "#433E52";
        var greenHex = "#3D5A47";
        var defaultHex = "#1F5A66";

        var navyColor = (Color)ColorConverter.ConvertFromString(navyHex);
        var slateColor = (Color)ColorConverter.ConvertFromString(slateHex);
        var greenColor = (Color)ColorConverter.ConvertFromString(greenHex);
        var defaultColor = (Color)ColorConverter.ConvertFromString(defaultHex);

        var navyHue = GetHue(navyColor);
        var slateHue = GetHue(slateColor);
        var greenHue = GetHue(greenColor);
        var defaultHue = GetHue(defaultColor);

        // فرق الـ Hue بين الأزرق الكحلي والرمادي الأردوازي يجب أن يكون >= 30 درجة
        var hueDiffNavySlate = Math.Abs(navyHue - slateHue);
        Assert.True(hueDiffNavySlate >= 30, $"Hue difference between Navy ({navyHue:F1}°) and Slate ({slateHue:F1}°) must be >= 30°. Actual: {hueDiffNavySlate:F1}°");

        // التأكد من أن جميع الألوان الـ 4 مميزة ومتباعدة
        var allHues = new[] { navyHue, slateHue, greenHue, defaultHue };
        Assert.Equal(4, allHues.Distinct().Count());
    }

    private static double GetHue(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        if (delta == 0) return 0;

        double hue;
        if (max == r)
            hue = 60 * (((g - b) / delta) % 6);
        else if (max == g)
            hue = 60 * (((b - r) / delta) + 2);
        else
            hue = 60 * (((r - g) / delta) + 4);

        if (hue < 0) hue += 360;
        return hue;
    }

    #endregion
}
