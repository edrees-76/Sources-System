# تقرير وقرارات المخطط (Database Schema & Decision Record)
**تاريخ التحديث:** الجولة 96 (محدّث من الجولة 89 والجولة 95)  
**المرجع الأساسي:** `OnModelCreating` في `AppDbContext.cs`، وملفات `Migrations/`، ومخطط النموذج `AppDbContextModelSnapshot.cs`.

---

## 1. ملخص تنفيذي وسياق التوحيد

في الجولات السابقة (حتى الجولة 89)، كان النظام يعتمد على آليتين:
1. **الآلية البرمجية المباشرة (Raw SQL):** داخل الدالة `MigrateSchema()` في `AppDbContext`.
2. **الآلية التصريحية في EF Core:** الموصوفة في `OnModelCreating` وترحيلات EF Core.

**في الجولة 90 أُلغيت دالة `MigrateSchema()` بالكامل**، ونُقلت كافة تعريفات الجداول والفهارس والعلاقات إلى `OnModelCreating`، وأصبح المخطط موحداً بنسبة 100% تحت إدارة **EF Core Migrations** وحدها.
وبناءً عليه، لم يعد هذا التقرير سجلاً لانحراف بين آليتين متنافستين، بل تحوّل إلى **سجل رسمي لقرارات المخطط والفهارس وتوثيق حالتها**.

---

## 2. سجل بنود المخطط والفهارس والتحقق منها

| # | اسم الجدول (Table) | العنصر / الحقل | الحالة في المخطط المعتمد (`OnModelCreating` / `Migrations` / `ModelSnapshot`) | حالة البند | سطر الإثبات في الكود |
|---|---|---|---|---|---|
| **1** | `NeutronSources` | `IX_NeutronSources_SourceCode` | فهرس فريد مفلتر: `HasDatabaseName("IX_NeutronSources_SourceCode").HasFilter("IsDeleted = 0").IsUnique()` | **مغلق (مُطبّق)** | `Data/AppDbContext.cs`: الأسطر 622-625 |
| **2** | `NeutronSourceTypes` | `IX_NeutronSourceTypes_Code` | فهرس فريد مفلتر: `HasDatabaseName("IX_NeutronSourceTypes_Code").HasFilter("IsDeleted = 0").IsUnique()` | **مغلق (مُطبّق)** | `Data/AppDbContext.cs`: الأسطر 578-581 |
| **3** | `SourceCertificates` | الجدول والفهارس بالكامل | جدول كامل معرّف بـ `DbSet<SourceCertificate>` ومفهرس على `SourceId` و `SourceType`. | **مغلق (مُطبّق)** | 1) التهيئة: `Data/AppDbContext.cs` (الأسطر 633-637)<br>2) الترحيل: `Migrations/20260901112320_InitialSchema.cs` (الأسطر 56-71 و 684-693)<br>3) المخطط: `Migrations/AppDbContextModelSnapshot.cs` (الأسطر 720-759) |
| **5** | `Sources` | فهارس الأداء (`Status`, `CalibrationDate`, `IsDeleted`, `SerialNumber`, `IsSealed`) | 5 فهارس منفصلة لتسريع الفلترة والحذف الناعم والبحث. | **مغلق (مُطبّق)** | `Data/AppDbContext.cs`: الأسطر 366-370 |
| **6** | `Locations` | `IX_Locations_IsDeleted` | فهرس أداء على الحذف الناعم: `entity.HasIndex(l => l.IsDeleted)`. | **مغلق (مُطبّق)** | `Data/AppDbContext.cs`: السطر 392 |
| **7** | `Users` | `IX_Users_IsDeleted` | فهرس أداء على الحذف الناعم: `entity.HasIndex(u => u.IsDeleted)`. | **مغلق (مُطبّق)** | `Data/AppDbContext.cs`: السطر 474 |
| **8** | `Radioisotopes` | `IX_Radioisotopes_IsDeleted` | فهرس أداء على الحذف الناعم: `entity.HasIndex(r => r.IsDeleted)`. | **مغلق (مُطبّق)** | `Data/AppDbContext.cs`: السطر 410 |
| **9** | `AuditLogs` | `IX_AuditLogs_ActionDate` | فهرس أداء على تاريخ العملية لتسريع التقارير: `entity.HasIndex(a => a.ActionDate)`. | **مغلق (مُطبّق)** | `Data/AppDbContext.cs`: السطر 485 |
| **10** | `AlertNotifications` | `IX_AlertNotifications_IsRead` | فهرس أداء على حالة القراءة: `entity.HasIndex(n => n.IsRead)`. | **مغلق (مُطبّق)** | `Data/AppDbContext.cs`: السطر 497 |
| **11** | `BorrowRequests` | `IX_BorrowRequests_Status` | فهرس أداء على حالة الاستعارة: `entity.HasIndex(b => b.Status)`. | **مغلق (مُطبّق)** | `Data/AppDbContext.cs`: السطر 453 |
| **12** | `BorrowRequests` | `IX_BorrowRequests_SourceId` | فهرسان: فهرس مفتاح خارجي عادي + فهرس فريد مفلتر `IX_BorrowRequests_SourceId_Active` للطلبات النشطة (`Status IN ('Delivered', 'Overdue')`). | **مغلق (مُطبّق)** | `Data/AppDbContext.cs`: الأسطر 447-451 |
| **13** | الكيانات الستة (`Sources`, `Locations`, `Radioisotopes`, `BorrowRequests`, `NeutronSources`, `NeutronSourceTypes`) | نوع العمود `AddedBy` والعلاقة `AddedByUser` | توحيد كامل على `Guid?` مع قيد مفتاح خارجي `SetNull` وفهارس `IX_*_AddedBy` وخاصية `[NotMapped] AddedByName`. | **مغلق (مُطبّق)** | `Data/AppDbContext.cs` والترحيل `20260901133004_UnifyAddedByToGuid` |

