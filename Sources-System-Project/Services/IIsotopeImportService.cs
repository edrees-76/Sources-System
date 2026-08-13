using System.Threading.Tasks;

namespace Sources.Services;

public interface IIsotopeImportService
{
    Task<(int imported, int updated)> ImportIsotopesAsync();
}
