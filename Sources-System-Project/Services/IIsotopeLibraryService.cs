using System.Collections.Generic;
using System.Threading.Tasks;
using Sources.Models;

namespace Sources.Services;

public interface IIsotopeLibraryService
{
    Task<IReadOnlyList<IsotopeReferenceEntry>> GetAllEntriesAsync();
    Task<IReadOnlyList<IsotopeReferenceEntry>> SearchAsync(string query);
    bool OpenReferencePdf(int pageNumber = 0);
    string GetReferencePdfPath();
    bool OpenIcrpPdf();
    string GetIcrpPdfPath();
}
