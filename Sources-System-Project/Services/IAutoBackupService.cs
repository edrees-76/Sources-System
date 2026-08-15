using System;

namespace Sources.Services;

public interface IAutoBackupService
{
    void Start();
    void Stop();
    void TriggerImmediateCheck();
    event EventHandler? BackupCompleted;
}
