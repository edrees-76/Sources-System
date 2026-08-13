using System;
using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

public interface IRadioisotopeService
{
    List<Radioisotope> GetAll();
    Radioisotope? GetById(Guid id);
    (bool Success, string Message) Create(Radioisotope item);
    (bool Success, string Message) Update(Radioisotope item);
    (bool Success, string Message) Delete(Guid id);
}
