# منظومة مصادر — لوحة جاهزية النشر

**آخر تحديث:** 9 سبتمبر 2026
**حالة المستودع:** الجولة 138 مدموجة على `main` (PR #31، commit الدمج
`4a756185177ebaf33d1d3ba9657ebcb4c3b23ffc`، ثانية ضمن طبقة `ViewModels` من ب5): تغليف كل النصوص
العربية المتبقية بلا غلاف في `NeutronSourceTypesViewModel.cs` عبر `TranslationHelper`، 8 مفاتيح
رسالة جديد وإعادة استخدام صريحة لثلاثة مفاتيح قائمة (`TitleWarning`، `AlertError`،
`AlertConfirmation`) دون تكرارها، مع انحرافين موثَّقين: اسم بديل `MsgErrNeutronReferenceTypeNotFound`
لتفادي تعارض مع مفتاح قائم بنفس الاسم يخدم ملفاً آخر، ومفتاح منفصل `MsgErrNeutronTypeHalfLifePositive`
لعدم تطابق النص حرفياً مع `MsgErrHalfLifeMustBePositive` القائم · 1146 اختباراً محلياً (Debug) / 1144
محلياً (Release) للجولة 138 · تحذيرات بناء مسبقة بلا علاقة بهذه الجولة (CS8604 في
`LoginWindow.xaml.cs`/`ViewInstantiationTests.cs`) و0 أخطاء

> لوحة حالة حيّة تُحدَّث وتُصحَّح مع كل جولة. السجل التاريخي للجولات في `session-summary.md` ولا يُعدَّل.

---

## 1. سياق النشر — قرارات محسومة

| البند | القرار |
|---|---|
| عدد الأجهزة | عدة أجهزة، **قاعدة بيانات مستقلة لكل جهاز** |
| البيانات الأولية | قاعدة فارغة تماماً، إدخال يدوي من الصفر |
| طريقة التوصيل | برنامج تثبيت يُبنى بعد اكتمال التطوير |
| الفترة التجريبية | **لا توجد** — تثبيت مباشر على كل الأجهزة دفعة واحدة |
| الموعد | لا يوجد — الجودة أولاً |
| الترجمة الإنجليزية | ميزة معلَنة يجب أن تعمل بالكامل |
| الدليل | شاشة داخل البرنامج **و** ملف PDF |
| النسخ الاحتياطي | مُفعَّل افتراضياً + سؤال عن مجلد الحفظ أول مرة |
| التقارير الرقابية | الحالية تكفي — لا عمل مطلوب |
| نظام التشغيل | ويندوز 10 و11 فقط. **.NET 8 لا يدعم ويندوز 7 ولا 8.1** |

**أثر غياب الفترة التجريبية:** أي عطب يظهر عند مستخدم يظهر عند الجميع في اليوم نفسه.
**أثر القاعدة الفارغة:** لا مستخدمين بهاش SHA256 قديم، فعيب `PasswordHelper` لم يعد مانع نشر.

---

## 2. موانع النشر — بترتيب التنفيذ

### ☑ ب1 — قيم `double` غير المنتهية في مدخلات المنظومة
`NaN` و `Infinity` تعبران الحارس `<= 0` لأن `NaN <= 0` تساوي `false`. تُحفظ في قاعدة البيانات وتدخل الحسابات فيخرج رقم فاسد يُعرض ويدخل تقريراً.
النطاق: `SourcesViewModel` (الجولة 109) + إغلاق بقية المسارات ونقل الحماية الصريحة (`double.IsFinite`) لطبقة الخدمات الخمس (`RadioisotopeService`, `NeutronSourceService`, `SourceService`, `LeakTestService`, `NeutronSourceTypeService`) ومعالجات الواجهات (الجولة 110).
يُغلق قبل ب4 حتى لا ترث الحقول الجديدة العيب. **أُنجز بالكامل في الجولتين 109 و 110.**

### ☑ ب2 — إنفاذ صلاحيات الأدوار في طبقة الخدمات
مُتحقَّق منه (قبل الجولة 111): كان `SourceService` لا يسأل عن الدور في أي دالة؛ `CurrentUser` يُستعمل لتسجيل الفاعل فقط. الحماية في الواجهة وحدها.
القرار: حارس مركزي في العمليات المدمّرة والحساسة فقط — الحذف والاسترجاع وإدارة المستخدمين والأدوار. لا مراجعة شاملة.
يُضم إليه: إصلاح `Roles.RemoveRange` الذي يحذف عند كل إقلاع أي دور غير «مدير النظام» و«مستخدم».
يسبقه: تأكيد أن `LocationService` و `UserService` و `BorrowService` على النمط نفسه. **أُنجز في الجولة 111.** حارس في 17 موضعاً: سبع دوال إدارية في `UserService` بـ `RequireAdmin`، وعشر دوال حذف واسترجاع في خمس خدمات بـ `RequireEditor`. وسُدَّت ثغرة ترقية الامتياز في `ResetPassword`. الإنشاء والتعديل غير محروسين بعد.

### ☑ ب3 — جودة الاختبارات المكتوبة حديثاً
ربط توكيدات `ClipboardFeedbackTests` بثوابت `ClipboardCopyHelper` وفك اعتماد اختبار النجاح على صياغة رسالة الواجهة · تشديد توكيد `BackupScanAndSettingsIntegrityTests` ليكون محدود الطرفين يرفض التواريخ المستقبلية · استخراج اقتطاع نصوص السجل الآمن إلى `LogTextHelper` بحد 50 حرفاً وحماية الأزواج البديلة مع 10 اختبارات شاملة وإعادة تسمية الاختبار السابق ليعكس فحص عدم الاستثناء · إغلاق ثغرة التاريخ المستقبلي في `AutoBackupService` برفضه لمصدري الإعدادات والمسح مع مهلة 5 دقائق لانحراف الساعة وإضافة 4 اختبارات جديدة للمسح والإعدادات · إزالة ستة اختبارات غير قابلة للفشل في مسار النسخ التلقائي وإثبات قابليتها للفشل بثلاث طفرات مؤقتة (113-ب). **أُنجز بالكامل في الجولتين 113 و 113-ب.**

### ☑ ب4 — توسعة المصادر النيترونية (86-ج)
حقول المصدر: الشركة المصنعة، الموديل، النشاط الإشعاعي وتاريخ قياسه، الكبسولة، الأبعاد.
حقول النوع المرجعي: أقصى طاقة، نوع الطيف.
شاشة مستقلة كاملة للأنواع المرجعية على نمط مكتبة النظائر + قسم المراجع العلمية.
محرَّمات حقل النشاط: ألا يُستنتج معدل الانبعاث منه، وألا تُعرض قيمة بلا وحدتها، وألا تُجمع قيم إلا بعد التحويل إلى البكريل.
يسبقه: مراجعة `P074_scr` و `TRS393`. يحتاج ترحيل مخطط.
**الجولة 123 (فرعية أولى، منجزة):** ترحيل `AddNeutronManufacturingAndPhotonRatioFields` يضيف
`NeutronSource.Manufacturer`/`Model`/`CapsuleLengthMm`/`CapsuleDiameterMm` (بترحيل الأبعاد يُشترط
`IsFinite` و`> 0`، مطابقاً لنمط `CalibratedEmissionRate`) و`NeutronSourceType.PhotonToNeutronDoseRatio`
(`IsFinite` فقط، بلا شرط `>0` لأنها نسبة بلا وحدة). خدمات `NeutronSourceService`/
`NeutronSourceTypeService` وسجلات `OldValues`/`NewValues` عُدِّلت لتشمل الحقول الجديدة.
**الجولة 124 (فرعية ثانية، منجزة):** أُغلقت ملاحظة إبهام بيانات البذر. صف `Am-241/Be` الوحيد في
`SeedData()` كان يترك `MeanNeutronEnergyMeV`/`AmbientDoseConversionCoefficient` فارغين لاعتمادهما على
حجم المصدر. استُبدل بصفين دقيقين: `Am-241/Be-Small` (`MeanNeutronEnergyMeV = 4.17`,
`AmbientDoseConversionCoefficient = 393.0`) و`Am-241/Be-Large` (`4.05` و`387.0` على الترتيب)، وفق
ISO 8529-1:2021 Table 1 (الطاقة المرجّحة بالتدفق) وISO 8529-3:2023 Table 2 (`h*_Φ(10;E)`؛ القيمة
الموحّدة السابقة 391 pSv·cm² موثّقة هناك كإصدار سابق مُستبدَل). الصف القديم "Am-241/Be"، إن وُجد من
تشغيل بذر سابق، يُبطَل بالحذف الناعم (`IsDeleted = true`, `DeletedAt`) لا يُحذف فعلياً ولا يُترك نشطاً
بجانب الصفين الجديدين — يحافظ على سلامة أي `NeutronSource` يشير إليه بمفتاح خارجي (`DeleteBehavior.
Restrict`) لكنه سيشير الآن إلى نوع محذوف ناعماً؛ لا بيانات إنتاجية حالياً فلا أثر عملي. لا ترحيل مخطط
مطلوب (`IsDeleted` موجود مسبقاً).
**الجولة 125 (فرعية ثالثة، منجزة محلياً):** أول جولة واجهة لـب4. وصل الحقول الخمسة التي أضافتها
الجولة 123 بالمخطط والخدمة (بلا واجهة حينها) بشاشتين: `SourcesView.xaml`/`SourcesViewModel.cs` صار
نموذج المصدر النيتروني يعرض ويحفظ `Manufacturer`/`Model` (بتعبئة `EditNeutronSource(...)` وإسناد
`SaveAsync()` الناقصين سابقاً) و`CapsuleLengthMm`/`CapsuleDiameterMm` (زوجا حقلين جديدان بنمط
`EditAnisotropyFactorText` بحارس عميل يرفض غير المنتهي و`<=0`، والخدمة تُحرِّس ذلك أصلاً منذ الجولة
123). و`NeutronSourceTypesWindow.xaml`/`NeutronSourceTypesViewModel.cs` صار يعرض ويحفظ
`PhotonToNeutronDoseRatio` (حارس يرفض غير المنتهي فقط، بلا `<=0`، يطابق قرار الجولة 123). **انحراف
مسجَّل:** رسائل الخطأ الثلاث الجديدة نص عربي حرفي مباشر لا `TranslationHelper.GetString` — قواميس
`Resources/Strings.*.xaml` خارج نطاق ملفات هذه الجولة المسموحة، وإضافة مفتاح غير موجود في كلا
القاموسين يُسقط اختبار `TranslationKeysTests` القائم. الترجمة الإنجليزية للتسميات الجديدة مؤجَّلة
لـب5 صراحة. 1109/1107 اختباراً (Debug/Release). **التحقق البصري الحقيقي من `bin\Debug\net8.0-windows`
لم يتم بعد — بانتظار إدريس قبل اعتبار هاتين الشاشتين مكتملتين بصرياً.**
**الجولة 126 (فرعية رابعة، منجزة محلياً):** إضافة نشاط الأمريسيوم-241 كما في شهادة المصدر —
حقلان جديدان مستقلان `NeutronSource.Am241ActivityValue` (`double?`) و`Am241ActivityUnitId`
(`Guid?`, FK اختياري على `ActivityUnit` بنمط `Restrict`، يعيد استخدام جدول `ActivityUnit` القائم
بلا جدول جديد)، بترحيل `AddNeutronSourceAm241Activity`. القرار الجوهري (ISO 8529-1:2021 §4.4):
معدل الانبعاث النيتروني المُعاير ونشاط الأمريسيوم-241 كميتان **مستقلتان لا تُشتق إحداهما من
الأخرى** — العلاقة بينهما تتأثر بعملية التصنيع ودرجة خلط المصدر ولا تخضع لصيغة عامة، فيجب تسجيل
كليهما من شهادة المصدر مباشرة. تاريخ المرجع لاضمحلال هذا النشاط هو `CalibrationDate` نفسه (قرار
معتمد مع إدريس — لا حقل تاريخ جديد). خدمة الحساب `NeutronDecayCalculationService` (خالية من أي
حقن تبعية عمداً) اكتسبت دالتين جديدتين `CalculateCurrentAm241Activity`/`CalculateAm241ActivityAtDate`
بنفس بنية `CalculateEmissionRate` القائمة تماماً: تُحوَّل القيمة إلى بكريل عبر
`Am241ActivityUnit.ConversionToBq` ثم يُطبَّق `B(t) = B₀ × exp(-ln(2) × Δt / T½)` بنصف عمر
432.2 سنة **مُكرَّر عمداً** كثابت محلي (يطابق قيمة جدول `Radioisotope` المرجعية) حفاظاً على
استقلالية هذه الآلة الحسابية عن أي حقن خدمات، بتعليق صريح يوثّق التكرار المتعمَّد. ثلاث حالات
حالة جديدة أُضيفت لتعداد `NeutronDecayCalculationStatus`: `NotRecorded` (الحقل الاختياري غير
مُدخَل — حالة طبيعية لا خطأ)، `MissingActivityUnit` (خاصية التنقل لم تُحمَّل من المستدعي، يماثل
`MissingSourceType`)، و`InvalidActivityValue` (القيمة بعد التحويل غير منتهية أو `<=0`). `NeutronSourceService.Create`/`Update`: حارس `IsFinite`+`>0` على `Am241ActivityValue` عند
إدخالها (بنمط `CapsuleLengthMm`)، ورفض صريح إن أُدخل أحد الحقلين (القيمة أو الوحدة) بمفرده دون
الآخر، والتحقق من وجود `Am241ActivityUnitId` فعلياً في جدول `ActivityUnit`. كائنات
`LogWithChanges` الخمسة في الملف (نمط الجولات 119-123) وُسِّعت لتشمل الحقلين معاً — تفادياً لإعادة
فتح فجوة التدقيق. **لا واجهة في هذه الجولة** (يتبع نمط الفصل 123→125: مخطط/حساب أولاً، وصل واجهة
لاحقاً). 15 اختباراً جديداً: تسعة في `NeutronDecayTests.cs` (تغطي غير المسجَّل، وحدة غير محمَّلة،
قيمة غير صالحة بعد التحويل، اضمحلال نصف عمر كامل، اضمحلال جزئي 100 سنة مطابق لقيمة محسوبة يدوياً،
تحويل وحدة غير-بكريل قبل تطبيق الاضمحلال، تاريخ معايرة غائب، تاريخ حساب يسبق المعايرة، مصدر
`null`)، وستة في `NeutronSourceServiceTests.cs` (رفض قيمة غير منتهية/غير موجبة، رفض إدخال أحد
الحقلين بمفرده عند الإنشاء والتعديل معاً، رفض وحدة غير موجودة، وإثبات مباشر أن سجل التدقيق
`NewValues` يحوي كلا الحقلين فعلياً عند الإنشاء والتعديل). 1124/1122 اختباراً (Debug/Release)،
5 تحذيرات بناء (CS8604 مسبقة، بلا علاقة بهذه الجولة) و0 أخطاء.
**الجولة 127 (فرعية خامسة، منجزة محلياً):** إصلاح — لا ميزة جديدة. CodeRabbit في مراجعة PR #14
(دمج الجولة 126) اكتشف أن دوال القراءة الخمس في `NeutronSourceService` (`GetAll`, `GetDeleted`,
`GetById`, `GetByCode`, `GetByLocation`) كانت جميعها تحمّل `.Include(n => n.NeutronSourceType)` و
`.Include(n => n.Location)` و`.Include(n => n.AddedByUser)` (و`.Include(n => n.DeletedByUser)` في
`GetDeleted`) دون `.Include(n => n.Am241ActivityUnit)` قط. تأكَّد الاكتشاف مباشرةً بقراءة الكود لا
بالاعتماد على ادّعاء CodeRabbit وحده. بما أن `Am241ActivityUnit` خاصية تنقل غير افتراضية
(`non-virtual`، كما أُضيفت في الجولة 126)، تبقى `null` بعد التخلص من `DbContext`، فكانت
`CalculateCurrentAm241Activity`/`CalculateAm241ActivityAtDate` تُرجعان دوماً `MissingActivityUnit`
لأي مصدر يُجلب عبر هذه الدوال الخمس — **حتى مع تخزين `Am241ActivityUnitId` بشكل صحيح فعلياً في
قاعدة البيانات**. الإصلاح إضافي بحت: `.Include(n => n.Am241ActivityUnit)` أُضيفت للدوال الخمس بنفس
موضع/أسلوب استدعاءات `.Include()` القائمة دون إعادة ترتيبها، بلا أي تغيير على منطق التصفية أو
الترتيب أو نوع الإرجاع. لا مساس بـ `Create`/`Update`/`Delete`/`Restore` في الملف نفسه (صحيحة أصلاً
ولم تُمس)، ولا بـ`NeutronDecayCalculationService` (منطق الحساب صحيح كما تحقَّق في الجولة 126؛ هذه
الجولة تسد فجوة تحميل البيانات التي تسبقه فقط)، ولا XAML أو ViewModel. اختبار انحداري واحد جديد في
`NeutronSourceServiceTests.cs`
(`GetById_WithAm241Activity_LoadsActivityUnit_AndDecayCalculationSucceeds`) يثبت الإصلاح من طرف
لطرف فعلياً لا وجود سطر `.Include()` فقط: ينشئ مصدراً نيترونياً بقيمة/وحدة نشاط أمريسيوم-241
صالحتين وتاريخ معايرة عبر `Create` الفعلية للخدمة، يجلبه ثانيةً عبر `GetById` (لا كائن مُنشأ يدوياً)،
ثم يستدعي `CalculateCurrentAm241Activity`/`CalculateAm241ActivityAtDate` على الكائن المجلوب ويؤكد
`Status == Calculated` مع `CurrentActivityBq` غير فارغ لكلتا الدالتين. 1125/1123 اختباراً
(Debug/Release، +1 عن الجولة 126)، 5 تحذيرات بناء (CS8604 مسبقة، بلا علاقة بهذه الجولة) و0 أخطاء.
**الجولة 128 (فرعية سادسة وأخيرة لـب4، منجزة محلياً):** الجولة الأخيرة لواجهة ب4. وصل
`NeutronSource.Am241ActivityValue`/`Am241ActivityUnitId` بنموذج المصدر الموحَّد بحقل قيمة + قائمة
اختيار وحدة قابلة للتغيير (بناءً على طلب إدريس الصريح — تكرار نمط `EditInitialActivity`/
`EditInitialActivityText`/`EditInitialUnitId` القائم في `Source`، لكن اختيارياً `double?` بنمط
`EditCapsuleLengthMm` من الجولة 125 لا نمط `Source` الإلزامي). `SourcesViewModel` اكتسب حقن
اختياري جديد `INeutronDecayCalculationService? neutronDecayService = null` (يفتَرِض
`new NeutronDecayCalculationService()` بنفس نمط `IDecayCalculationService?` القائم في المُنشئ
نفسه)، وخاصية عرض للقراءة فقط `DisplayAm241CurrentActivity` تُحسَب عبر
`CalculateCurrentAm241Activity(target)` عند فتح `EditNeutronSource(...)` لمصدر قائم فقط — لسجل
جديد (`IsNew == true`) تبقى فارغة عمداً إذ لا شيء يُحسَب بعد. حارس عميل «كلاهما أو لا شيء» قبل
الحفظ (بنمط حارس `EditCapsuleLengthText`) يرفض إدخال القيمة بلا وحدة أو العكس قبل استدعاء الخدمة،
والخدمة نفسها (`NeutronSourceService`) تُحرِّس ذلك أصلاً منذ الجولة 126 كخط دفاع ثانٍ. **انحراف
مسجَّل (يكرر نمط الجولة 125):** رسائل الخطأ الجديدة (حارس «كلاهما أو لا شيء» وقيمة النشاط غير
الصالحة) وحالتا العرض `MissingActivityUnit`/`InvalidActivityValue` الجديدتان ونص «لم يُسجَّل» —
نص عربي حرفي مباشر لا `TranslationHelper.GetString` بمفتاح جديد؛ قواميس `Resources/Strings.*.xaml`
خارج نطاق ملفات هذه الجولة المسموحة، وإضافة مفتاح غير موجود في كلا القاموسين يُسقط اختبار
`TranslationKeysTests` القائم (تأكَّد ذلك فعلياً بمحاولة أولى فشلت ثم أُصلحت). الترجمة الإنجليزية
مؤجَّلة لـب5 صراحة. 6 اختبارات جديدة في `SourcesViewModelTests.cs`: إنشاء بقيمة+وحدة يحفظ كليهما
فعلياً عبر `Create` المُلتقَط، تعديل مصدر بنشاط مخزَّن يُعبِّئ القيمة/الوحدة ويحسب النشاط الحالي
بعد اضمحلال نصف عمر كامل (432.2 سنة) مطابق يدوياً لنصف القيمة الابتدائية (بنمط الجولة 126
التحقّقي)، تعديل مصدر بلا نشاط مخزَّن يعرض «لم يُسجَّل» بلا رسالة خطأ، سجل جديد لا يحسب/يعرض شيئاً،
وحالتا رفض عميل (قيمة بلا وحدة / وحدة بلا قيمة) عبر نظرية بحالتين. 1131/1129 اختباراً
(Debug/Release، +6 عن الجولة 127)، 5 تحذيرات بناء (CS8604 مسبقة، بلا علاقة بهذه الجولة) و0 أخطاء.
**التحقق البصري الحقيقي من `bin\Debug\net8.0-windows` لم يتم بعد — بانتظار إدريس.**
مراجعة CodeRabbit على PR #16 (طلبت صراحة عبر `@coderabbitai review` بعد تحويل الـPR من مسودة)
اكتشفت ملاحظة فعلية واحدة قابلة للتنفيذ: احتمال حفظ `EditActivityText` (يُسمَّى `EditAm241ActivityText` وقت اكتشاف CodeRabbit، عُمِّم لاحقاً بتصحيح إدريس) بقيمة قديمة/فارغة
عند الحفظ بمفتاح Enter بسبب `UpdateSourceTrigger=LostFocus` — مُفصَّلة أعلاه في القسم 3 كدَين
تقني مؤجَّل موثَّق (يمسّ سبعة حقول مشتركة في النمط نفسه، ليس حصرياً لهذه الجولة). القرار: تأجيل
بلا إصلاح جزئي في هذه الجولة، حفاظاً على مطابقة العقد (تكرار النمط القائم حرفياً) وتفادياً لتباين
محلي بين حقل جديد وستة حقول قديمة تحمل العيب نفسه.

