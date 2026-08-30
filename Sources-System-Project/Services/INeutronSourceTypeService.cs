using System;
using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

public interface INeutronSourceTypeService
{
    List<NeutronSourceType> GetAll();
    List<NeutronSourceType> GetDeleted();
    NeutronSourceType? GetById(Guid id);
    (bool Success, string Message) Create(NeutronSourceType item);
    (bool Success, string Message) Update(NeutronSourceType item);
    (bool Success, string Message) Delete(Guid id);
    (bool Success, string Message) Restore(Guid id);
}
