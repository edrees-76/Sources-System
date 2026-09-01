# تقرير انحراف المخطط (Schema Drift Report)
**تاريخ التوثيق:** الجولة 89  
**النطاق:** توثيق الفروقات بين المخطط المُنشأ عبر SQL الخام في `AppDbContext.MigrateSchema()` وما يصفه `OnModelCreating` وملفات `Migrations/` و `AppDbContextModelSnapshot.cs`.

---

## 1. ملخص تنفيذي (Executive Summary)

يعتمد نظام `Sources` على آليتين لتجهيز وصيانة قاعدة بيانات SQLite:
1. **الآلية البرمجية المباشرة (Raw SQL):** تُنفذ داخل الدالة `AppDbContext.MigrateSchema()` عند تشغيل التطبيق (`InitializeDatabase`).
2. **الآلية التصريحية في EF Core:** الموصوفة في `OnModelCreating` وملفات الترحيل `Migrations/` ومخطط النموذج `AppDbContextModelSnapshot.cs`.

أظهر الفحص التفصيلي وجود انحرافات جوهرية (Schema Drift) بين الآليتين تشمل:
- غياب كامل لجدول حديث (`SourceCertificates`) من ترحيلات ومخطط EF Core.
- انحرافات في قيود الفهرسة الفريدة والمفلترة (`UNIQUE ... WHERE IsDeleted = 0`) بين المصادر النيترونية والأنواع المرجعية مقارنة بنموذج EF.
- غياب العديد من فهارس الأداء الخاصة بالحذف الناعم (`IsDeleted`) والحالات والتواريخ من نموذج EF Core رغم إنشائها في SQL الخام.
- عدم تصفية الفهرس الفريد لكود المصدر العادي (`Sources.SourceCode`) عند الحذف الناعم في نموذج EF.

---

## 2. جدول مقارنة الانحرافات التفصيلية

