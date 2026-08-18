using System;
using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

public interface ILocationService
{
    List<Location> GetAll();
    Location? GetById(Guid id);
    (bool Success, string Message) Create(Location item);
    (bool Success, string Message) Update(Location item);
    (bool Success, string Message) Delete(Guid id);
    int GetCount();
    List<Source> GetSourcesLinkedToLocation(Guid locationId);
}
