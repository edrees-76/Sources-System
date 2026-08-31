using System;
using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

/// <summary>
/// خدمة إدارة أنواع المصادر النيترونية
/// </summary>
public interface INeutronSourceTypeService
{
    /// <summary>جلب جميع أنواع المصادر النيترونية النشطة</summary>
    List<NeutronSourceType> GetAll();

    /// <summary>جلب أنواع المصادر النيترونية المحذوفة</summary>
    List<NeutronSourceType> GetDeleted();

    /// <summary>جلب نوع مصدر نيتروني بالمعرف</summary>
    NeutronSourceType? GetById(Guid id);

    /// <summary>إنشاء نوع مصدر نيتروني جديد</summary>
    (bool Success, string Message) Create(NeutronSourceType item);

    /// <summary>تحديث نوع مصدر نيتروني</summary>
    (bool Success, string Message) Update(NeutronSourceType item);

    /// <summary>حذف نوع مصدر نيتروني</summary>
    (bool Success, string Message) Delete(Guid id);

    /// <summary>استرجاع نوع مصدر نيتروني محذوف</summary>
    (bool Success, string Message) Restore(Guid id);
}