| # | اسم الجدول (Table) | العنصر / الحقل | الحالة في SQL الخام (`MigrateSchema`) | الحالة في EF Core (`OnModelCreating` / `Migrations` / `ModelSnapshot`) | الأثر السلوكي المحتمل (Behavioral Impact) |
|---|---|---|---|---|---|
| **1** | `NeutronSources` | `IX_NeutronSources_SourceCode` | فهرس فريد مفلتر: `CREATE UNIQUE INDEX ... WHERE IsDeleted = 0` | فهرس عادي غير فريد وبلا فلتر: `entity.HasIndex(n => n.SourceCode)` | في بيئات الاختبار أو قواعد البيانات المُنشأة عبر EF Migration فقط، لا تمنع قاعدة البيانات تكرار كود المصدر النيتروني النشط، بينما في قواعد البيانات الفعلية يمنع الـ DB التكرار بقيد فريد. |
| **2** | `NeutronSourceTypes` | `IX_NeutronSourceTypes_Code` | فهرس فريد مفلتر: `CREATE UNIQUE INDEX ... WHERE IsDeleted = 0` | فهرس عادي غير فريد وبلا فلتر: `entity.HasIndex(t => t.Code)` | نفس الأثر: تكرار الكود لنوع المصدر مسموح في مخطط EF وممنوع بقيد صارم في SQL الخام. |
| **3** | `SourceCertificates` | الجدول والفهارس بالكامل | جدول كامل يُنشأ بـ `CREATE TABLE IF NOT EXISTS SourceCertificates` مع فهرسين `IX_SourceCertificates_SourceId` و `IX_SourceCertificates_SourceType` | **غير موجود نهائياً** في مجلد `Migrations/` أو `AppDbContextModelSnapshot.cs` (معرّف فقط كـ `DbSet` في `AppDbContext`) | لو شُغّلت أداة `dotnet ef database update` لإنشاء قاعدة بيانات جديدة من الصفر دون المرور بـ `MigrateSchema()`، سيفشل التطبيق فوراً بخطأ `SQLite Error 1: no such table: SourceCertificates` عند محاولة حفظ أو قراءة أي شهادة. |
| **4** | `Sources` | `IX_Sources_SourceCode` | لا يتم إنشاء فهرس مخصص في `MigrateSchema`، بل يعتمد على EF | فهرس فريد **غير مفلتر**: `entity.HasIndex(s => s.SourceCode).IsUnique()` | إذا حُذف مصدر بالـ Soft Delete (`IsDeleted = 1`)، فإن محاولة إضافة مصدر جديد بنفس الكود تفشل بـ Unique Constraint Violation في EF لأن الفهرس غير مفلتر بـ `WHERE IsDeleted = 0` (عكس باقي الجداول). |
| **5** | `Sources` | فهارس الأداء (`Status`, `CalibrationDate`, `IsDeleted`, `SerialNumber`, `IsSealed`) | تُنشأ 5 فهارس منفصلة في SQL الخام: `IX_Sources_Status`, `IX_Sources_CalibrationDate`, `IX_Sources_IsDeleted`, `IX_Sources_SerialNumber`, `IX_Sources_IsSealed` | **غير معرّفة** في `OnModelCreating` ولا تظهر في `AppDbContextModelSnapshot` | في حال عدم تنفيذ SQL الخام، ستعاني استعلامات الفلترة حسب الحالة وتواريخ المعايرة والحذف الناعم والمصادر المختومة من بطء في الأداء (Full Table Scans). |
| **6** | `Locations` | `IX_Locations_IsDeleted` | يُنشأ فهرس أداء: `CREATE INDEX IF NOT EXISTS IX_Locations_IsDeleted ON Locations(IsDeleted)` | غير معرّف في `OnModelCreating` أو `ModelSnapshot` (يوجد فقط `IX_Locations_LocationName` المفلتر) | تراجع أداء استعلامات تصفية المواقع النشطة والمحذوفة في البيئات التي تعتمد فقط على ترحيلات EF Core. |
| **7** | `Users` | `IX_Users_IsDeleted` | يُنشأ فهرس أداء: `CREATE INDEX IF NOT EXISTS IX_Users_IsDeleted ON Users(IsDeleted)` | غير معرّف في `OnModelCreating` أو `ModelSnapshot` | تراجع أداء استعلامات المستخدمين النشطين/المحذوفين. |
| **8** | `Radioisotopes` | `IX_Radioisotopes_IsDeleted` | يُنشأ فهرس أداء: `CREATE INDEX IF NOT EXISTS IX_Radioisotopes_IsDeleted ON Radioisotopes(IsDeleted)` | غير معرّف في `OnModelCreating` أو `ModelSnapshot` | تراجع أداء استعلامات النظائر المشعة عند كثرة السجلات. |
| **9** | `AuditLogs` | `IX_AuditLogs_ActionDate` | يُنشأ فهرس أداء: `CREATE INDEX IF NOT EXISTS IX_AuditLogs_ActionDate ON AuditLogs(ActionDate)` | غير معرّف في `OnModelCreating` أو `ModelSnapshot` | عند تضخم سجل التدقيق لعشرات الآلاف من السجلات، ستصبح استعلامات التقارير والبحث بالتواريخ بطيئة جداً إذا لم يُنشأ الفهرس عبر SQL الخام. |
| **10** | `AlertNotifications` | `IX_AlertNotifications_IsRead` | يُنشأ فهرس أداء: `CREATE INDEX IF NOT EXISTS IX_AlertNotifications_IsRead ON AlertNotifications(IsRead)` | غير معرّف في `OnModelCreating` أو `ModelSnapshot` | استعلام جلب التنبيهات غير المقروءة سيمسح الجدول كاملاً بدون الفهرس. |
| **11** | `BorrowRequests` | `IX_BorrowRequests_Status` | يُنشأ فهرس أداء: `CREATE INDEX IF NOT EXISTS IX_BorrowRequests_Status ON BorrowRequests(Status)` | غير معرّف في `OnModelCreating` أو `ModelSnapshot` | استعلامات تصنيف طلبات الاستعارة (معلقة، متأخرة، مسلّمة) تصبح أبطأ. |
| **12** | `BorrowRequests` | `IX_BorrowRequests_SourceId` | يُنشأ فهرسان: فهرس عادي `IX_BorrowRequests_SourceId` وفهرس فريد مفلتر `IX_BorrowRequests_SourceId_Active` (`WHERE Status IN ('Delivered', 'Overdue')`) | يُعرّف فهرس واحد فريد مفلتر باسم `IX_BorrowRequests_SourceId` | في نموذج EF، يحل الفهرس المفلتر محل فهرس المفتاح الخارجي العادي، بينما في SQL الخام يوجد فهرسان مما قد يسبب ازدواجية طفيفة في التخزين لكنه يضمن تسريع الاستعلامات العادية على المفتاح الخارجي. |
| **13** | الكيانات الستة (`Sources`, `Locations`, `Radioisotopes`, `BorrowRequests`, `NeutronSources`, `NeutronSourceTypes`) | نوع العمود `AddedBy` والعلاقة `AddedByUser` | **تم الإغلاق والتوحيد في الجولة 95** على `Guid?` مع قيد مفتاح خارجي `SetNull` وفهرس لكل جدول وخاصية `[NotMapped] AddedByName`. | **مطابق تماماً** في `OnModelCreating` وترحيل `20260901133004_UnifyAddedByToGuid` و `AppDbContextModelSnapshot`. | تم القضاء على الانحراف بالكامل، وتوحيد منطق تسجيل المستخدم المنشئ مع معالجة البيانات القائمة وحماية التوافق. |

