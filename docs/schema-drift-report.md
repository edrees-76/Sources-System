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
3. **القيم المعيارية للمصادر النيترونية ومحددات المعامل المكافئ للجرعة المحيطية (`ISO 8529-1:2021` و `ISO 8529-3:2023`):**
   - **المرجع المعتمد:** تم اعتماد أحدث إصدارات المعايير الدولية `ISO 8529-1:2021` (حقول الإشعاع المرجعية) و `ISO 8529-3:2023` (معايرة مقاييس الجرعة والمكافئ الجرعي).
   - **سبب إبقاء `AmbientDoseConversionCoefficient` فارغاً (`NULL`) لتسعة أنواع من عشرة:**
     1. **`Cf-252` (المجرد):** هو النوع الوحيد الذي يحمل قيمة ثابتة ومحددة معيارياً للنوع ككل ($h^*(10) = 385\text{ pSv}\cdot\text{cm}^2$ ومتوسط طاقة $2.13\text{ MeV}$).
     2. **`Am-241/Be`:** في معيار `ISO 8529-3:2023 Table 2` أصبح المعامل خاصية معتمدة على الحجم الفعلي للمصدر وتصميمه الفيزيائي (المصادر الصغيرة: $393\text{ pSv}\cdot\text{cm}^2$ بطاقة $4.17\text{ MeV}$؛ المصادر الكبيرة: $387\text{ pSv}\cdot\text{cm}^2$ بطاقة $4.05\text{ MeV}$؛ بينما القيمة السابقة 391 موسومة كقيمة قديمة). ونظراً لعدم وجود حقل لتمييز حجم الكبسولة على مستوى نوع المصدر العام، تُرِك الحقل فارغاً مع توثيق السبب بدقة.
     3. **`Am-241/B`:** خرج من جدول الإشعاعات المرجعية في طبعة `ISO 8529-1:2021` ونُقل إلى الملحق غير المشمول ولا يتوفر له معامل حالي في `ISO 8529-3:2023`.
     4. **الأنواع السبعة الأخرى:** (`Pu-239/Be`, `Pu-238/Be`, `Am-241/F`, `Am-241/Li`, `Ra-226/Be`, `Sb-124/Be`, `NBS-1`): غير مشمولة بجدول المعاملات المعيارية في `ISO 8529-3:2023` أصلاً.
   - **عدم إضافة `Cf-252` المهدَّأ بالماء الثقيل (`D2O-moderated Cf-252`) كنوع مصدر مستقل:**
     - لأن معامله مشروط بمسافة قياس محددة ($100\text{ cm}$) وتشكيل هندسي للمجموعة وتغطيات الكادميوم، فهو يمثل "تهيئة حقل إشعاعي" (Field Configuration) وليس صنف كبسولة مصدر مستقلة.
   - **مصدر أعمار النصف المسجّلة واتساقها:**
     - أعمار النصف المسجّلة (`Cf-252` بـ 2,645 سنة و`Am-241` بـ 432,2 سنة) مأخوذة من مكتبة النظائر الداخلية للمنظومة لا من `ISO 8529-1:2021` الذي يذكر 2,647 و432,6. الفرق أقل من 0,1% وأثره على حساب الاضمحلال أقل من 0,2% على مدى عشر سنوات — أي أدنى بكثير من عدم يقين معايرة معدل الانبعاث نفسه. تُركت كما هي عمداً حفاظاً على اتساق مكتبة النظائر، ويُعاد النظر فيها إن استدعت جهة رقابية ذلك.

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

### الجولة 97-أ / 97-أ-2: حقول شهادة المعايرة والمعايير النيترونية الحديثة
- فصل معدل الانبعاث المعاير `CalibratedEmissionRate` وإضافة حقول الشهادة (`EmissionCalibrationDate`, `CalibrationReference`, `AnisotropyFactor`) مع الحفاظ على `CalibrationDate`.
- تحديث `NeutronSourceType` بإضافة `MeanNeutronEnergyMeV`, `AmbientDoseConversionCoefficient`, `StandardReference` وحذف `TypicalNeutronYield`.
- الترحيل `20260901184302_AddNeutronCalibrationAndDecayFields` ونقل تواريخ المعايرة تلقائياً.
- مواءمة بيانات البذر مع `ISO 8529-1:2021` و `ISO 8529-3:2023`.

