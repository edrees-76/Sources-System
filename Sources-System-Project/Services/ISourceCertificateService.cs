using System;
using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

public interface ISourceCertificateService
{
    List<SourceCertificate> GetCertificates(Guid sourceId, string sourceType);
    SourceCertificate AttachCertificate(Guid sourceId, string sourceType, string filePath, string attachedBy);
    bool DeleteCertificate(Guid certificateId, string deletedBy);
    bool DownloadCertificate(Guid certificateId, string destinationPath);
    string GetCertificatesFolder();
    void DeleteAllCertificateFiles();
}