---

## 3. التحديثات المنجزة (Resolved Items)

### الجولة 95: توحيد `AddedBy` إلى `Guid?` مع قيد المفتاح الخارجي
- **الكيانات المشمولة:** `Source`, `Location`, `Radioisotope`, `BorrowRequest`, `NeutronSource`, `NeutronSourceType`.
- **النمط الموحد:**
  - `public Guid? AddedBy { get; set; }`
  - `[ForeignKey(nameof(AddedBy))] public User? AddedByUser { get; set; }`
  - `[NotMapped] public string AddedByName => AddedByUser?.FullName ?? "غير معروف";`
- **التهيئة والترحيل:** إضافة علاقة `AddedByUser` مع `IsRequired(false)` و `OnDelete(DeleteBehavior.SetNull)` وفهارس `IX_*_AddedBy`. تم إنشاء الترحيل `20260901133004_UnifyAddedByToGuid` وترحيل البيانات القائمة برمجياً.
- **استثناء `SourceCertificate.AttachedBy`:** يظل `string?` لكونه يعبر عن نص الوصف/جهة الإرفاق وليس المستخدم المنشئ للنظام.
- **تشديد فحص التوافق في `BackupService`:** مقارنة ترحيلات `__EFMigrationsHistory` في النسخة الاحتياطية بالمخطط المعتمد للمنظومة لرفض النسخ المستقبلية المجهولة أو القديمة الخالية من سجل الترحيلات بأمان.
- **تغيير سلوك مرئي في `DeletionsViewModel`:** كان حقل `DeletedByName` يعرض اسم مُضيف السجل كقيمة احتياطية حين يكون القائم بالحذف مجهولاً، في `Source` و `Location` و `Radioisotope`. أُزيلت هذه الاحتياطية وصار يُعرض `"-"`، لأن عرض اسم المُضيف تحت لافتة "حُذف بواسطة" إسناد كاذب في سجل التدقيق.

---

## 4. التوصيات الفنية للجولات القادمة (Technical Recommendations)

عند التخطيط لمعالجة باقي الانحرافات في جولات لاحقة، يُوصى بما يلي:
1. **تحديث `OnModelCreating`:** لمطابقة الفهارس الفريدة المفلترة لـ `NeutronSources.SourceCode` و `NeutronSourceTypes.Code` وفهارس الأداء.
2. **إضافة تهيئة `SourceCertificates` إلى `OnModelCreating`:** وتوليد Migration رسمي لها لضمان تزامن `AppDbContextModelSnapshot`.
3. **الجولة 96 القادمة:** استكمال تحديث الـ ViewModels والـ XAML لعرض `AddedByName` في باقي الواجهات والنوافذ.
