using System;
using System.Collections.Generic;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Xunit;

namespace Sources.Tests;

public class Round207TextLocalizationTests
{
    private static void WithEnglishDictionary(Action assertions)
    {
        Sources.Tests.Fixtures.WpfStaFixture.RunInSta(() =>
        {
            var dicts = System.Windows.Application.Current.Resources.MergedDictionaries;
            var arabicDictIndex = -1;
            for (int i = 0; i < dicts.Count; i++)
            {
                var src = dicts[i].Source?.OriginalString;
                if (src != null && src.Contains("Strings.ar.xaml"))
                {
                    arabicDictIndex = i;
                    break;
                }
            }
            Assert.True(arabicDictIndex >= 0, "Strings.ar.xaml dictionary must already be loaded by WpfStaFixture.");

            try
            {
                dicts[arabicDictIndex] = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Strings.en.xaml", UriKind.Absolute)
                };

                assertions();
            }
            finally
            {
                dicts[arabicDictIndex] = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Sources;component/Resources/Strings.ar.xaml", UriKind.Absolute)
                };
            }
        });
    }

    private static void WithArabicDictionary(Action assertions)
    {
        Sources.Tests.Fixtures.WpfStaFixture.RunInSta(assertions);
    }

    [Fact]
    public void AddedByName_ReturnsCorrectFallback_ForArabicAndEnglish()
    {
        var src = new Source { AddedByUser = null };
        var loc = new Location { AddedByUser = null };
        var req = new BorrowRequest { AddedByUser = null };
        var iso = new Radioisotope { AddedByUser = null };
        var ntype = new NeutronSourceType { AddedByUser = null };
        var nsrc = new NeutronSource { AddedByUser = null };

        WithArabicDictionary(() =>
        {
            Assert.Equal("غير معروف", src.AddedByName);
            Assert.Equal("غير معروف", loc.AddedByName);
            Assert.Equal("غير معروف", req.AddedByName);
            Assert.Equal("غير معروف", iso.AddedByName);
            Assert.Equal("غير معروف", ntype.AddedByName);
            Assert.Equal("غير معروف", nsrc.AddedByName);
        });

        WithEnglishDictionary(() =>
        {
            Assert.Equal("Unknown", src.AddedByName);
            Assert.Equal("Unknown", loc.AddedByName);
            Assert.Equal("Unknown", req.AddedByName);
            Assert.Equal("Unknown", iso.AddedByName);
            Assert.Equal("Unknown", ntype.AddedByName);
            Assert.Equal("Unknown", nsrc.AddedByName);
        });
    }

    [Fact]
    public void AlertSeverityDisplay_ReturnsLocalizedStrings_ForArabicAndEnglish()
    {
        var crit = new Source { AlertSeverity = "Critical" };
        var warn = new Source { AlertSeverity = "Warning" };
        var other = new Source { AlertSeverity = "Info" };

        WithArabicDictionary(() =>
        {
            Assert.Equal("حرج", crit.AlertSeverityDisplay);
            Assert.Equal("تحذير", warn.AlertSeverityDisplay);
            Assert.Equal("Info", other.AlertSeverityDisplay);
        });

        WithEnglishDictionary(() =>
        {
            Assert.Equal("Critical", crit.AlertSeverityDisplay);
            Assert.Equal("Warning", warn.AlertSeverityDisplay);
            Assert.Equal("Info", other.AlertSeverityDisplay);
        });
    }

    [Fact]
    public void User_StatusDisplayName_ReturnsLocalizedStrings_ForArabicAndEnglish()
    {
        var activeUser = new User { IsActive = true };
        var inactiveUser = new User { IsActive = false };

        WithArabicDictionary(() =>
        {
            Assert.Equal("نشط", activeUser.StatusDisplayName);
            Assert.Equal("موقوف", inactiveUser.StatusDisplayName);
        });

        WithEnglishDictionary(() =>
        {
            Assert.Equal("Active", activeUser.StatusDisplayName);
            Assert.Equal("Inactive", inactiveUser.StatusDisplayName);
        });
    }

    [Fact]
    public void LeakTestRecord_StatusDisplay_ReturnsLocalizedStrings_ForArabicAndEnglish()
    {
        var overdue = new LeakTestRecord { NextDueDate = AppClock.Current.LocalToday().AddDays(-5) };
        var valid = new LeakTestRecord { NextDueDate = AppClock.Current.LocalToday().AddDays(30) };

        WithArabicDictionary(() =>
        {
            Assert.Equal("متأخر", overdue.StatusDisplay);
            Assert.Equal("ساري", valid.StatusDisplay);
        });

        WithEnglishDictionary(() =>
        {
            Assert.Equal("Overdue", overdue.StatusDisplay);
            Assert.Equal("Valid", valid.StatusDisplay);
        });
    }

    [Fact]
    public void TooltipText_ReturnsLocalizedContent_ForArabicAndEnglish()
    {
        var emptyEstimate = new DoseRateResult
        {
            Contributions = new List<DoseRateIsotopeContribution>()
        };

        WithArabicDictionary(() =>
        {
            Assert.Equal("لا توجد بيانات للنظائر", emptyEstimate.TooltipText);
        });

        WithEnglishDictionary(() =>
        {
            Assert.Equal("No isotope data available", emptyEstimate.TooltipText);
        });

        var cs137 = new Radioisotope { Symbol = "Cs-137", RadiationType = "Gamma" };
        var estimate = new DoseRateResult
        {
            TotalDoseRateMicroSvPerHour = 10.5,
            Contributions = new List<DoseRateIsotopeContribution>
            {
                new()
                {
                    Isotope = cs137,
                    Status = DoseRateContributionStatus.Contributing,
                    ContributionMicroSvPerHour = 10.5,
                    GammaConstant = 0.0772
                }
            }
        };

        WithArabicDictionary(() =>
        {
            var text = estimate.TooltipText;
            Assert.Contains("معدل الجرعة التقديري عند 1 متر في الهواء:", text);
            Assert.Contains("الإجمالي:", text);
            Assert.Contains("Cs-137", text);
        });

        WithEnglishDictionary(() =>
        {
            var text = estimate.TooltipText;
            Assert.Contains("Estimated dose rate at 1 meter in air:", text);
            Assert.Contains("Total:", text);
            Assert.Contains("Cs-137", text);
        });
    }
}
