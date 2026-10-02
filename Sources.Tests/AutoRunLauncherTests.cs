using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Sources.AutoRun.Logic;
using Xunit;

namespace Sources.Tests;

/// <summary>الجولة 214: منطق واجهة التشغيل التلقائي (ملفات Sources.AutoRun/Logic مرتبطة بمشروع الاختبار).</summary>
public sealed class AutoRunLauncherTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "autorun-tests-" + Guid.NewGuid().ToString("N"));

    public AutoRunLauncherTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string WriteInstaller(string name, string content, string? checksumText)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        if (checksumText != null) File.WriteAllText(path + ".sha256", checksumText);
        return path;
    }

    private static string Sha256Of(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    // ── ChecksumVerifier ──

    [Fact]
    public void Verify_MatchingChecksum_Ok()
    {
        var path = WriteInstaller("SourcesSystemSetup_v1.0.0.exe", "payload", Sha256Of("payload") + "  SourcesSystemSetup_v1.0.0.exe\n");
        Assert.Equal(ChecksumStatus.Ok, ChecksumVerifier.Verify(path));
    }

    [Fact]
    public void Verify_LowercaseHashAndBom_Ok()
    {
        var path = WriteInstaller("a.exe", "payload", "\uFEFF" + Sha256Of("payload").ToLowerInvariant() + "  a.exe");
        Assert.Equal(ChecksumStatus.Ok, ChecksumVerifier.Verify(path));
    }

    [Fact]
    public void Verify_TamperedFile_Mismatch()
    {
        var path = WriteInstaller("a.exe", "payload", Sha256Of("payload") + "  a.exe");
        File.WriteAllText(path, "tampered");
        Assert.Equal(ChecksumStatus.Mismatch, ChecksumVerifier.Verify(path));
    }

    [Fact]
    public void Verify_MissingFile_FileMissing() =>
        Assert.Equal(ChecksumStatus.FileMissing, ChecksumVerifier.Verify(Path.Combine(_dir, "none.exe")));

    [Fact]
    public void Verify_NoChecksumFile_FailsClosed()
    {
        var path = WriteInstaller("a.exe", "payload", null);
        Assert.Equal(ChecksumStatus.ChecksumFileMissing, ChecksumVerifier.Verify(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash  a.exe")]
    [InlineData("ABC123")]
    public void Verify_MalformedChecksumFile_Invalid(string checksumText)
    {
        var path = WriteInstaller("a.exe", "payload", checksumText);
        Assert.Equal(ChecksumStatus.ChecksumFileInvalid, ChecksumVerifier.Verify(path));
    }

    [Fact]
    public void ParseExpected_RejectsNonHexOf64Chars() =>
        Assert.Null(ChecksumVerifier.ParseExpected(new string('G', 64)));

    // ── LauncherConfig ──

    [Fact]
    public void Config_ParsesActivationCode_IgnoringCommentsAndWhitespace()
    {
        var config = LauncherConfig.Parse("# comment\r\n  ActivationCode =  TNRC-AAAA-BBBB \r\nOther=1\r\n");
        Assert.Equal("TNRC-AAAA-BBBB", config.ActivationCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("# ActivationCode=hidden")]
    [InlineData("ActivationCode=   ")]
    [InlineData("garbage line")]
    public void Config_NoUsableCode_ReturnsNull(string? text) =>
        Assert.Null(LauncherConfig.Parse(text).ActivationCode);

    [Fact]
    public void Config_Load_MissingFile_ReturnsEmpty() =>
        Assert.Null(LauncherConfig.Load(_dir).ActivationCode);

    [Fact]
    public void Config_Load_ReadsFileWithBom()
    {
        File.WriteAllText(Path.Combine(_dir, LauncherConfig.FileName), "ActivationCode=X-1\n", new UTF8Encoding(true));
        Assert.Equal("X-1", LauncherConfig.Load(_dir).ActivationCode);
    }

    // ── PackageLocator ──

    [Fact]
    public void FindInstaller_PicksHighestVersionNumerically()
    {
        foreach (var name in new[] { "SourcesSystemSetup_v1.9.0.exe", "SourcesSystemSetup_v1.10.0.exe", "SourcesSystemSetup_v1.2.0.exe.sha256" })
            File.WriteAllText(Path.Combine(_dir, name), "x");

        Assert.Equal("SourcesSystemSetup_v1.10.0.exe", Path.GetFileName(PackageLocator.FindInstaller(_dir)));
    }

    [Fact]
    public void FindInstaller_None_ReturnsNull() => Assert.Null(PackageLocator.FindInstaller(_dir));

    [Fact]
    public void FindInstaller_MissingDirectory_ReturnsNull() =>
        Assert.Null(PackageLocator.FindInstaller(Path.Combine(_dir, "nope")));

    [Fact]
    public void ParseVersion_ReadsVersionFromName()
    {
        Assert.Equal(new Version(1, 1, 2), PackageLocator.ParseVersion(@"C:\x\SourcesSystemSetup_v1.1.2.exe"));
        Assert.Null(PackageLocator.ParseVersion(@"C:\x\SourcesSystemSetup_vABC.exe"));
    }

    [Fact]
    public void GuidePath_UsesLanguageCode()
    {
        Assert.EndsWith(Path.Combine("Docs", "UserGuide.ar.pdf"), PackageLocator.GuidePath(_dir, LauncherLanguage.Arabic));
        Assert.EndsWith(Path.Combine("Docs", "UserGuide.en.pdf"), PackageLocator.GuidePath(_dir, LauncherLanguage.English));
    }

    // ── اللغة والنصوص ──

    [Theory]
    [InlineData("ar", "en", LauncherLanguage.Arabic)]
    [InlineData("en", "ar", LauncherLanguage.English)]
    [InlineData(" AR ", "en", LauncherLanguage.Arabic)]
    [InlineData(null, "ar", LauncherLanguage.Arabic)]
    [InlineData(null, "en", LauncherLanguage.English)]
    [InlineData("junk", "ar", LauncherLanguage.Arabic)]
    [InlineData(null, "fr", LauncherLanguage.English)]
    public void Resolve_PrefersSavedThenWindowsLanguage(string? saved, string windows, LauncherLanguage expected) =>
        Assert.Equal(expected, LauncherLanguageExtensions.Resolve(saved, windows));

    [Fact]
    public void Strings_BothLanguagesHaveSameKeysAndNonEmptyValues()
    {
        var arabic = LauncherStrings.Keys(LauncherLanguage.Arabic).OrderBy(k => k).ToList();
        var english = LauncherStrings.Keys(LauncherLanguage.English).OrderBy(k => k).ToList();
        Assert.Equal(arabic, english);
        foreach (var key in arabic)
        {
            Assert.False(string.IsNullOrWhiteSpace(LauncherStrings.Get(LauncherLanguage.Arabic, key)), key);
            Assert.False(string.IsNullOrWhiteSpace(LauncherStrings.Get(LauncherLanguage.English, key)), key);
        }
    }

    [Fact]
    public void Strings_FormatPlaceholdersMatchBetweenLanguages()
    {
        foreach (var key in LauncherStrings.Keys(LauncherLanguage.Arabic))
        {
            Assert.Equal(
                LauncherStrings.Get(LauncherLanguage.Arabic, key).Contains("{0}"),
                LauncherStrings.Get(LauncherLanguage.English, key).Contains("{0}"));
        }
    }
}
