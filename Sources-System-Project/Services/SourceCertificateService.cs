using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;

namespace Sources.Services;

public class SourceCertificateService : ISourceCertificateService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditService _auditService;
    private readonly string _certificatesFolder;

    public SourceCertificateService(
        IDbContextFactory<AppDbContext> dbFactory,
        IAuditService auditService,
        string? customCertificatesFolder = null)
    {
        _dbFactory = dbFactory;
        _auditService = auditService;
        _certificatesFolder = !string.IsNullOrEmpty(customCertificatesFolder)
            ? customCertificatesFolder
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Certificates");

        EnsureDirectoryExists();
    }

    public string GetCertificatesFolder()
    {
        EnsureDirectoryExists();
        return _certificatesFolder;
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_certificatesFolder))
        {
            Directory.CreateDirectory(_certificatesFolder);
        }
    }

    public List<SourceCertificate> GetCertificates(Guid sourceId, string sourceType)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.SourceCertificates
            .AsNoTracking()
            .Where(c => c.SourceId == sourceId && c.SourceType == sourceType)
            .OrderByDescending(c => c.AttachedAt)
            .ToList();
    }

    public SourceCertificate AttachCertificate(Guid sourceId, string sourceType, string filePath, string attachedBy)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("الملف المطلوب إرفاقه غير موجود", filePath);

        EnsureDirectoryExists();

        var originalName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var destinationPath = Path.Combine(_certificatesFolder, storedName);

        File.Copy(filePath, destinationPath, overwrite: true);

        var cert = new SourceCertificate
        {
            Id = Guid.NewGuid(),
            SourceId = sourceId,
            SourceType = string.IsNullOrWhiteSpace(sourceType) ? "Standard" : sourceType,
            StoredFileName = storedName,
            OriginalFileName = originalName,
            AttachedAt = DateTime.Now,
            AttachedBy = !string.IsNullOrWhiteSpace(attachedBy) ? attachedBy : "غير معروف"
        };

        using var db = _dbFactory.CreateDbContext();
        db.SourceCertificates.Add(cert);
        db.SaveChanges();

        _auditService.Log("Create", "SourceCertificates", cert.Id,
            $"إرفاق شهادة '{originalName}' للمصدر {sourceId}");

        return cert;
    }

    public bool DeleteCertificate(Guid certificateId, string deletedBy)
    {
        using var db = _dbFactory.CreateDbContext();
        var cert = db.SourceCertificates.FirstOrDefault(c => c.Id == certificateId);
        if (cert == null)
            return false;

        try
        {
            var filePath = Path.Combine(_certificatesFolder, cert.StoredFileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogWarning($"تعذر حذف ملف الشهادة الفعلي من القرص: {ex.Message}");
        }

        db.SourceCertificates.Remove(cert);
        db.SaveChanges();

        _auditService.Log("Delete", "SourceCertificates", cert.Id,
            $"حذف شهادة '{cert.OriginalFileName}' من المصدر {cert.SourceId}");

        return true;
    }

    public bool DownloadCertificate(Guid certificateId, string destinationPath)
    {
        using var db = _dbFactory.CreateDbContext();
        var cert = db.SourceCertificates.AsNoTracking().FirstOrDefault(c => c.Id == certificateId);
        if (cert == null)
            return false;

        var sourcePath = Path.Combine(_certificatesFolder, cert.StoredFileName);
        if (!File.Exists(sourcePath))
            return false;

        var targetDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
        return true;
    }

    public void DeleteAllCertificateFiles()
    {
        try
        {
            var folder = GetCertificatesFolder();
            if (Directory.Exists(folder))
            {
                var files = Directory.GetFiles(folder);
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogWarning($"تعذر حذف ملف الشهادة '{file}' أثناء تنظيف الملفات: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogWarning($"تعذر الوصول لمجلد الشهادات لحذف الملفات: {ex.Message}");
        }
    }
}
