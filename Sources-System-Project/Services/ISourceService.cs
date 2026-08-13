using System;
using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

public interface ISourceService
{
    List<Source> GetAllSources();
    Source? GetSourceById(Guid id);
    (bool Success, string Message) CreateSource(Source source, List<SourceIsotope>? isotopes = null);
    (bool Success, string Message) UpdateSource(Source source, List<SourceIsotope>? isotopes = null);
    (bool Success, string Message) DeleteSource(Guid id);
    void UpdateAllCurrentActivities();
    int GetTotalSourcesCount();
    int GetActiveSourcesCount();
    List<Source> GetLowActivitySources(double threshold);
}
