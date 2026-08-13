using System;
using System.Collections.Generic;

namespace Sources.Services;

public interface IBackupService
{
    (bool Success, string Message, string? BackupPath) CreateBackup();
    (bool Success, string Message, string? BackupPath) CreateBackup(string customPath);
    (bool Success, string Message) RestoreBackup(string backupFilePath);
    List<BackupInfo> GetBackups();
}
