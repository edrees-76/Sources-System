using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sources.Services;

/// <summary>نتيجة مسح مجلدات النسخ الاحتياطي.</summary>
public enum BackupScanOutcome
{
    /// <summary>وُجدت نسخة واحدة على الأقل.</summary>
    Found,
    /// <summary>المسح نجح ولا توجد نسخ.</summary>
    NoneFound,
    /// <summary>تعذّر مسح مجلد واحد على الأقل ولم يُعثر على نسخة — الحالة مجهولة.</summary>
    ScanFailed
}

/// <summary>
/// يفصل «لا توجد نسخ» عن «تعذّرت القراءة». الخلط بينهما كان يجعل النسخ التلقائي
/// يُطلق نسخة كاملة كل دورة إلى الأبد حين يتعذّر الوصول إلى مجلد النسخ.
/// </summary>
public static class BackupFolderScanner
{
    /// <summary>
    /// يمسح المجلدات ويُرجع أحدث تاريخ نسخة. لا يرمي أبداً.
    /// <paramref name="backupTimestampsReader"/> فاصل للاختبار؛ الافتراضي يقرأ من القرص.
    /// </summary>
    public static (BackupScanOutcome Outcome, DateTime? Latest) ScanLatest(
        IEnumerable<string> folders,
        Func<string, IEnumerable<DateTime>>? backupTimestampsReader = null)
    {
        var reader = backupTimestampsReader ?? ReadBackupTimestamps;

        DateTime? latest = null;
        bool anyFolderFailed = false;

        foreach (var folder in folders.Distinct())
        {
            try
            {
                foreach (var stamp in reader(folder))
                {
                    if (!latest.HasValue || stamp > latest.Value) latest = stamp;
                }
            }
            catch
            {
                // الفشل لكل مجلد على حدة: مجلد متعذّر لا يُبطل نتيجة مجلد نجح.
                // الاستثناء لا يُبتلع — يُرفع أثره في BackupScanOutcome.ScanFailed
                // ويُسجَّل تحذيراً في المستدعي.
                anyFolderFailed = true;
            }
        }

        if (latest.HasValue) return (BackupScanOutcome.Found, latest);
        return (anyFolderFailed ? BackupScanOutcome.ScanFailed : BackupScanOutcome.NoneFound, null);
    }

    private static IEnumerable<DateTime> ReadBackupTimestamps(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<DateTime>();

        return Directory.GetFiles(folder, "*.*")
            .Where(f => (f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                        && Path.GetFileName(f).Contains("_backup_", StringComparison.OrdinalIgnoreCase))
            .Select(f => new FileInfo(f).CreationTime)
            .ToList();
    }
}