**تصحيح إدريس قبل الدمج (خطأ تصميم حقيقي، لا تجميلي — نُفِّذ داخل نفس PR #16 لا كجولة لاحقة):**
راجع إدريس PR #16 قبل الموافقة على الدمج واكتشف أن `Am241ActivityValue`/`Am241ActivityUnitId`
ومنطق حساب الاضمحلال مُقيَّدان بالأمريسيوم-241 تحديداً رغم أن `NeutronSource` قد يكون أياً من عشرة
أنواع مرجعية (Cf-252، Pu-238/Be، Pu-239/Be، Ra-226/Be، Sb-124/Be، Am-241/Be ...إلخ)، لكل منها
`ParentNuclide`/`HalfLife`/`HalfLifeUnit` مُسجَّلة أصلاً على `NeutronSourceType`. الثابت المُكرَّر
`Am241HalfLifeYears = 432.2` في `NeutronDecayCalculationService.CalculateAm241ActivityAtDate` كان
سيُعطي نتيجة اضمحلال **خاطئة صامتة** لو استُعمل يوماً لمصدر من نوع غير Am-241. سبب التصحيح داخل
هذا الـPR لا في جولة لاحقة: هذا عيب صحة في كود لم يُدمَج بعد، أرخص وأسلم إصلاحه قبل الدمج من إصلاحه
بعده (ترحيل مخطط إضافي على بيانات إنتاجية محتملة، وواجهة منشورة تحمل تسمية مضلِّلة لفترة).
التغييرات: (1) إعادة تسمية حقول المخطط `Am241ActivityValue`→`ActivityValue`،
`Am241ActivityUnitId`→`ActivityUnitId`، خاصية التنقل `Am241ActivityUnit`→`ActivityUnit`، بترحيل
جديد `RenameNeutronSourceActivityToGeneric` (لا تعديل على ترحيل الجولة 126 المدفوع أصلاً لـmain) —
ولّد EF Core فعلياً عمليات `RenameColumn`/`RenameIndex` حقيقية (لا Drop+Add) تلقائياً. (2) إعادة
تسمية دالتي الحساب `CalculateCurrentAm241Activity`→`CalculateCurrentSourceActivity` و
`CalculateAm241ActivityAtDate`→`CalculateSourceActivityAtDate` في `INeutronDecayCalculationService`/
`NeutronDecayCalculationService` (كانتا خارج نطاق عقد الجولة 128 الأصلي — أُدرجتا الآن حصراً لهذا
التصحيح بتفويض مباشر من إدريس). (3) حذف الثابت `Am241HalfLifeYears` نهائياً واستبداله بقراءة
`source.NeutronSourceType.HalfLife`/`HalfLifeUnit` عبر `TryConvertToSeconds` الموجودة أصلاً — يطابق
حرفياً كيف تتعامل `CalculateEmissionRateAtDate` (نفس الملف) مع نصف العمر لمعدل الانبعاث؛ هذا يجعل
حالتي `MissingSourceType`/`InvalidHalfLife`/`UnsupportedHalfLifeUnit` قابلة للتحقق فعلياً لمسار
النشاط أيضاً الآن، لا لمعدل الانبعاث فقط. (4) تعميم تسميات الواجهة: «نشاط الأمريسيوم-241» →
«النشاط الإشعاعي»، «وحدة نشاط الأمريسيوم-241» → «وحدة النشاط»، «النشاط الحالي المحسوب
للأمريسيوم-241» → «النشاط الحالي المحسوب» (`SourcesView.xaml`)، وتعميم رسائل الخطأ العربية
المقابلة في `NeutronSourceService.cs`. (5) تحديث ثلاثة ملفات اختبار: `SourcesViewModelTests.cs`
(الجولة 128)، و`NeutronSourceServiceTests.cs`/`NeutronDecayTests.cs` (الجولتان 126/127، مدفوعتان
أصلاً لـmain) — إعادة تسمية المراجع، وإضافة `NeutronSourceType.HalfLife`/`HalfLifeUnit` صريحة لكل
اختبار يتوقّع `Calculated` (لم تكن مطلوبة سابقاً حين كان الثابت مُكرَّراً)، زائداً اختباري تحقّق
جديدين في `NeutronDecayTests.cs`: `SourceActivity_MissingSourceType_ReturnsUncalculated` (يثبت أن
هذه الحالة صارت قابلة للتحقّق فعلياً)، و`SourceActivity_Pu238_UsesSourceTypeHalfLife_NotAm241HardcodedValue`
— يثبت الاضمحلال الصحيح لمصدر Pu-238/Be (نصف عمر 87.7 سنة، ليس 432.2) بعد نصف عمر واحد بالضبط
يُعطي نصف القيمة، مع تأكيد صريح أن النتيجة **تختلف** عمّا كان سيُنتجه الثابت القديم المُكرَّر لو
بقي — لا تعميم تسمية فقط، بل إثبات أن الحساب الفعلي صحيح لنوع غير الأمريسيوم-241. 1133/1131 اختباراً
(Debug/Release، +2 عن الجولة 128 الأصلية 1131/1129؛ فشل اختبار واحد مؤقتاً بتوكيد نص عربي قديم
`Create_WithUnknownActivityUnitId_ReturnsFailure` أُصلح فوراً)، نفس 5 تحذيرات `CS8604` المسبقة
(بلا علاقة) و0 أخطاء. **انحراف عملية مُسجَّل:** تعذّر تشغيل `round-implementer` داخل نفس worktree
الفرع القائم بسبب عزل الوكيل الفرعي (sandbox) عن مسارات worktree غير مسارِه المخصَّص — قيد بيئي لا
قرار تصميم؛ نُفِّذ التصحيح مباشرة من جلسة القائد بنفس نمط التحقق (بناء، اختبار كامل مرتين، فحص نطاق
الملفات) بدل التفويض، وسُجِّل صراحة كانحراف عن سير العمل المعتاد.

**الجولة 129 (خارج تسلسل ب4 الفرعي — عرض قراءة فقط في نافذة منفصلة):** أغلقت الفجوة التي
استثناها نطاق الجولة 128 صراحة ("`NeutronSourceDetailsViewModel.cs`/`Window.xaml` — نافذة
تفاصيل/شهادات للقراءة فقط، غير ذات صلة"): مصدر نيتروني بنشاط مُدخَل يعرضه نموذج التحرير بشكل صحيح
منذ الجولة 128 لكن نافذة تفاصيله (أيقونة العين في قائمة المصادر) لم تكن تعرض أي معلومة نشاط إطلاقاً،
رغم أن معدل الانبعاث المماثل يعرض صفَّي معايرته وحسابه الحالي هناك. أُضيفت `NeutronSource.
ActivityValueFormatted` (`[NotMapped]`, `Models/AllModels.cs`) و`ActivityValueFormatted`/
`ActivityDecayResult`/`CurrentActivityDisplay`/`IsCurrentActivityCalculated` (`NeutronSourceDetails
ViewModel.cs`) بنفس نمط `CalibratedEmissionRateFormatted`/`CurrentEmissionRateDisplay` القائم في
النافذة نفسها، وصفان جديدان في `NeutronSourceDetailsWindow.xaml`. استهلاك قراءة فقط للحساب والمخطط
القائمين منذ الجولتين 126/127 — بلا مساس بـ`NeutronDecayCalculationService`/`NeutronSourceService`،
وبلا ترحيل مخطط. اختبار انحداري صريح (على مستوى الموديل وعلى مستوى الـViewModel معاً) يثبت أن
`ActivityUnit == null` رغم `ActivityUnitId.HasValue` لا يرمي استثناءً على طبقة العرض — نفس فئة عيب
الجولة 127 لكن هنا مُتحقَّق منها استباقياً قبل الشحن لا مُكتشفة بعده. 1137/1135 اختباراً
(Debug/Release، +4 عن الجولة 128). التفاصيل الكاملة في `session-summary.md`.

**ب4 مكتملة وظيفياً بالكامل (الجولات 123–128)، وعرض النشاط الآن مربوط بواجهة كلا الشاشتين —
نموذج التحرير (128) ونافذة التفاصيل (129) — ومُتحقَّق منه بصرياً بالكامل من إدريس (حالتا "يوجد
نشاط" و"لا نشاط مسجَّل" كلتاهما، بلقطات شاشة حقيقية من `bin\Debug\net8.0-windows`).** الحاجز
التالي هو **ب5 — إكمال الترجمة
الإنجليزية**، الذي يشمل الآن أيضاً الانحرافين المُسجَّلين في الجولتين 125 و128 (رسائل نصية عربية
حرفية بلا مفاتيح ترجمة). شاشة الأنواع المرجعية المستقلة بالكامل على نمط مكتبة النظائر وقسم
المراجع العلمية داخلها ما زالا تحسينَي واجهة مؤجَّلين اختيارياً (الوظيفة الجوهرية لأنواع المصادر
المرجعية موجودة ضمن نافذة قائمة منذ الجولة 125) ولا يُعدّان حاجزي نشر.

### ☐ ب5 — إكمال الترجمة الإنجليزية
56 نصاً عربياً مثبتاً خارج `DynamicResource` + 7 أعمدة `DataGrid`. أكثرها في `BorrowView.xaml` و `SettingsView.xaml` و `SourceDetailsWindow.xaml`. يُنفَّذ بعد ب4 لتشمل شاشاتها.

**الجولة 130 (فرعية أولى لـب5، منجزة محلياً):** غلَّفت كل رسالة تحقق/نجاح في `NeutronSourceService.
cs` (Create/Update/Delete/Restore) بنمط `TranslationHelper.GetString/GetFormat` مع ارتداد عربي
مطابق حرفياً للنص الأصلي — 20 رسالة (منها الانحرافان المُسجَّلان صراحة في الجولتين 125 و128: رسائل
طول/قطر الكبسولة، ورسالة «كلاهما أو لا شيء» لقيمة النشاط)، بلا أي تغيير في منطق التحقق نفسه ولا في
نصوص `_auditService.LogWithChanges` الوصفية (تبقى عربية دوماً بقرار معماري منفصل عن لغة الواجهة).
20 مفتاح ترجمة جديد أُضيف لكلا القاموسين. اختبار انحداري جديد
(`Create_ValidationAndSuccessMessages_UseEnglishStrings_WhenEnglishLanguageActive`) يُبدِّل قاموس
الموارد النشط فعلياً إلى `Strings.en.xaml` (بنفس آلية تبديل القاموس المُستعملة إنتاجياً في
`App.ApplyLanguage`، عبر URI مطلق بدل النسبي لأن الأخير لا يُحل خارج التطبيق المُعبَّأ) ويثبت أن
النص الإنجليزي الفعلي يُعرَض لا الارتداد العربي. 1138/1136 اختباراً (Debug/Release، +1 عن الجولة
129). **الانحرافان الموثَّقان أعلاه (الجولتان 125/128) أُغلقا الآن بهذه الجولة.** النطاق المتبقي
من ب5 مؤجَّل صراحة لجولات لاحقة: خدمات أخرى تطابق نفس النمط (رسائل عربية حرفية غير مُغلَّفة في
خدمات مشابهة لـ`NeutronSourceService`)، `ViewModels` (مثل `SourcesViewModel.cs`/
`NeutronSourceTypesViewModel.cs`)، و56 النص العربي المثبت + 7 أعمدة `DataGrid` في ملفات XAML
(`BorrowView.xaml`، `SettingsView.xaml`، `SourceDetailsWindow.xaml` وغيرها) المذكورة أعلاه — لم
تُمسّ أي منها في هذه الجولة.

**الجولة 131 (فرعية ثانية لـب5، منجزة محلياً):** غلَّفت كل رسالة تحقق/نجاح في `SourceService.cs`
(Create/Update/Delete/Restore) بنفس نمط الجولة 130 — `TranslationHelper.GetString` مع ارتداد عربي
مطابق حرفياً للنص الأصلي، و`string.Format` (لا `GetFormat` مباشرة) للرسائل ذات المتغيرات لأن
`GetFormat` تُرجع اسم المفتاح نفسه عند غيابه وهذا غير آمن كارتداد. 17 مفتاح ترجمة جديد + إعادة
استخدام `MsgErrNeutronSourceCodeExists` الموجود من الجولة 130 لتطابق نص «كود المصدر موجود بالفعل»
حرفياً بلا تكرار مفتاح. بلا أي تغيير في منطق التحقق نفسه ولا في نصوص `_auditService.LogWithChanges`
الوصفية (تبقى عربية دوماً). **قيد معماري موثَّق:** رسالة نجاح `RestoreSource` تحتوي `source.
ArabicStatus` — خاصية `[NotMapped]` تُعيد نصاً عربياً دائماً بصرف النظر عن لغة الواجهة؛ غُلِّف قالب
الرسالة نفسه بالترجمة لكن قيمة الحالة المعروضة داخله تبقى عربية، معالجتها مؤجَّلة لجولة منفصلة
مستقبلاً. اختبار انحداري جديد
(`CreateSource_ValidationAndSuccessMessages_UseEnglishStrings_WhenEnglishLanguageActive`) بنفس آلية
تبديل القاموس المستعملة في الجولة 130. 1139/1137 اختباراً (Debug/Release، +1 عن الجولة 130). النطاق
المتبقي من ب5 (خدمات أخرى، `ViewModels`، ونصوص XAML المثبتة) ما زال مؤجَّلاً لجولات لاحقة.

**الجولة 132 (فرعية ثالثة لـب5، منجزة محلياً):** غلَّفت كل رسالة تحقق/نجاح في `UserService.cs`
(`Login`/`CreateUser`/`UpdateUser`/`ResetPassword`/`UnlockAccount`/`DeleteUser`/`RestoreUser`/
`ToggleUserFreeze`) بنفس نمط الجولتين 130/131 — `TranslationHelper.GetString` مع ارتداد عربي مطابق
حرفياً للنص الأصلي، و`string.Format` (لا `GetFormat` مباشرة) للرسائل ذات المتغيرات. **حالة معمارية
خاصة جديدة**، غير مسبوقة في الجولتين 130/131 (كانتا تتعاملان مع خاصية `[NotMapped]` ثابتة اللغة
عربياً كـ`ArabicStatus`): `ToggleUserFreeze` كانت تبني متغيراً محلياً `string action` ("تنشيط" أو
"تجميد") يُستخدم في رسالتين مختلفتين — تفصيل سجل التدقيق (يجب أن يبقى عربياً دوماً) ورسالة الإرجاع
للمستخدم (يجب أن تُترجَم بالكامل). فُصل المتغير إلى اثنين: `action` (عربي ثابت، دون تغيير، يُستخدم
حصراً في نص `_auditService.LogWithChanges` الوصفي) و`translatedAction` (يُختار عبر
`TranslationHelper.GetString("TextActivate"/"TextDeactivate")` حسب لغة الواجهة النشطة، يُستخدم
حصراً في رسالة الإرجاع `MsgSuccessToggleFreeze`) — فلا يتسرب أثر الترجمة إلى سجل التدقيق ولا يبقى
أي استثناء معماري في رسالة المستخدم. 20 مفتاح رسالة جديد + مفتاحا الكلمة `TextActivate`/
`TextDeactivate` (بلا إعادة استخدام مفاتيح موجودة — لا تصادم أسماء مع أي خدمة سابقة). بلا أي تغيير
في منطق التحقق أو القفل بعد المحاولات الفاشلة (5 محاولات، 15 دقيقة) أو صلاحيات `RequireAdmin` ولا
في نصوص `_auditService.Log`/`LogWithChanges` الوصفية (تبقى عربية دوماً). اختبار انحداري جديد
(`Login_And_ToggleUserFreeze_Messages_UseEnglishStrings_WhenEnglishLanguageActive`) بنفس آلية تبديل
القاموس المستعملة في الجولتين 130/131، يثبت رسالتي فشل/نجاح تسجيل الدخول بالإنجليزية إضافة لحالة
`ToggleUserFreeze` المترجمة (الكلمة الخاصة معاً). 1140/1138 اختباراً (Debug/Release، +1 عن الجولة
131). النطاق المتبقي من ب5 (خدمات أخرى، `ViewModels`، ونصوص XAML المثبتة) ما زال مؤجَّلاً لجولات
لاحقة.

**الجولة 133 (فرعية رابعة وأخيرة لـب5 من ناحية طبقة الخدمات):** غلَّفت كل رسالة تحقق/نجاح في
`BorrowService.cs` (`CreateRequest`/`MarkReturned`) بنفس نمط الجولات 130-132 —
`TranslationHelper.GetString` مع ارتداد عربي مطابق حرفياً **بما فيه علامات الترقيم** (رسائل هذا
الملف تنتهي بنقطة، بخلاف نظائرها اللفظية في `SourceService.cs` — لم تُدمَج المفاتيح رغم تشابه
المعنى لأن النصين ليسا متطابقين حرفياً). مفتاح واحد جديد (`MsgErrActiveBorrowExists`) أُعيد
استخدامه في موضعين متطابقين حرفياً (التحقق العادي عند الإنشاء و`catch (DbUpdateException)`)، ومفتاح
قالب واحد جديد (`MsgErrGenericWithDetail`, "حدث خطأ: {0}") عبر `string.Format` في كِلا معالجي
`catch (Exception ex)` بالملف — `CreateRequest` يمرر تفصيلاً يشمل `InnerException` إن وُجد،
`MarkReturned` يمرر `ex.Message` فقط، دون أي تغيير في منطق بناء نص التفاصيل نفسه. **انحراف موثَّق
عن نص العقد:** كلمة "غير معروف" (ارتداد كود المصدر المفقود في `MarkReturned`) لم تُغلَّف بمفتاح
جديد (`TextUnknown`) كما اقترح العقد — عند القراءة تبيّن أنها تُستخدم حصراً داخل نص
`_auditService.Log` الوصفي، وسجلات التدقيق مستثناة تماماً من الترجمة بقرار معماري ثابت (تبقى عربية
دوماً)؛ لو غُلِّفت لتسرَّب نص إنجليزي إلى سجل تدقيق يجب أن يبقى عربياً بالكامل — نفس فخ الجولة 132
(`action`/`translatedAction`)، لكن هنا لا يوجد استهلاك آخر للكلمة يبرر مفتاحاً منفصلاً، فتُرك النص
العربي المباشر بلا أي مفتاح غير مُستخدَم. بلا أي تغيير في منطق فحص حالة المصدر أو فحص التسرب أو
الاستعارة النشطة أو تواريخ الإرجاع، ولا في نصوص `_auditService.Log` الوصفية في الدوال الأربع
(`CreateRequest`, `MarkReturned`, `CheckAndUpdateOverdue` بحالتيها) التي تبقى عربية دوماً بتصميم
مقصود ومستثناة صراحة. 9 مفاتيح رسالة جديدة. اختبار انحداري جديد
(`CreateRequest_And_MarkReturned_Messages_UseEnglishStrings_WhenEnglishLanguageActive`) بنفس آلية
تبديل القاموس المستعملة في الجولات 130-132، يثبت رسالة فشل واحدة (محاولة استعارة مصدر غير موجود)
ورسالتي نجاح (`CreateRequest` ثم `MarkReturned`) بالإنجليزية الصحيحة. 1141/1139 اختباراً
(Debug/Release، +1 عن الجولة 132). **سلسلة ب5 الفرعية لطبقة الخدمات مكتملة الآن**
(`NeutronSourceService` ← `SourceService` ← `UserService` ← `BorrowService`). النطاق المتبقي من ب5
(`ViewModels` ونصوص XAML المثبتة، بما فيها 56 نصاً + 7 أعمدة `DataGrid` في `BorrowView.xaml`/
`SettingsView.xaml`/`SourceDetailsWindow.xaml` وغيرها) ما زال مؤجَّلاً لجولات لاحقة.

**الجولة 134 (خامسة، تصحيح نطاق بعد إعلان الاكتمال في الجولة 133 — مدموجة، PR #23، commit
`49fad42dd61ebc82a5d9bfb371fecb10f014fdcc`):** عند مراجعة طبقة الخدمات تبيّن
أن `LocationService.cs` لم تُشمَل ضمن السلسلة الأربعية السابقة رغم مطابقتها لنفس النمط، فغُلِّفت كل
رسالة تحقق/نجاح في `Create`/`Update`/`Delete`/`Restore` بنفس نمط الجولات 130-133 —
`TranslationHelper.GetString` مع ارتداد عربي مطابق حرفياً، و`string.Format` (لا `GetFormat` مباشرة)
للرسائل ذات المتغيرات (`MsgErrCannotDeleteLocationHasSources` باسم الموقع،
`MsgErrCannotRestoreLocationNameConflict` باسم الموقع المتعارض، `MsgSuccessLocationRestored` باسم
الموقع المُسترجَع). دمج صريح لأربعة مفاتيح متكررة حرفياً بين الدوال: `MsgErrInvalidLocationData`
("بيانات الموقع غير صالحة"، `Create`+`Update`)، `MsgErrLocationNameRequired` ("اسم الموقع مطلوب"،
`Create`+`Update`)، `MsgErrLocationNameExists` ("اسم الموقع موجود بالفعل"، `Create`+`Update`)،
و`MsgErrLocationNotFound` ("الموقع غير موجود"، `Update`+`Delete`+`Restore`). 11 مفتاح رسالة جديد
إجمالاً. `guard.Message` من `AuthorizationGuard.RequireEditor` في `Delete`/`Restore` بقي دون أي
تعديل، ونصوص `_auditService.LogWithChanges` الوصفية في الدوال الأربع بقيت عربية دوماً بتصميم مقصود
ومستثناة صراحة من الترجمة. بلا أي تغيير في منطق التحقق من التكرار أو فحص الارتباط بمصادر (`Sources`
أو `NeutronSources`) أو شرط الاسترجاع. اختبار انحداري جديد
(`Create_And_Delete_Messages_UseEnglishStrings_WhenEnglishLanguageActive`) بنفس آلية تبديل القاموس
المستعملة في الجولات 130-133، يثبت رسالة فشل واحدة (اسم موقع فارغ) ورسالتي نجاح (`Create` ثم
`Delete`) بالإنجليزية الصحيحة. 1142/1140 اختباراً (Debug/Release، +1 عن الجولة 133). **سلسلة ب5
الفرعية لطبقة الخدمات مكتملة الآن فعلياً** (`NeutronSourceService` ← `SourceService` ←
`UserService` ← `BorrowService` ← `LocationService`)، مع تحفظ أن أي خدمة أخرى لم تُراجَع بعد صراحة
قد تكشف نفس الفجوة. النطاق المتبقي من ب5 (`ViewModels` ونصوص XAML المثبتة) ما زال مؤجَّلاً لجولات
لاحقة.

**الجولة 135 (سادسة ضمن سلسلة ب5 — مدموجة، PR #25، commit الدمج
`8ecbdf08b75023fff64d6a663c741c2965adbe40`):** أول ملف في هذه السلسلة يجمع بين رسائل مُترجَمة سابقاً (خمس
رسائل تحقق من كون القيمة رقماً منتهياً لنصف العمر/الطاقة/المردود/حد الإعفاء/ثابت غاما، بالمفاتيح
`MsgErrInvalidHalfLifeFinite` وأخواتها، من الجولتين 109/110 تقريباً) ورسائل متبقية لم تُغلَّف بعد —
وليس ملفاً كاملاً غير مُترجَم كالملفات الخمسة السابقة (`NeutronSourceService`/`SourceService`/
`UserService`/`BorrowService`/`LocationService`). غُلِّفت كل الرسائل الـ12 المتبقية في
`RadioisotopeService.cs` (`Create`/`Update`/`Delete`/`Restore`) بنفس نمط الجولات 130-134، مع دمج
صريح لأربعة مفاتيح متكررة حرفياً بين الدوال: `MsgErrInvalidRadioisotopeData`
("بيانات النظير غير صالحة"، `Create`+`Update`)، `MsgErrHalfLifeMustBePositive`
("نصف العمر يجب أن يكون أكبر من صفر"، `Create`+`Update`)، `MsgErrRadioisotopeSymbolExists`
("رمز النظير موجود بالفعل"، `Create`+`Update`)، و`MsgErrRadioisotopeNotFound`
("النظير غير موجود"، `Update`+`Delete`+`Restore`). مفتاح جديد منفصل عمداً `MsgErrInvalidEnergy`
("قيمة الطاقة غير صالحة") لعدم دمجه مع `MsgErrInvalidEnergyFinite` الأطول والمُترجَم سابقاً رغم
تشابه الصياغة. `string.Format` (لا `GetFormat` مباشرة) لرسالتي المتغيرات
(`MsgErrCannotRestoreRadioisotopeSymbolConflict` برمز النظير المتعارض،
`MsgSuccessRadioisotopeRestored` باسم النظير المُسترجَع). الخمس رسائل المُترجَمة سابقاً
(`MsgErrInvalidHalfLifeFinite`/`MsgErrInvalidEnergyFinite`/`MsgErrInvalidYieldFinite`/
`MsgErrInvalidExemptionLimitFinite`/`MsgErrInvalidGammaConstantFinite`) بقيت بلا أي لمس. `guard.Message`
من `AuthorizationGuard.RequireEditor` في `Delete`/`Restore` بقي دون تعديل، ونصوص
`_auditService.LogWithChanges` الوصفية بقيت عربية دوماً بتصميم مقصود. بلا أي تغيير في منطق التحقق من
تكرار الرمز أو فحص الارتباط بمصادر أو شرط الاسترجاع. اختبار انحداري جديد
(`Create_Messages_UseEnglishStrings_WhenEnglishLanguageActive`) بنفس آلية تبديل القاموس المستعملة في
الجولات 130-134، يثبت رسالة فشل واحدة (نصف عمر غير موجب) ورسالة نجاح واحدة (`Create`) بالإنجليزية
الصحيحة. 1143/1141 اختباراً (Debug/Release، +1 عن الجولة 134). النطاق المتبقي من ب5 (`ViewModels`
ونصوص XAML المثبتة، وأي خدمة أخرى لم تُراجَع بعد صراحة) ما زال مؤجَّلاً لجولات لاحقة.

**الجولة 136 (سابعة ضمن سلسلة ب5):** غُلِّفت الرسائل المتبقية في `NeutronSourceTypeService.cs`
(`Create`/`Update`/`Delete`/`Restore`) بنفس نمط الجولات 130-135. 13 مفتاح رسالة جديد، منها 6 مفاتيح
مُدمَجة صراحة بين الدوال: `MsgErrInvalidNeutronSourceTypeData` ("بيانات نوع المصدر غير صالحة"،
`Create`+`Update`)، `MsgErrNeutronSourceTypeCodeRequired` و`MsgErrNeutronSourceTypeNameEnRequired`
(`Create`+`Update`)، `MsgErrInvalidPhotonToNeutronDoseRatioFinite` (`Create`+`Update`)،
`MsgErrNeutronSourceTypeCodeExists` ("رمز نوع المصدر موجود بالفعل"،
`Create`+`Update`)، و`MsgErrNeutronSourceTypeNotFound` ("نوع المصدر غير موجود"،
`Update`+`Delete`+`Restore`). مفتاح إضافي (رابع عشر إجمالاً، غير محسوب ضمن الـ13 الجديدة) مُعاد
استخدامه لا مُكرَّراً: تحقق `HalfLife <= 0` في
`Create`/`Update` كان يحمل نفس النص الحرفي الذي أنشأته الجولة 135 لـ`RadioisotopeService.cs`
(`"نصف العمر يجب أن يكون أكبر من صفر"`)، فأُعيد استخدام مفتاحها `MsgErrHalfLifeMustBePositive`
الموجود مسبقاً في المورد دون أي تكرار أو إضافة مفتاح جديد بنفس النص. `string.Format` (لا `GetFormat`
مباشرة) لرسالتي المتغيرات (`MsgErrCannotRestoreNeutronSourceTypeCodeConflict` برمز النوع المتعارض،
`MsgSuccessNeutronSourceTypeRestored` برمز النوع المُسترجَع). الثلاث رسائل المُترجَمة سابقاً من
الجولة 110 (`MsgErrInvalidHalfLifeFinite`/`MsgErrInvalidMeanNeutronEnergyFinite`/
`MsgErrInvalidAmbientDoseConversionFinite`) بقيت بلا أي لمس، وكذلك تحقق `PhotonToNeutronDoseRatio`
غير المنتهي المُضاف في الجولة 123 (نُقل فقط إلى مفتاح جديد `MsgErrInvalidPhotonToNeutronDoseRatioFinite`
دون تغيير في الشرط نفسه). `guard.Message` من `AuthorizationGuard.RequireEditor` في `Delete`/`Restore`
بقي دون تعديل، ونصوص `_auditService.LogWithChanges` الوصفية بقيت عربية دوماً بتصميم مقصود. بلا أي
تغيير في منطق التحقق من تكرار الرمز أو فحص الارتباط بمصادر نيترونية أو شرط الاسترجاع. اختبار انحداري
جديد (`Create_Messages_UseEnglishStrings_WhenEnglishLanguageActive`) بنفس آلية تبديل القاموس
المستعملة في الجولات 130-135، يثبت رسالة فشل واحدة (نصف عمر غير موجب) ورسالة نجاح واحدة (`Create`)
بالإنجليزية الصحيحة. 1144/1142 اختباراً (Debug/Release، +1 عن الجولة 135). النطاق المتبقي من ب5
(`ViewModels` ونصوص XAML المثبتة، وأي خدمة أخرى لم تُراجَع بعد صراحة) ما زال مؤجَّلاً لجولات لاحقة.

**الجولة 137 (أولى ضمن طبقة `ViewModels` من ب5):** غُلِّفت كل النصوص العربية الظاهرة للمستخدم
المتبقية بلا غلاف في `SourcesViewModel.cs`. 10 مفاتيح جديدة: `DecayStatusNotRecorded` (نفس المفتاح
لحالتي `NeutronDecayCalculationStatus.NotRecorded` والقيمة الافتراضية `_` في نفس تعبير `switch`، دون
تكرار)، `DecayStatusMissingActivityUnit`، `DecayStatusInvalidActivityValue`،
`MsgErrNeutronServiceUnavailable` (نفس المفتاح الواحد لثلاثة مواضع متطابقة نصياً عند
`_neutronSourceService` الفارغة في `Delete`/`Create`/`Update`)، `MsgErrActivityValueUnitTogether`،
`TitleInventoryReportPdf`، `TitleInventoryReportExcel`، وزوج جديد بأسماء غير متعارضة عمداً:
`MsgErrCapsuleLengthPositiveNumber`/`MsgErrCapsuleDiameterPositiveNumber` و
`MsgErrActivityValuePositiveNumber`. **انحراف مسجَّل (اكتُشف قبل الكتابة، لا بعدها):** أسماء المفاتيح
الأصلية التي طلبها نص العقد (`MsgErrCapsuleLengthPositive`/`MsgErrCapsuleDiameterPositive`/
`MsgErrActivityValuePositive`) تتعارض فعلياً مع ثلاثة مفاتيح موجودة مسبقاً من الجولة 123 بنفس
الاسم تماماً لكن بنص عربي مختلف قليلاً (بلا كلمة "رقماً")، تخدم `NeutronSourceService.cs` — ملف خارج
نطاق هذه الجولة، فتعذَّر توحيد الصياغة فيه. تفادياً لتلويث نص Enum `NeutronSourceService` القائم أو
انتهاك تعليمة "لا تُغيّر النص، فقط غلِّفه"، استُخدمت ثلاثة أسماء مفاتيح جديدة ومختلفة بلاحقة `Number`
تحافظ على نص `SourcesViewModel.cs` الحرفي الأصلي دون أي تعديل. مفتاحان مُعاد استخدامهما دون تكرار:
`MsgErrCannotEditActiveBorrowSource` (أنشأته الجولة 131 لـ`SourceService.cs`، نفس النص الحرفي في
منع تعديل الموقع/الحالة لمصدر قيد استعارة نشطة) و`TitleSuccess` (موجود مسبقاً، وحَّد الموضع الوحيد في
الملف الذي كان يستعمل النص الحرفي "نجاح العملية" بدلاً منه، في `DialogHelper.ShowInfo` عند تعديل
مصدر عادي ناجح). لا لمس لنصوص `_auditService.Log`/`LogWithChanges`، ولا تغيير في أي شرط تحقق. اختبار
انحداري جديد
(`EditNeutronSource_WithNoStoredAm241Activity_ShowsEnglishNotRecordedDisplay_WhenEnglishLanguageActive`)
بنفس آلية تبديل القاموس المستعملة في الجولات 130-136، يثبت أن `DecayStatusNotRecorded` يظهر
بالإنجليزية الصحيحة ("Not recorded") عبر السلوك الفعلي لـ`EditNeutronSourceCommand` لا عبر استدعاء
`TranslationHelper` مباشرة فقط. 1145/1143 اختباراً (Debug/Release، +1 عن الجولة 136)، 5 تحذيرات بناء
مسبقة بلا علاقة بهذه الجولة (CS8604) و0 أخطاء. النطاق المتبقي من ب5 (بقية طبقة `ViewModels`، نصوص
XAML المثبتة، وأي خدمة أخرى لم تُراجَع بعد صراحة) ما زال مؤجَّلاً لجولات لاحقة.

**الجولة 138 (ثانية ضمن طبقة `ViewModels` من ب5 — مدموجة، PR #31، commit الدمج
`4a756185177ebaf33d1d3ba9657ebcb4c3b23ffc`):** غُلِّفت كل النصوص العربية الظاهرة
للمستخدم المتبقية بلا غلاف في `NeutronSourceTypesViewModel.cs` بنفس نمط الجولة 137. 8 مفاتيح جديدة
(`MsgErrNeutronTypeCodeRequired`، `MsgErrNeutronTypeHalfLifePositive`،
`MsgErrPhotonToNeutronRatioInvalid`، `TitleSuccessShort`، `MsgErrNeutronReferenceTypeNotFound`،
`MsgConfirmDeleteNeutronType`، `TitleDeleteSuccess`، `TitleDeleteFailed`) وإعادة استخدام صريحة
لثلاثة مفاتيح قائمة (`TitleWarning`، `AlertError`، `AlertConfirmation` — الأخير بنفس نمط
`SourcesViewModel.cs` القائم: قيمة القاموس الفعلية "تأكيد" مع ارتداد محلي مختلف "تأكيد الحذف").
**انحرافان مسجَّلان (اكتُشفا بقراءة `Strings.ar.xaml` الفعلية قبل الكتابة، لا بعدها):**
(1) اسم المفتاح `MsgErrNeutronTypeNotFound` الذي افترضه نص العقد الأصلي لرسالة "النوع المرجعي غير
موجود" **مُستخدَم بالفعل** بقيمة عربية مختلفة تماماً ("نوع المصدر النيتروني المحدد غير موجود") تخدم
ملفاً آخر خارج نطاق هذه الجولة — استُخدم اسم بديل `MsgErrNeutronReferenceTypeNotFound` تفادياً
لتلويث مفتاح قائم (نفس مبدأ انحراف الجولة 137 حول تعارض أسماء الكبسولة). (2) نص عمر النصف في هذا
الملف ("يجب إدخال قيمة عمر نصف موجبة وأكبر من صفر") **غير مطابق حرفياً** لقيمة المفتاح القائم
`MsgErrHalfLifeMustBePositive` ("نصف العمر يجب أن يكون أكبر من صفر")، فأُنشئ مفتاح جديد منفصل
`MsgErrNeutronTypeHalfLifePositive` بدل إعادة الاستخدام — بالضبط كما توقّع نص العقد عند عدم
التطابق. لا استدعاء لـ`_auditService.Log`/`LogWithChanges` في هذا الملف أصلاً (تحقَّق منه صراحة).
بلا أي تغيير في منطق التحقق أو شرط العمل. اختبار انحداري جديد
(`Save_Create_WithEmptyCode_ShowsEnglishRequiredMessage_WhenEnglishLanguageActive`) بنفس آلية تبديل
القاموس المستعملة في الجولات 130-137، يثبت أن `MsgErrNeutronTypeCodeRequired`/`TitleWarning` يظهران
بالإنجليزية الصحيحة عبر السلوك الفعلي لـ`SaveCommand`. 1146/1144 اختباراً (Debug/Release، +1 عن
الجولة 137)، نفس 5 تحذيرات بناء مسبقة بلا علاقة (CS8604) و0 أخطاء. النطاق المتبقي من ب5 (بقية طبقة
`ViewModels`، نصوص XAML المثبتة، وأي خدمة أخرى لم تُراجَع بعد صراحة) ما زال مؤجَّلاً لجولات لاحقة.

### ☐ ب6 — معالج أول تشغيل
يسأل عن مجلد النسخ الاحتياطي ويُفعّل النسخ التلقائي. الافتراضي الحالي `AutoBackupEnabled = false`.

### ☐ ب7 — الدليل والمساعدة
`HelpView` + PDF. يشمل جرعة الإشعاع، مكتبة النظائر، الحاسبة، فحوصات التسرب، المحذوفات، المصادر النيترونية وصلاحياتها، النسخ والاستعادة.

### ☐ ب8 — نظام النشر
يتبع `MASTER_DEPLOYMENT_PLAN_FOR_WINDOWS_DESKTOP_APPLICATION.md` (105 قسماً) على عشر مراحل، **تبدأ بمرحلة تحليل بلا تعديل** بعد استقرار ب1–ب7.
قرارات مبكرة: بناء واحد على .NET 8 لويندوز 10/11، وتوثيق عدم دعم ويندوز 7 و8.1 بوسم `Not Compatible` صراحةً.
أسئلة معلَّقة: ما نظام التشغيل على الأجهزة المستهدفة فعلاً؟ وهل تتوفر شهادة توقيع رقمي أم نوثّق تحذير SmartScreen؟
مطلوبات جديدة من الخطة لم تكن مرصودة: مصدر حقيقة واحد للإصدار، نافذة About، منع تشغيل نسختين، Silent install وExit codes، SHA-256 للمخرجات، `build-release.ps1`.

---

## 3. متبقٍ وليس مانع نشر

**تسجيل الابتلاع الصامت (جرد 106):** `App.xaml.cs` (المظهر) + `LeakTestsViewModel` + `LoginWindow.xaml.cs` · `DashboardViewModel` + `SettingsViewModel` + `SourceDetailsViewModel` ومعها إظهار `CorruptedKeys` · `SourcesViewModel` + `AlertsViewModel` + `ActivityCalculatorViewModel` · إعادة مسح بمعيار «هل بدأ الفعل بنقرة صريحة من المستخدم؟»

**أمنية مؤجَّلة:** `SeedData` يبتلع فشل ترقية هاش المدير — ~~`PasswordHelper.VerifyPassword` يبتلع فشل BCrypt بلا تسجيل~~ أُغلق في الجولة 117 بإضافة `LoggerService.LogError` داخل catch دون تغيير قيمة الإرجاع أو التوقيع (انظر §5)
- ~~القفل بعد المحاولات الفاشلة~~ — **موجود ويعمل، شُطب من القائمة.** تبيّن بالفحص في الجولة 111 أن `UserService.Login` ينفّذه فعلاً: خمس محاولات فاشلة ثم قفل خمس عشرة دقيقة، مع عدّاد يخبر المستخدم بالمتبقي، وتسجيل في السجل، ودالة `UnlockAccount` للمدير. كان مصنَّفاً مؤجَّلاً استناداً إلى توثيق قديم لا إلى الكود.

**دين تقني:** `SettingsHelper` يهجر الملف القديم بعد فشل نسخ عابر (أُدخل في 105-ب) · `LoggerService.cs:9` يبني مسار السجلات يدوياً · 5 تحذيرات `CS8604` مؤكَّدة من CI وموزَّعة على `LoginWindow.xaml.cs` (السطران 104 و199، مكرران لأن الحل يبني `Sources.csproj` ونسخة `wpftmp` مؤقتة) و `ViewInstantiationTests.cs:304`، وتحذيرات `LoginWindow` لا تُعالَج لأن الملف تحت الحظر المطلق — فهي قرار لا دين مفتوح · تبعثر سلاسل الحالة في 45+ موضعاً بلا `enum` · تباين ألوان الحالة · ازدواج وحدات الحاسبة · تكرار `SimpleArabicStatus` · `TestDataGeneratorService` بلا مصادر نيترونية · توحيد UTC · وضوح رسالة تكرار `SourceCode` عند الاسترجاع · مسح double.TryParse: أُغلقت جميع مسارات الحفظ وقُيِّدت بالخدمات والواجهات (الجولتان 109 و110)، والمتبقي تفاعلي خالص في الذاكرة (ActivityCalculatorViewModel)
- ~~`UsersViewModel.UpdateRoleSummaries` تكتب وصف صلاحيات الدور **نصاً ثابتاً** مُرمَّزاً في الكود~~ — أُغلق في الجولة 116 باستبداله ببيان الضبط الفردي (انظر §5).
- رسالتان عربيتان متقاربتان لمعنى واحد: `MsgErrAdminOnly` القائم («غير مصرح: هذه العملية مخصصة لمدير النظام فقط») يستعمله `PasswordPromptDialog`، و `MsgErrOperationAdminOnly` المستحدث في الجولة 111 للحارس. الفصل كان صحيحاً وقتها تفادياً لتغيير نص قائم، ويلزم توحيدهما لاحقاً.
- اسم الدور «مدير النظام» مكتوب حرفياً في مواضع متفرقة من الكود والاختبارات (`User.IsAdmin`، `UsersViewModel`، `PackPermissions`، وملفات اختبار عدة). نفس نمط تبعثر سلاسل الحالة في البند 3، ويُعالَج معه.
- **كنس الوسيط الشامل (مؤجَّل موثَّق):** جاني فشل CI `#100` كان اختبار `BorrowViewModel_ReceivesSourcesUpdatedMessage` عبر `WeakReferenceMessenger.Default` المشترك الذي أبلغ مستقبِلاً يحمل fixture ميتاً (`no such table: Sources`)، وعزلته الجولة 115 بحقن `IMessenger` في `BorrowViewModel`. كنس الوسيط الكامل في بقية الشاشات (سبعة ViewModels + `IDisposable` في الخمسة الناقصة + عزل وسائط الاختبارات القائمة) مؤجَّل موثَّق لما بعد النشر. **[إصلاح `IsTestMode` أُنجز في 115-ب — انظر §5.]**
- كود الإنتاج يقرأ `DialogHelper.IsTestMode` في أربعة مواضع (`SourceNavigationHelper` ×2، `LocationsViewModel`، `PasswordPromptDialog`) كحارس تخطٍّ — وعيُ اختبارٍ مبثوثٌ في الإنتاج. لا يُعالَج الآن (نطاق 115-ب محصور بتثبيت العلم)؛ يُنظر لاحقًا في عزله خلف واجهة اختبار.
- **حقول نصية رقمية بـ`UpdateSourceTrigger=LostFocus` قد تُحفَظ بقيمة قديمة/فارغة عند الحفظ بمفتاح Enter (اكتشاف CodeRabbit على PR #16 للجولة 128، مؤجَّل موثَّق):** `SourcesView.xaml` يحمل `<KeyBinding Key="Enter" Command="{Binding SaveCommand}"/>` على مستوى النافذة (السطر 1075)، بالإضافة إلى زر افتراضي `IsDefault="True"` مربوط بنفس `SaveCommand`. ضغط Enter أثناء تركيز المؤشّر داخل أحد صناديق النص التالية يُفعِّل `SaveCommand` عبر معالجة الإدخال الموجَّه مباشرة **دون** أن يفقد الصندوق تركيزه — فحدث `LostFocus` الذي يُشغِّل تحديث خاصية الربط لا يُطلَق أبداً، ويصل `SaveAsync` إلى القيمة القديمة (أو الفارغة عند إدخال أول) بدل ما كتبه المستخدم للتو. يمسّ سبعة حقول تشترك في نفس نمط الربط: `EditInitialActivityText` (موجود قبل ب4)، و`EditEmissionRateText`، `EditRelativeUncertaintyText`، `EditAnisotropyFactorText`، `EditCapsuleLengthText`، `EditCapsuleDiameterText` (جميعها من جولات ب4 السابقة 123-127)، و`EditActivityText` (الجولة 128 الحالية، اسمها وقت اكتشاف CodeRabbit كان `EditAm241ActivityText` قبل تعميمه بتصحيح إدريس — التي مرّرت الملاحظة أصلاً باعتبارها تكراراً حرفياً مقصوداً لنمط `EditInitialActivityText` القائم وفق نص العقد). ليس عيباً أدخلته الجولة 128 بل نمط سابق موروث عبر كل هذه الحقول؛ التأثير العملي محدود لأن حارس «كلاهما أو لا شيء» (ولمعظم الحقول: حارس `IsFinite`/`>0`) يرفض الحفظ برسالة خطأ صريحة بدل فساد صامت للبيانات — لكنها رسالة خطأ مُضلِّلة («أدخل القيمة») رغم أن المستخدم أدخلها فعلاً. **القرار: تأجيل مقصود، لا إصلاح جزئي.** إصلاح حقل واحد فقط (كما اقترح CodeRabbit بتغيير حقل النشاط وحده إلى `UpdateSourceTrigger=PropertyChanged`) يخالف تعليمات عقد الجولة 128 بتكرار النمط القائم حرفياً، ويُنشئ تبايناً محلياً بين حقل جديد وستة حقول قديمة تحمل العيب نفسه. يُحتاج جولة مخصَّصة لاحقة تُصحِّح نمط زناد الربط عبر الحقول السبعة معاً بقرار موحَّد (`PropertyChanged` أو معالجة صريحة لحدث Enter قبل تنفيذ الأمر) مع اختبار انحداري يحاكي Enter بتركيز نشط لا `LostFocus` وحده.

**عمود «النشاط الحالي» غائب عن جدول المصادر النيترونية:** جدول المصادر النيترونية (`SourcesView.xaml`)
لا يعرض عمود «النشاط الحالي» رغم أن الحساب (`_neutronDecayService.CalculateCurrentSourceActivity`
عبر `DisplaySourceCurrentActivity`) موجود وصحيح ومُستخدَم فعلاً في نافذة التعديل. جدولا المصادر
العادية والمحذوفة يعرضان عمود `CurrentActivityWithUnit`، بينما جدول المصادر النيترونية لا يحتويه.
مؤجَّل لجولة XAML مستقلة قصيرة بعد إغلاق ب5، لإضافة عمود مماثل بنفس أسلوب التنسيق.

**تعليق متقطّع في عدّاء GitHub:** وقع مرتين في أقل من ساعة على كود مرّ أخضر قبله وبعده، وحُصر أثره بمهلة الوظيفة والخطوة في الجولة 114. المؤشّر: خطوة اختبارات تتجاوز خمس دقائق مع أنها تنتهي في 73 ثانية. الإجراء عند تكراره: إعادة تشغيل التشغيل نفسه بلا تعديل كود، ولا يُفتح تحقيق في الحزمة إلا إن فشل التشغيل المعاد على الـ commit نفسه.

**متابعة:** 51 تعليقاً تراكمياً في CodeRabbit عبر 47 مراجعة · `Build and Test #82` أحمر ولم يُفحص

---

## 4. مرفوض نهائياً — ممنوع إحياؤه

**حساب `H*(10)` للمركّبة المباشرة في المجال الحر.** الشهادة الحقيقية غير متاحة ولن تكون، والحساب بلا معايرة غير قابل للدفاع عنه: معامل التحويل متاح لنوع واحد من عشرة (`Cf-252` بـ 385)، و`AnisotropyFactor` غير مقاس لأي مصدر، وتشتت الغرفة يضيف حتى 40% خارج الحساب. وحيث يظهر مكانه في الواجهة يُعرض نص صريح يوضح أنه غير محسوب وسببه — لا صفر ولا فراغ.

**مؤجَّل بقرار:** توحيد `SourceCertificate.AttachedBy` إلى `Guid?`

---

## 5. مغلق ومُتحقَّق منه

عمود «معدل الجرعة (1م)» حساب جاما مشروع (Γ × النشاط بالـ MBq عند متر) يعالج غير الجاما وناقص الثابت بنصوص صريحة بلا أصفار كاذبة، والمصادر النيترونية معزولة عنه باختبار · توحيد المخطط على EF Migrations (90) · صلاحيات الشاشات على مستوى الواجهة (83) · القائمة المنسدلة لاسم المستخدم (84) · تدفق PR و CodeRabbit (85) · الأس العلمي لمعدل الانبعاث (89) · واجهات المصادر النيترونية (86-ب) · عزل مسارات القاعدة والاستيراد الذرّي (105، 105-ب) · إظهار الفشل التفاعلي وتوحيد الحافظة (107، 107-ب) · عاصفة النسخ الاحتياطي والإعدادات التالفة (108، 108-ب) · التقارير الرقابية · منع القيم غير المنتهية من الدخول إلى القاعدة في الشاشات وطبقة الخدمة (109، 110) · إنفاذ صلاحيات الأدوار في عمليات الحذف والاسترجاع والإدارة الحساسة (17 موضعاً) وسدّ ترقية الامتياز (111) · القفل بعد المحاولات الفاشلة (قائم منذ ما قبل الجلسة) · رفض التاريخ المستقبلي في النسخ الاحتياطي التلقائي وسد ثغرة انقطاع النسخ الدوري وتشديد جودة اختبارات الحافظة والسجل والنسخ (113، 113-ب) · عزل وسيط الرسائل IMessenger في BorrowViewModel واختباره لإنهاء تداخل الحزم المتوازية (115) · تثبيت `DialogHelper.IsTestMode` عبر `[ModuleInitializer]` في تجميعة الاختبار وإزالة كل إسناد متبعثر، فلا يُطفأ ولا يُسرَّب بين الفئات المتسلسلة (115-ب) · تصحيح `UsersViewModel.UpdateRoleSummaries`: إزالة الادّعاء الثابت بقائمة صلاحيات موحّدة لدور «مستخدم» (النظام يضبط الصلاحيات فرديًّا عبر `User.Permissions` لا عبر `Role.Permissions`) — استبدال بجملة صادقة (116) · تسجيل استثناء BCrypt في `PasswordHelper.VerifyPassword` بدل الابتلاع الصامت، عبر `LoggerService.LogError` داخل `catch (Exception ex)` المُسمّى حديثاً، دون تغيير قيمة الإرجاع (تبقى `false`) أو التوقيع أو `HashPassword` (117) · إضافة تسجيل تدقيق لثلاث دوال إدارية في `UserService` كانت لا تكتب أي سجل: `ResetPassword` (سجل بسيط بلا `LogWithChanges` ودون أي أثر لهاش كلمة المرور القديمة أو الجديدة)، و`UnlockAccount` و`ToggleUserFreeze` (سجل بفرق قبل/بعد عبر `LogWithChanges`)، عبر نمط `_auditService ?? new AuditService(...)` المطابق لـ `DeleteUser`/`RestoreUser` كي لا يُسقَط السجل صمتاً عند حقن تبعية تدقيق فارغة (118) · إضافة `OldValues`/`NewValues` إلى `SourceService.CreateSource` و`UpdateSource` عبر `LogWithChanges` بنفس نمط `Delete`/`Restore` القائم في الملف (13 حقلاً خاماً بما فيها `ImagePath`)، أول جولة من ثلاث لإغلاق نفس الفجوة في `LocationService` و`RadioisotopeService` و`NeutronSourceService`/`NeutronSourceTypeService` (119) · إضافة `OldValues`/`NewValues` إلى عمليات `LocationService` الأربع (`Create`/`Update`/`Delete`/`Restore`) عبر `LogWithChanges`، ببناء نمط الفروقات من الصفر (5 حقول خامة: `LocationName`, `LocationType`, `Building`, `Room`, `ResponsiblePerson`، دون `AddedBy`)، مع اختبار جديد لعملية `Restore` التي لم يكن لها اختبار سابقاً في الملف (120) · إضافة `OldValues`/`NewValues` إلى عمليات `RadioisotopeService` الأربع (`Create`/`Update`/`Delete`/`Restore`) عبر `LogWithChanges`، ببناء نمط الفروقات من الصفر (13 حقلاً خاماً: `Name`, `ArabicName`, `Symbol`, `RadiationType`, `HalfLife`, `HalfLifeUnit`, `Energy`, `Yield`, `Category`, `ExemptionLimit`, `GammaConstant`, `Notes`, `EnglishNotes`، دون `AddedBy`)، مع الانتباه لالتقاط `Update` من `existing` قبل إسناد `ArabicName` التلقائي وبعد `SaveChanges()` لتعكس القيمة المُملوءة تلقائياً، ولالتقاط `Delete` بعد حارس المصادر المرتبطة وقبل `IsDeleted = true`؛ أربعة اختبارات جديدة لعملية `Restore` التي لم يكن لها أي اختبار سابقاً في الملف (121) · إضافة `OldValues`/`NewValues` إلى عمليات `NeutronSourceService` و`NeutronSourceTypeService` الأربع (`Create`/`Update`/`Delete`/`Restore`) لكل منهما عبر `LogWithChanges` (12 حقلاً خاماً لكل خدمة)، مع التقاط `Delete` في `NeutronSourceTypeService` بعد حارس المصادر النيترونية المرتبطة وقبل `IsDeleted = true`، وتنسيق التواريخ الاختيارية (`CalibrationDate`, `EmissionCalibrationDate`) بصيغة `yyyy-MM-dd`؛ توسيع اختباري `Restore` القائمين في كلا الملفين بدل تكرارهما — هذه الجولة الرابعة والأخيرة تُغلق دين `OldValues`/`NewValues` بالكامل عبر الخدمات الخمس (122)

---

## فرز تعليقات CodeRabbit — قبل الإصدار (الجولة 112)

الأساس: آخر commit مدفوع d27d0f3، CI #95 أخضر عند 1055 اختباراً.
فُرزت عشرة تعليقات مفتوحة. بعد الإصلاحات: 1063 اختباراً أخضر محلياً (Debug)، و1061 على CI (Release). الفرق ثابت وموثّق: اختبارا TestDataGeneratorTests محاطان بـ #if DEBUG فيستثنيهما بناء Release. CI #96 أخضر على commit 0c85d5f.

### منتهية (10)

| # | الملف | المسألة | القرار |
|---|-------|---------|--------|
| 1 | Helpers/NumericInputParser.cs | تفسير الفاصلة كفاصل آلاف عبر الثقافات يحوّل "1,5" إلى "15" — خطأ بعامل 10 في إدخال علمي | تحليل بثقافة ثابتة مع تمييز صريح بين الفاصل العشري وفاصل الآلاف |
| 3 | Services/SourceService.cs | تسرّب قيم NaN/Infinity عبر كائن التنقل SourceIsotopes قبل الحفظ | رفض أي قيمة غير منتهية في CreateSource وUpdateSource قبل الحفظ |
| 4 | Services/SourceService.cs | استرجاع مصدر محذوف قد ينشئ SourceCode مكرراً مع مصدر نشط — خلل جرد في سجلّ رقابي | فحص وجود رمز نشط مطابق قبل الاسترجاع ومنعه |
| 5 | Helpers/SettingsHelper.cs | فقدان إعدادات المستخدم نهائياً إذا مُسح علم الترحيل رغم فشل النسخ | إبقاء علم الترحيل المعلّق حتى نجاح النسخ المؤكد |
| 2 | Resources/Strings.ar.xaml + Services/NeutronSourceTypeService.cs | مصطلح "معامل تحويل التدفق" ناقص؛ الكمية هي الجرعة المحيطية H*(10) | تصحيح إلى "معامل تحويل التدفق إلى الجرعة المحيطية" في المورد والنصّين الاحتياطيين |
| 11 | Services/SourceService.cs + NeutronSourceService.cs | حماية التاريخ المستقبلي في الـ ViewModel وحده؛ طبقة الخدمة عارية — يخالف مبدأ الجولة 111 | حاجز ثانٍ في طبقة الخدمة يرفض تاريخ معايرة مستقبلياً، مع 6 اختبارات استدعاء مباشر |
| 10 | اختبار توكيد التاريخ المتساهل | التوكيد يقبل تواريخ مستقبلية | أُقفل ضمن #11: اختبارات الخدمة المباشرة تغطي الثغرة |
| 6 | Sources.Tests/ClipboardFeedbackTests.cs | مقارنة نصوص عربية حرفية — هشاشة اختبار | استبدال النصوص المباشرة بثوابت ClipboardCopyHelper وتجريد فحص النجاح من صياغة الواجهة (الجولة 113) |
| 8 | Services/SystemSettingsService.cs | خطأ بمقدار واحد في طول التحذير (51 بدل 50) | استخراج الاقتطاع إلى LogTextHelper.Truncate لضمان حد أقصى 50 محرفاً بدقة وحماية الأزواج البديلة (الجولة 113) |
| 9 | Sources.Tests/BackupScanAndSettingsIntegrityTests.cs | توكيد يفحص عدم الاستثناء لا حقيقة الاقتطاع | إعادة تسمية الاختبار ليعكس فحص عدم رمي استثناء، ونقل اختبار الاقتطاع إلى LogTextHelperTests بـ 10 حالات (الجولة 113) |

فحص جانبي (#10): المسار الطبيعي للواجهة محمي أصلاً — MovedAt يُفرض بـ DateTime.Now
ولا حقل إدخال له؛ تواريخ المعايرة والإرجاع مرفوضة في الـ ViewModel. الثغرة كانت في
الاستدعاء البرمجي المباشر للخدمة، وهي ما سدّه #11.

### مرفوض (1)

| # | الملف | المسألة | سبب الرفض |
|---|-------|---------|-----------|
| 7 | Services/BackupFolderScanner.cs | اقتراح fail-closed عند تعذّر قراءة مجلد نسخ | قرار سلوكي محسوم في الجولة 108-ب: fail-closed يولّد إنذارات كاذبة على أعطال صلاحيات عابرة فيتآكل الثقة في مؤشر النسخ. لا معطى تقني جديد يعيد فتحه |

### مؤجَّل بعد الإصدار (0) — لا يمسّ سلوك المنتَج

لا توجد بنود مؤجلة حالياً — نُقلت البنود 6 و8 و9 إلى المنتهية في الجولة 113.

### دَين تحسين مسجَّل
توحيد المصطلح العربي للكمية H*(10) عبر الكود: رسالة الخطأ تقول "الجرعة المحيطية"،
وتعليق AllModels.cs:965 يقول "المكافئ المحيطي". نفس الكمية، صياغتان. تجميلي، بعد الإصدار.

---

## 6. أفكار بعيدة — لا عمل مطلوب

تحويل المنظومة إلى ويب/هاتف · مهارة عامة من منهجية المراجعة · مهارة لتدفق CodeRabbit/PR

---

## 7. قواعد العمل المثبتة

- جولة واحدة ← دفع ← **قراءة رقم الاختبارات من CI لا لونه** ← الجولة التالية. لا تتراكم الجولات.
- تحديث `session-summary.md` و `release-readiness.md` يُدرَج في برومبت الجولة نفسها ويدخل commitها، لا في commit توثيقي منفصل. الاستثناء: ما لا يُعرف إلا بعد الدفع (رقم CI والتحذيرات).
- عدد التحذيرات يُقرأ من خطوة `Build Solution` في CI لا من البناء المحلي.
- اقتراحات أدوات المراجعة الآلية مُدخلات تشخيصية لا أوامر. ممنوع «Fix all issues» وممنوع تمرير ملف تعليماتها إلى وكيل.
- كل تعليق مرفوض يُغلق في اللوحة ويُدوَّن سببه، وإلا تكرر.

---

## 8. مبادرة معمارية جديدة — التراكب المنبثق داخل العرض (In-View Modal Overlay)

**الجولة 139 (أولى، Draft PR غير مدموج بعد):** بدأت مبادرة معمارية منفصلة تماماً عن سلسلة ترجمة
النصوص (ب5) أعلاه — تحويل نوافذ WPF المستقلة (`Window` + `ShowDialog()`) إلى تراكبات `UserControl`
مدمجة داخل الشاشة الأم مباشرة (`IsVisible`/`Visibility` مربوطة بخاصية `bool` في نموذج العرض الأصل،
بدل نافذة منفصلة بدورة حياة `Owner`/`ShowDialog` خاصة بها). `NeutronSourceTypesWindow` هي أول نافذة
تُحوَّل لهذا النمط الجديد (`NeutronSourceTypesOverlay.xaml` مدمجة داخل `SourcesView.xaml` عبر
`IsManagingNeutronTypes`/`NeutronTypesManagementViewModel` في `SourcesViewModel.cs`) — اختيرت أولاً
لأنها حوار CRUD معزول (إدارة أنواع المصادر النيترونية المرجعية) بلا أي تبعيات تنقّل معقدة أو تعشيش
حوارات فرعية، فتصلح كنموذج أول آمن لإرساء النمط قبل تطبيقه على النوافذ الأربع المتبقية الأكثر
تعقيداً. البنية القابلة لإعادة الاستخدام التي أرستها هذه الجولة: (1) `UserControl` بخلفية معتّمة
(`#88000000`) وبطاقة مركزية بظل، (2) زر إغلاق `✕` صريح في الترويسة يستدعي `CloseCommand` جديداً في
نموذج العرض الأصلي للنافذة (`Action? OnClose` + `[RelayCommand] Close()`)، (3) خاصيتا `bool
IsManaging...`/`...ManagementViewModel?` في نموذج العرض المضيف تتحكمان في الظهور عبر
`BoolToVis` القائم بدل `ShowDialog()`، (4) تمرير `Grid.RowSpan`/`Panel.ZIndex="1000"` ليغطي
التراكب كامل مساحة الشاشة المضيفة. لا تغيير في منطق التحقق أو الحفظ داخل
`NeutronSourceTypesViewModel.cs` سوى إضافة `OnClose`/`CloseCommand`. صُحِّح أيضاً نص XAML واحد تسرّب
من ترجمة الجولة 138 (تلميح حقل `PhotonToNeutronRatio` كان لا يزال عربياً مباشراً) عبر مفتاح جديد
`HintPhotonToNeutronRatio`. حُذفت `NeutronSourceTypesWindow.xaml`/`.xaml.cs` بعد تأكيد خلوّ المستودع
من أي مرجع كودي متبقٍ لهما. **انحراف موثَّق (مصرَّح به مسبقاً من القائد):** تعديل سطر واحد في
`App.xaml.cs` لحذف تسجيل `AddTransient<Sources.Views.NeutronSourceTypesWindow>()` — لم يكن ضمن
قائمة الملفات الأصلية في عقد الجولة لكنه ضروري لعدم كسر البناء بعد حذف النافذة، بلا أي لمسة أخرى
لذلك الملف. 1148 اختباراً محلياً (Debug)، صفر فشل، صفر تجاوز، بلا تحذيرات بناء جديدة (التحذيرات
الخمسة الظاهرة CS8604 مسبقة وغير متعلقة بهذه الجولة: `LoginWindow.xaml.cs`،
`ViewInstantiationTests.cs`). **التحقق البصري الحقيقي من `bin\Debug\net8.0-windows` لم يتم بعد —
بانتظار إدريس قبل اعتبار هذا التراكب مكتملاً بصرياً.**
- التحقق البصري من الإقلاع يلتقطه المستخدم بنفسه لا الوكيل.

---

## 9. الجولة 140 — إصلاح عاجل (Hotfix): عودة تراكب أنواع المصادر النيترونية منعت استخدام قسم المصادر بالكامل

**الحالة: Draft PR غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس معاً — لا دمج قبل الاثنين.**

**العَرَض:** منذ دمج الجولة 139، ظهر تراكب `NeutronSourceTypesOverlay` دائماً فوق قسم المصادر بالكامل
بمجرد فتحه، بلا أي استجابة لزر الإغلاق `✕`، مما منع استخدام القسم كلياً.

**السبب الجذري (دقيق):** العنصر `<views:NeutronSourceTypesOverlay>` في `SourcesView.xaml` كان يحمل
معاً `DataContext="{Binding NeutronTypesManagementViewModel}"` و
`Visibility="{Binding IsManagingNeutronTypes, Converter={StaticResource BoolToVis}}"` على نفس
العنصر. في WPF، إعادة تعيين `DataContext` على عنصر تجعل كل ربط آخر على نفس العنصر (بما فيه
`Visibility`) يُقيَّم بالنسبة للـ`DataContext` الجديد لا الموروث من الأب. بما أن
`NeutronSourceTypesViewModel` (نموذج عرض التراكب نفسه) لا يملك خاصية `IsManagingNeutronTypes`، فشل
ربط `Visibility` بصمت وارتدّت القيمة لافتراضيها الأصلي `Visible` دائماً، بصرف النظر عن حالة
`SourcesViewModel.IsManagingNeutronTypes` الفعلية.

**الإصلاح:** لُفّ `NeutronSourceTypesOverlay` بـ`Grid` خارجي يحمل وحده `Visibility` (فيبقى ضمن سياق
`DataContext` الموروث من `SourcesViewModel`)، بينما `DataContext` الخاص بنموذج التراكب بقي فقط على
العنصر الداخلي `NeutronSourceTypesOverlay` نفسه. لا تغيير آخر في الملف، ولا لمسة لـ
`NeutronSourceTypesOverlay.xaml` أو أي نموذج عرض.

**الدرس المستفاد (الأهم في هذه الجولة):** جميع اختبارات الجولة 139 كانت تتحقق من نجاح الربط منطقياً
(استدعاء أمر يضبط خاصية) لا من القيمة *المحسوبة فعلياً* لـ`Visibility` في الشجرة المرئية الحقيقية —
فمرّت رغم الخلل الحرج. اختبار انحداري حقيقي يتحقق من القيمة النهائية لـ`Visibility` كان سيكتشف هذا
قبل الدمج. كما تبيّن أثناء كتابة هذا الاختبار أن الربط المعتمِد على `DataContext` موروث لا يُفعَّل
(`BindingExpression.Status` يبقى `Unattached`) إلا حين يكون العنصر متصلاً بـ`PresentationSource`
حقيقي (نافذة مضيفة) — فاستدعاء `Measure`/`Arrange`/`UpdateLayout` وحده على عنصر معزول لا يكفي
لتفعيل هذا النوع من الربط، ويُرجع القيمة الافتراضية للخاصية (`Visible`) بصرف النظر عن صحة XAML.
لذلك يستضيف الاختبار الجديد `SourcesView` داخل `Window` حقيقية (خارج الشاشة، `ShowInTaskbar=false`)
قبل قراءة `Visibility` الفعلية — تحقُّق تم التأكد منه يدوياً بإعادة الكود لحالته قبل الإصلاح ومشاهدة
فشل الاختبار (`Visible` بدل `Collapsed` المتوقع) ثم إعادة الإصلاح ومشاهدة نجاحه.

**النتائج:** 1149 اختباراً محلياً (Debug)، صفر فشل، صفر تجاوز (+1 عن الجولة 139: اختبار انحداري
جديد `SourcesView_ByDefault_NeutronTypesOverlayHostIsCollapsed`). بناء المشروع الرئيسي بلا تحذيرات
جديدة (التحذيران الوحيدان CS8604 مسبقان في `LoginWindow.xaml.cs`، ممنوع لمسه بقاعدة المشروع).

**انحراف مسجَّل:** العقد طلب "ملف اختبار جديد"؛ الاختبار أُضيف كطريقة جديدة داخل الملف القائم
`Sources.Tests/ViewInstantiationTests.cs` (بدل ملف منفصل) لأنه يتبع حرفياً نمط الاختبارات القائمة
فيه (`RunInSta`, `FindVisualChildren`) وإنشاء ملف موازٍ كان سيكرر تلك الأدوات المساعدة دون فائدة.

---

## 10. الجولة 141 — In-View Modal Overlay لشاشة المواقع (LocationsView)

**الحالة: Draft PR غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس معاً — لا دمج قبل الاثنين.**

ثاني شاشة تُحوَّل لنمط التراكب المنبثق داخل العرض (In-View Modal Overlay) بعد أنواع المصادر
النيترونية (الجولة 139/140). الفرق هذه المرة: `LocationsView` شاشة بسيطة ذات نموذج تحرير واحد داخل
`UserControl` واحد (لا نافذة `Window` مستقلة أصلاً)، لذا لم تكن هناك حاجة لإنشاء `UserControl` overlay
منفصل أو ViewModel فرعي إضافي كما حدث مع `NeutronSourceTypesOverlay.xaml`/
`NeutronTypesManagementViewModel` — كل ما تطلبه الأمر هو: (1) حذف شرط `Visibility` المرتبط بـ
`IsEditing` عن `Border` الجدول ليبقى ظاهراً دائماً، و(2) لف `Border` النموذج بخلفية معتّمة
(`#88000000`) وبطاقة مركزية بظل و`Panel.ZIndex="1000"` بنفس أسلوب الجولة 139، مع إبقاء كل
الـ Bindings/Commands الحالية (`SaveCommand`/`CancelEditCommand`) ونقل حقول النموذج حرفياً بلا أي
تعديل C#. لم يُلمس `LocationsViewModel.cs` إطلاقاً. أُضيف اختبار تكامل حقيقي
(`LocationsViewOverlayTests.cs`) يستضيف `LocationsView` داخل `Window` حقيقية فعلياً (`Show()`/
`UpdateLayout()`, وليس فقط `Measure`/`Arrange`) للتحقق من أن الجدول يبقى ظاهراً في الحالتين
(`IsEditing = true/false`) وأن التراكب (`Panel.ZIndex="1000"`) يظهر فقط عند `IsEditing = true`. تحقُّق
يدوي (بإعادة الملف مؤقتاً لنسخته القديمة من `git show` ثم استعادة النسخة المُصلَحة) أثبت أن كلا
الاختبارين يفشلان فعلاً (`لم يتم العثور على Grid التراكب`) بدون الإصلاح، وينجحان معه. 1150 اختباراً
محلياً (Debug)، صفر فشل، صفر تجاوز (+2 عن الجولة 139؛ الجولة 140 لم تُضِف اختباراً جديداً). بناء
المشروع الرئيسي بلا تحذيرات جديدة.

## 11. الجولة 142 — In-View Modal Overlay لشاشة النظائر المشعة (RadioisotopesView)

**الحالة: Draft PR غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس معاً — لا دمج قبل الاثنين.**

ثالث شاشة تُحوَّل لنمط التراكب المنبثق داخل العرض (In-View Modal Overlay)، إتماماً للمسار B بعد أنواع
المصادر النيترونية (الجولة 139/140) والمواقع (الجولة 141). `RadioisotopesView` تشترك نفس البنية:
ViewModel واحد بين المشهدين (لا ViewModel فرعي منفصل)، ونموذج تحرير من خطوتين (Stepper) داخل
`ScrollViewer`/`Border`. التعديل شمل فقط: (1) حذف شرط `Visibility` المرتبط بـ `IsEditing` عن `Grid`
الجدول ليبقى ظاهراً دائماً بغض النظر عن حالة التحرير، و(2) لف `Border` النموذج (الشبكة ذات 4 صفوف:
شريط العنوان، مؤشر الخطوات، محتوى الخطوة، أزرار التذييل) بخلفية معتّمة (`#88000000`) وبطاقة مركزية
ثابتة العرض (`720`) بظل و`Panel.ZIndex="1000"` بنفس أسلوب الجولتين 140/141، مع نقل مفاتيح `Enter`/
`Escape` من `ScrollViewer.InputBindings` إلى `Border.InputBindings` الخاصة بالبطاقة (بلا تكرار)،
وإبقاء كل الـ Bindings/Commands الحالية (`CurrentStep`, `NextStepCommand`/`PreviousStepCommand`/
`SaveCommand`/`CancelEditCommand`) ومحتوى الخطوتين حرفياً بلا أي تعديل C#. لم يُلمس
`RadioisotopesViewModel.cs` ولا `LeakTestsView.xaml` إطلاقاً. أُضيف اختبار تكامل حقيقي
(`RadioisotopesViewOverlayTests.cs`) يستضيف `RadioisotopesView` داخل `Window` حقيقية فعلياً
(`Show()`/`UpdateLayout()`, وليس فقط `Measure`/`Arrange`) للتحقق من أن الجدول يبقى ظاهراً في الحالتين
(`IsEditing = true/false`) وأن التراكب (`Panel.ZIndex="1000"`) يظهر فقط عند `IsEditing = true`. تحقُّق
يدوي (بإعادة `RadioisotopesView.xaml` مؤقتاً لنسخته القديمة عبر `git checkout <commit-قديم> --` ثم
استعادة النسخة المُصلَحة) أثبت أن كلا الاختبارين يفشلان فعلاً (`لم يتم العثور على Grid التراكب`) بدون
الإصلاح، وينجحان معه. 1153 اختباراً محلياً (Debug)، صفر فشل، صفر تجاوز (+2 عن الجولة 141). بناء
المشروع الرئيسي بلا تحذيرات جديدة.

## 12. الجولة 143 — In-View Modal Overlay لشاشة الاستعارة (BorrowView)

**الحالة: Draft PR غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس معاً — لا دمج قبل الاثنين.**

رابع شاشة تُحوَّل لنمط التراكب المنبثق داخل العرض (In-View Modal Overlay)، إتماماً للمسار B بعد أنواع
المصادر النيترونية (الجولة 139/140)، المواقع (الجولة 141)، والنظائر المشعة (الجولة 142).
`BorrowView` تشترك نفس البنية: ViewModel واحد بين مشهد القائمة ومشهد النموذج (لا ViewModel فرعي
منفصل)، ونموذج تحرير له وضعان متبادلان يحكمهما `IsNew` — وضع أ (طلب استعارة جديد، Stepper من
خطوتين) ووضع ب (عرض وتفاصيل وإرجاع). التعديل شمل فقط: (1) حذف شرط `Visibility` المرتبط بـ
`IsEditing` عن `StackPanel` أزرار الإجراءات العلوية (إضافة/تصدير) ليبقى ظاهراً دائماً، (2) حذف نفس
الشرط عن `Grid` مشهد القائمة (البطاقات الإحصائية، البحث والتصفية، الجدول) ليبقى ظاهراً دائماً بغض
النظر عن حالة التحرير، و(3) لف `Border` بطاقة النموذج (التي تحوي الشبكتين المتبادلتين حسب `IsNew`
دون أي تعديل عليهما) بخلفية معتّمة (`#88000000`) وبطاقة مركزية ثابتة العرض (`720`) بظل
و`Panel.ZIndex="1000"` بنفس أسلوب الجولات 140/141/142، دون إضافة أي `KeyBinding` جديد لم يكن موجوداً
(`Escape` فقط، كما كان). أُبقيت كل الـ Bindings/Commands الحالية (`AddNewCommand`, `CurrentStep`,
`NextStepCommand`/`PreviousStepCommand`/`SubmitCommand`/`MarkReturnedCommand`/`CancelEditCommand`)
ومحتوى الوضعين حرفياً بلا أي تعديل. لم يُلمس `BorrowViewModel.cs` إطلاقاً. أُضيف اختبار تكامل حقيقي
(`BorrowViewOverlayTests.cs`) يستضيف `BorrowView` داخل `Window` حقيقية فعلياً (`Show()`/
`UpdateLayout()`, وليس فقط `Measure`/`Arrange`) للتحقق من أن الجدول يبقى ظاهراً في الحالتين
(`IsEditing = true/false`) وأن التراكب (`Panel.ZIndex="1000"`) يظهر فقط عند `IsEditing = true`؛ لأن
`AddNewCommand` يستدعي `LoadAvailableSources()` التي تفتح `DbContext` فعلياً، استُخدم
`SqliteInMemoryFixture` حقيقي (بدلاً من الاعتماد الضمني على `App.ServiceProvider`) لضمان عدم تأثر
النتيجة بتلوث الحالة الساكنة بين الاختبارات عند تشغيل الحزمة كاملة. تحقُّق يدوي (بإعادة
`BorrowView.xaml` مؤقتاً لنسخته القديمة عبر `git stash` ثم استعادة النسخة المُصلَحة عبر
`git stash apply`) أثبت أن كلا الاختبارين يفشلان فعلاً (`لم يتم العثور على Grid التراكب`) بدون
الإصلاح، وينجحان معه. 1155 اختباراً محلياً (Debug)، صفر فشل، صفر تجاوز (+2 عن الجولة 142). بناء
المشروع الرئيسي بلا تحذيرات جديدة.

## 13. تصحيح معماري للجولة 143 — استبدال التراكب المنبثق داخل العرض بنافذة WPF حقيقية لشاشة الاستعارة (BorrowFormWindow)

**الحالة: Draft PR #37 غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس معاً — لا دمج قبل الاثنين.**

قرار معماري ألغى نمط التراكب المنبثق داخل العرض (In-View Modal Overlay) الذي طبّقته الجولة 143 على
`BorrowView` (والمُصحَّح موضعياً بعدها في نفس الفرع) واستبدله بنافذة WPF مستقلة حقيقية
(`BorrowFormWindow.xaml`/`.xaml.cs`) بنفس نمط `LocationDetailsWindow` القائم مسبقاً في المستودع
(شريط عنوان نظام التشغيل الأصلي، `WindowStartupLocation="CenterOwner"`، تُفتح عبر `ShowDialog()`).
لا يُلغي هذا القرار مبادرة التراكب المنبثق لبقية الشاشات (139/140/141/142) — يقتصر فقط على `BorrowView`
كتصحيح لاحق. التعديل: (1) حُذف `Grid` التراكب بالكامل (`Panel.ZIndex="1000"`، الخلفية المعتّمة
`#88000000`، البطاقة المركزية بالظل) من `BorrowView.xaml`، مع بقاء "المشهد 1" (البطاقات الإحصائية،
شريط البحث والتصفية، الجدول) ظاهراً دائماً كما كان بلا أي `Visibility` مرتبط بـ`IsEditing`؛ (2) أُنشئت
`BorrowFormWindow.xaml`/`.xaml.cs` تحوي *محتوى* البطاقة السابقة حرفياً بلا أي تعديل على الربط أو
الأوامر (`ScrollViewer` وما بداخله: وضعا `IsNew` — طلب استعارة جديد Stepper من خطوتين، وعرض/تفاصيل/
إرجاع)، مع `KeyBinding` وحيد لـ`Escape` على `Window.InputBindings` (نفس السلوك السابق)، ودون
`DataContext` في XAML — يُمرَّر من كود `BorrowView.xaml.cs`؛ (3) أُضيف مفتاح ترجمة جديد
`TitleBorrowForm` ("نموذج الاستعارة" / "Borrow Form") في `Strings.ar.xaml`/`Strings.en.xaml` بنفس نمط
`TitleLocationDetails`؛ (4) أُضيف منطق دورة حياة نافذة في `BorrowView.xaml.cs` (طبقة العرض حصراً، بلا
لمس `BorrowViewModel.cs`): اشتراك بـ`PropertyChanged` على `DataContext` بعد `Loaded` وإلغاء الاشتراك
عند `Unloaded`؛ عند تحوّل `IsEditing` إلى `true` (وبحارس ضد إعادة الدخول يمنع فتح نافذة ثانية) تُنشأ
`BorrowFormWindow` بـ`DataContext`/`Owner` مناسبين وتُستدعى `ShowDialog()`؛ وعند تحوّلها إلى `false`
(من `Save`/`Submit`/`MarkReturned`/`Cancel` داخل الـViewModel كما كانت) تُغلَق النافذة إن كانت مفتوحة.
لم يُلمس `BorrowViewModel.cs` ولا أي أمر من أوامره (`AddNewCommand`, `SubmitCommand`,
`CancelEditCommand`, `NextStepCommand`, `PreviousStepCommand`, `MarkReturnedCommand`) إطلاقاً. أُعيدت
كتابة `BorrowViewOverlayTests.cs` بالكامل: اختبار أول يثبت أن الجدول ظاهر دائماً بغض النظر عن
`IsEditing`؛ واختبار ثانٍ يثبت أن `AddNewCommand` يفتح فعلياً نافذة من نوع `BorrowFormWindow` (يُتحقَّق
عبر `Application.Current.Windows`) وأن `CancelEditCommand` يُغلقها. **تسوية اختبارية موثَّقة:**
`ShowDialog()` التي يستدعيها `BorrowView.xaml.cs` عند `IsEditing=true` تحجب مسار التنفيذ الحالي بمضخة
رسائل متداخلة (nested message pump) خاصة بها؛ استُخدم `Dispatcher.CurrentDispatcher.BeginInvoke` مع
`DispatcherPriority.ApplicationIdle` لجدولة التحقق من فتح النافذة واستدعاء `CancelEditCommand` بحيث
تُنفَّذ هذه الخطوة أثناء تشغيل حلقة `ShowDialog()` المتداخلة نفسها، فتُغلَق النافذة ويعود
`AddNewCommand.Execute` من الحجب طبيعياً — بدل خيط STA مخصص إضافي، لأن `WpfStaFixture` القائم يوفر
بالفعل خيط STA واحد بمضخة `Dispatcher.Run()` تكفي لتشغيل `BeginInvoke` أثناء `ShowDialog()` المتداخلة.
**تحقُّق يدوي إلزامي:** عبر `git stash push -u` (بمعرّف فريد، واستعادة بـ`git stash apply <sha>` لا
`pop`) أُعيدت ملفات `BorrowView.xaml`/`.xaml.cs`/`BorrowFormWindow.*`/ملفي الترجمة إلى نسختها السابقة
للتصحيح بينما بقي ملف الاختبار المُعاد كتابته كما هو — فشل البناء فعلياً بخطأ ترجمة
(`CS0246: BorrowFormWindow could not be found`) لأن النوع غير موجود قبل التصحيح، ثم استُعيدت الملفات
المُصحَّحة عبر `git stash apply` ونجح كلا الاختبارين. 1155 اختباراً محلياً (Debug)، صفر فشل، صفر
تجاوز (عدد صافٍ ثابت — اختباران استُبدلا باختبارين). بناء المشروع الرئيسي بلا تحذيرات جديدة (نفس
التحذيرات الخمسة السابقة في `LoginWindow.xaml.cs`/`ViewInstantiationTests.cs`، غير متعلقة بالاستعارة).

### 13.1 إصلاح عاجل لاحق — تصفير `IsEditing` تلقائياً عند إغلاق `BorrowFormWindow` عبر ✕/Alt+F4

عطل حرج اكتُشف بالاختبار البصري الفعلي بعد التصحيح المعماري أعلاه: إغلاق `BorrowFormWindow` عبر زر
الإغلاق الأصلي لنظام التشغيل أو `Alt+F4` يستدعي فقط `Window.Close()` الافتراضي لـWPF، ولا يمر أبداً
عبر `CancelEditCommand`، فتبقى `BorrowViewModel.IsEditing` عالقة على `true` — يمنع فتح أي نافذة جديدة
لاحقاً ويُظهر تحذير "نافذة مفتوحة" عند التنقل أو إغلاق التطبيق، يضطر المستخدم معه لإنهاء العملية بالقوة.
الإصلاح: أُضيف معالج `Closing` في `BorrowFormWindow.xaml.cs` يستدعي `CancelEditCommand` (بشرط
`CanExecute`) فقط إن كانت `IsEditing` لا تزال `true` لحظة الإغلاق (أي إغلاق عبر ✕/Alt+F4، لا نجاح
الحفظ الذي يُصفّرها الـViewModel نفسه أولاً) — عبر نفس مسار الإلغاء اليدوي تماماً، بلا لمس
`BorrowViewModel.cs`. أُضيفت خاصية `IsClosingInProgress` لمنع استدعاء `Close()` متكرر (reentrant) من
`BorrowView.xaml.cs` أثناء معالجة `Closing` نفسها. اختبار انحداري جديد
(`BorrowFormWindow_ClosedViaNativeCloseButton_ResetsIsEditing_AndAllowsReopening`) يحاكي السيناريو
تماماً: `AddNewCommand` ثم `Close()` مباشرة على كائن النافذة (لا `CancelEditCommand`)، يتحقق من عودة
`IsEditing` إلى `false` تلقائياً، ثم يتحقق أن `AddNewCommand` التالي يفتح نافذة جديدة فعلياً لا شيء.
تحقُّق يدوي مستقل (بإعادة `BorrowFormWindow.xaml.cs`/`BorrowView.xaml.cs` مؤقتاً للنسخة السابقة عبر
`git checkout <commit> --`) أثبت فشل الاختبار فعلاً (`Assert.False(): Actual: True`) قبل الإصلاح
ونجاحه بعده. طلب ثانوي في نفس الكوميت: توسيع `BorrowFormWindow` (720×720 → 900×860) لتقليل الحاجة
للتمرير، بلا إعادة تصميم تخطيط الحقول. مسار "تفاصيل وإجراء الاستعارة" (زر العين) يستخدم نفس خاصية
`IsEditing` وذات آلية الفتح/الإغلاق في `BorrowView.xaml.cs`، فيستفيد من الإصلاح تلقائياً دون حاجة لمسار
منفصل. 1156 اختباراً محلياً (Debug، +1)، صفر فشل. بناء بلا تحذيرات جديدة.

## 14. الجولة 144 — تصحيح معماري لأنواع المصادر النيترونية: استبدال التراكب المنبثق داخل العرض بنافذة WPF حقيقية (`NeutronSourceTypesWindow`)

**الحالة: Draft PR غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس معاً — لا دمج قبل الاثنين.**

قرار معماري طبّق على `NeutronSourceTypesOverlay` (أول شاشة حُوِّلت لنمط التراكب المنبثق داخل العرض،
الجولة 139/140) نفس التصحيح الذي طُبِّق على `BorrowView` في البند 13 أعلاه — إلغاء نمط التراكب
واستبداله بنافذة WPF مستقلة حقيقية (`NeutronSourceTypesWindow.xaml`/`.xaml.cs`) بنفس نمط
`LocationDetailsWindow`/`BorrowFormWindow` القائمين (شريط عنوان نظام التشغيل الأصلي،
`WindowStartupLocation="CenterOwner"`، تُفتح عبر `ShowDialog()`). لا يُلغي هذا القرار مبادرة التراكب
المنبثق لبقية الشاشات المحوَّلة (141/142) — يقتصر فقط على شاشة إدارة أنواع المصادر النيترونية
المرجعية كتصحيح لاحق. التعديل: (1) حُذف `Grid` التراكب بالكامل (`Panel.ZIndex="1000"`، الخلفية
المعتّمة `#88000000`، `<views:NeutronSourceTypesOverlay>` وربط `xmlns:views`) من `SourcesView.xaml`،
مع بقاء جدول/بطاقات المصادر ظاهراً دائماً كما كان بلا أي `Visibility` مرتبط بحالة الإدارة؛ (2) حُذف
`NeutronSourceTypesOverlay.xaml`/`.xaml.cs` بالكامل بعد تأكيد خلوّ المستودع من أي مرجع كودي متبقٍ
لهما؛ (3) أُعيد إنشاء `NeutronSourceTypesWindow.xaml`/`.xaml.cs` (كانت قد حُذفت في الجولة 139) تحوي
*محتوى* التراكب السابق حرفياً بلا أي تعديل على الربط أو الأوامر، بإعادة استخدام مفتاحي الترجمة
القائمين `TitleNeutronSourceTypes`/`SubtitleNeutronTypes` دون أي مفتاح جديد، مع `KeyBinding` وحيد
لـ`Escape` على `Window.InputBindings` (نفس السلوك السابق)، ودون `DataContext` في XAML — يُمرَّر من
كود `SourcesView.xaml.cs`؛ لا حاجة لتسجيل `NeutronSourceTypesWindow` في حاوية DI بـ`App.xaml.cs` (تُنشأ
مباشرة بـ`new` بنفس نمط `BorrowFormWindow`، لا `LocationDetailsWindow`)؛ (4) أُضيف منطق دورة حياة
نافذة في `SourcesView.xaml.cs` (طبقة العرض حصراً، بلا لمس `SourcesViewModel.cs`/
`NeutronSourceTypesViewModel.cs`): اشتراك بـ`PropertyChanged` على `DataContext` بعد `Loaded` وإلغاء
الاشتراك عند `Unloaded`؛ عند تحوّل `IsManagingNeutronTypes` إلى `true` (وبحارس ضد إعادة الدخول يمنع
فتح نافذة ثانية) تُنشأ `NeutronSourceTypesWindow` بـ`DataContext`/`Owner` مناسبين وتُستدعى
`ShowDialog()`؛ وعند تحوّلها إلى `false` تُغلَق النافذة إن كانت مفتوحة وليست بصدد الإغلاق أصلاً
(`IsClosingInProgress`)؛ (5) أُضيف معالج `Closing` في `NeutronSourceTypesWindow.xaml.cs` يستدعي
`CloseCommand` (بشرط `CanExecute`) مباشرة — بلا حاجة لفحص حالة وسيطة كما في `BorrowFormWindow` لأن
`CloseCommand` هنا مُعرَّف أصلاً منذ الجولة 139 كمسار إغلاق وحيد (`OnClose` + تصفير
`IsManagingNeutronTypes`) — مع خاصية `IsClosingInProgress` لمنع استدعاء `Close()` متكرر
(reentrant) من `SourcesView.xaml.cs`، فتُطبَّق دفعة واحدة معالجة الإغلاق عبر ✕/Alt+F4 التي احتاجت
إصلاحاً عاجلاً لاحقاً في حالة `BorrowFormWindow` (البند 13.1)، بدل تكرار نفس العطل ثم إصلاحه لاحقاً.
لم يُلمس `NeutronSourceTypesViewModel.cs` ولا أي أمر من أوامره (`CloseCommand` وأوامر الحفظ/التحقق
القائمة) إطلاقاً. أُعيدت كتابة اختبار التكامل بالكامل: حُذف الاختبار القديم
`SourcesView_ByDefault_NeutronTypesOverlayHostIsCollapsed` من `ViewInstantiationTests.cs` (كان
يتحقق من عنصر `NeutronSourceTypesOverlay` الذي لم يعد موجوداً في `SourcesView.xaml`)، وأُضيف ملف
اختبار تكامل جديد (`SourcesViewNeutronOverlayTests.cs`) يستضيف `SourcesView` داخل `Window` حقيقية
فعلياً (`Show()`/`UpdateLayout()`) بثلاثة اختبارات: (أ) جدول المصادر ظاهر دائماً بغض النظر عن
`IsManagingNeutronTypes`، (ب) `OpenNeutronSourceTypesManagementCommand` يفتح فعلياً نافذة من نوع
`NeutronSourceTypesWindow` (يُتحقَّق عبر `Application.Current.Windows`) وأن جدول المصادر يبقى ظاهراً
تحتها، و(ج) الإغلاق عبر ✕/Alt+F4 (محاكاة بـ`Close()` مباشرة لا `CloseCommand`) يُصفِّر
`IsManagingNeutronTypes` تلقائياً ويسمح بإعادة الفتح لاحقاً. **تسوية اختبارية موثَّقة (نفس أسلوب
البند 13):** `ShowDialog()` التي يستدعيها `SourcesView.xaml.cs` عند `IsManagingNeutronTypes=true`
تحجب مسار التنفيذ الحالي بمضخة رسائل متداخلة خاصة بها؛ استُخدم `Dispatcher.CurrentDispatcher.BeginInvoke`
مع `DispatcherPriority.ApplicationIdle` لجدولة التحقق من فتح النافذة واستدعاء `CloseCommand`/`Close()`
بحيث تُنفَّذ هذه الخطوة أثناء تشغيل حلقة `ShowDialog()` المتداخلة نفسها. **عطل إضافي اكتُشف ثم أُصلح
أثناء كتابة الاختبار الأول (لا علاقة له بالتحويل المعماري نفسه):** `SourcesViewModel` يُحمِّل بياناته
بشكل غير متزامن في مُنشئه (`_ = LoadDataAsync();` تنتظر `Task.Run(...)` داخلياً)، ودالة `LoadDataAsync`
تستدعي `_sourceService.GetDeletedSources()` مباشرة بعد `GetAllSources()` وتستخدم النتيجة فوراً
(`deletedList.Count`) دون فحص `null`؛ إعداد الاختبار (`CreateViewModel`) لم يكن يُهيّئ Stub لهذه
الدالة على `Mock<ISourceService>`، فأعادت Moq افتراضياً `null` (سلوك `MockBehavior.Loose` مع نوع
مرجعي)، فرُمي `NullReferenceException` غير مُلتقَط داخل المهمة غير المتزامنة قبل الوصول لسطر
`Sources = new ObservableCollection<Source>(allSources);`، فبقيت `Sources` فارغة دائماً وظهر جدول
المصادر (`SourceCardsPanel`) كـ`Collapsed` خطأً — ظاهره سباق زمني (Dispatcher لم يُفرَّغ بعد) لكن
جذره الفعلي إعداد Mock ناقص في الاختبار نفسه، لا خلل في كود الإنتاج ولا في ربط `Visibility`. أُضيف
Stub الناقص (`mockSourceService.Setup(s => s.GetDeletedSources()).Returns(new List<Source>())`) في
`CreateViewModel()`، إلى جانب مساعد استطلاع (`WaitForSourcesLoaded`) يُفرِّغ طابور Dispatcher دورياً
حتى تكتمل `Sources` (بمهلة قصوى ~2 ثانية) بدل الاعتماد على توقيت تفريغ واحد غير مضمون، لتفادي سباق
حقيقي متبقٍ في توقيت اكتمال `Task.Run` نفسه. لم يُعدَّل أي توكيد (`Assert`) لإخفاء هذا العطل. **تحقُّق
يدوي إلزامي:** عبر `git stash push -u` (بمعرّف فريد، واستعادة بـ`git stash apply <sha>` لا `pop`) أُعيد
تخزين ملفات الشاشة (`SourcesView.xaml`/`.xaml.cs`، ملفي `NeutronSourceTypesOverlay.*` المحذوفين،
ملفي `NeutronSourceTypesWindow.*` الجديدين، وتعديل `ViewInstantiationTests.cs`) في stash بينما بقي
ملف الاختبار الجديد كما هو في مجلد العمل؛ فشل البناء فعلياً بأخطاء ترجمة متعددة
(`CS0246: NeutronSourceTypesWindow could not be found`) لأن النوع غير موجود قبل التصحيح، ثم
استُعيدت الملفات المُصحَّحة عبر `git stash apply <sha>` وأُسقطت النسخة المخزَّنة بعدها
(`git stash drop`)، ونجحت الاختبارات الثلاثة معاً. 1158 اختباراً محلياً (Debug)، صفر فشل، صفر تجاوز
(−1 اختبار قديم حُذف، +3 اختبارات جديدة = +2 صافياً عن الجولة 143). بناء المشروع الرئيسي بلا تحذيرات
جديدة (نفس التحذيرات الثلاثة المسبقة: اثنان CS8604 في `LoginWindow.xaml.cs`، وواحد CS8604 في
`ViewInstantiationTests.cs`، غير متعلقة بهذه الجولة). لم يُلمس `App.xaml.cs` إطلاقاً (بخلاف الجولة
139 التي احتاجت حذف تسجيل DI، لا حاجة هنا لأن `NeutronSourceTypesWindow` تُنشأ مباشرة بـ`new` لا عبر
الحاوية).