---

## 3. قرارات تصميم مقصودة (Intentional Design Decisions)

1. **فهرس كود المصدر العادي `Sources.SourceCode` فريد غير مفلتر عمداً:**
   - **التعريف:** `entity.HasIndex(s => s.SourceCode).IsUnique();` (`AppDbContext.cs` السطر 363).
   - **التعليل الهندسي والرقابي:** كود المصدر معرّف دائم لجسم مشع خاضع للرقابة الدولية والمحلية ولا يُعاد استخدامه أبداً بعد الحذف الناعم حفاظاً على سلامة سجل الحيازة والتدقيق وتجنب أي خلط بين جسمين مشعين.
2. **بقاء الأنواع المرجعية للمصادر النيترونية `NeutronSourceTypes` عند إعادة ضبط المصنع:**
   - جدول `NeutronSourceTypes` يمثل بيانات مرجعية أساسية وثوابت فيزيائية تنجو من إعادة ضبط المصنع (`SystemResetService`) لضمان جاهزية النظام التشغيلية بعد التهيئة.

---

## 4. التحديثات المنجزة

### الجولة 95: توحيد `AddedBy` إلى `Guid?` مع قيد المفتاح الخارجي
- توحيد الكيانات الستة بنمط `AddedByUser` + `AddedByName`.
- إضافة حراس وجود المستخدم في دوال الإنشاء الست.
- الترحيل `20260901133004_UnifyAddedByToGuid` مع ترحيل البيانات السابقة وعكسية كاملة في `Down()`.
- تشديد فحص التوافق في `BackupService`.

### الجولة 96: تصحيح التوثيق وإغلاق الإسناد الكاذب والبنود الصغيرة
- تصحيح هذا التقرير ليعتمد على `OnModelCreating` وترحيلات EF Core بعد حذف `MigrateSchema()` في الجولة 90.
- إغلاق الإسناد الكاذب في `SourceDetailsViewModel` و `NeutronSourceDetailsViewModel` واستبداله بـ `"غير معروف"` مع `LoggerService.LogWarning`.
- تحديث `NeutronSourceDetailsViewModel` و `NeutronSourceDetailsWindow.xaml` لربط `AddedByName` مباشرة من النموذج.
- إضافة اختبار شامل لمسار التراجع `Down()` للترحيل `20260901133004_UnifyAddedByToGuid`.
- تنظيف استدعاء فحص الترحيلات في `RestoreBackup` لتفادي إنشاء مجلد LocalAppData كأثر جانبي.
