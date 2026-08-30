using System;
using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

public interface INeutronSourceService
{
    List<NeutronSource> GetAll();
    List<NeutronSource> GetDeleted();
    NeutronSource? GetById(Guid id);
    NeutronSource? GetByCode(string sourceCode);
    List<NeutronSource> GetByLocation(Guid locationId);
    int GetTotalCount();
    (bool Success, string Message) Create(NeutronSource item);
    (bool Success, string Message) Update(NeutronSource item);
    (bool Success, string Message) Delete(Guid id);
    (bool Success, string Message) Restore(Guid id);
}
