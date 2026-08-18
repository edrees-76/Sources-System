using System.Threading.Tasks;

namespace Sources.Services;

public interface ISystemResetService
{
    Task<(bool Success, string Message, string? BackupPath)> ResetSystemAsync(string executedByUsername);
}
