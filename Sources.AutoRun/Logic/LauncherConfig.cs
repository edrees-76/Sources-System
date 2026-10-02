using System;
using System.IO;
using System.Text;

namespace Sources.AutoRun.Logic;

/// <summary>
/// إعدادات الواجهة من ملف <c>autorun.config</c> بجوار البرنامج: أسطر <c>المفتاح=القيمة</c>،
/// والسطر الذي يبدأ بـ <c>#</c> تعليق. المفتاح الوحيد حالياً: <c>ActivationCode</c>.
/// </summary>
public sealed class LauncherConfig
{
    public const string FileName = "autorun.config";

    /// <summary>رقم تفعيل المنظومة، أو <c>null</c> إن لم يوجد في الملف.</summary>
    public string? ActivationCode { get; }

    public LauncherConfig(string? activationCode)
    {
        ActivationCode = string.IsNullOrWhiteSpace(activationCode) ? null : activationCode!.Trim();
    }

    public static LauncherConfig Parse(string? text)
    {
        string? code = null;
        foreach (var rawLine in (text ?? string.Empty).Split('\n'))
        {
            var line = rawLine.Trim().TrimStart('﻿');
            if (line.Length == 0 || line[0] == '#') continue;

            var separator = line.IndexOf('=');
            if (separator <= 0) continue;

            var key = line.Substring(0, separator).Trim();
            if (string.Equals(key, "ActivationCode", StringComparison.OrdinalIgnoreCase))
                code = line.Substring(separator + 1).Trim();
        }
        return new LauncherConfig(code);
    }

    /// <summary>يقرأ الملف؛ الغياب أو تعذّر القراءة يُرجع إعداداً فارغاً (الواجهة تعرض نصاً بديلاً).</summary>
    public static LauncherConfig Load(string baseDirectory)
    {
        try
        {
            var path = Path.Combine(baseDirectory, FileName);
            return File.Exists(path) ? Parse(File.ReadAllText(path, Encoding.UTF8)) : new LauncherConfig(null);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning("AutoRun: failed to read " + FileName + ": " + ex.Message);
            return new LauncherConfig(null);
        }
    }
}
