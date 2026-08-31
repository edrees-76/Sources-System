using System;
using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

/// <summary>
/// خدمة إدارة المصادر النيترونية
/// </summary>
public interface INeutronSourceService
{
    /// <summary>جلب جميع المصادر النيترونية النشطة</summary>
    List<NeutronSource> GetAll();

    /// <summary>جلب المصادر النيترونية المحذوفة</summary>
    List<NeutronSource> GetDeleted();

    /// <summary>جلب مصدر نيتروني بالمعرف</summary>
    NeutronSource? GetById(Guid id);

    /// <summary>جلب مصدر نيتروني بالكود</summary>
    NeutronSource? GetByCode(string sourceCode);

    /// <summary>جلب المصادر النيترونية حسب الموقع</summary>
    List<NeutronSource> GetByLocation(Guid locationId);

    /// <summary>عدد المصادر النيترونية الكلي</summary>
    int GetTotalCount();

    /// <summary>إنشاء مصدر نيتروني جديد</summary>
    (bool Success, string Message) Create(NeutronSource item);

    /// <summary>تحديث مصدر نيتروني</summary>
    (bool Success, string Message) Update(NeutronSource item);

    /// <summary>حذف مصدر نيتروني</summary>
    (bool Success, string Message) Delete(Guid id);

    /// <summary>استرجاع مصدر نيتروني محذوف</summary>
    (bool Success, string Message) Restore(Guid id);
}
