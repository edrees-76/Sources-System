# الجولة 158 — توطين تصدير PDF/Excel وفق لغة الواجهة

## الهوية

- المالك: إدريس
- القائد: Claude (الجلسة السحابية المنسِّقة)
- الفرع الأساس: main
- Commit الأساس: d30438b17cdf7c8e3e78d53af78ccafe92d73d26 (الجولة 157، مدموجة)
- فرع العمل: round-158-reporting-localization
- الخطورة: متوسطة — تلمس ملفاً واحداً كبيراً (ReportingService.cs, 1550 سطراً، 20 دالة تصدير) عالي الظهور (كل زر تصدير في النظام)، لكن بلا تغيير مخطط قاعدة بيانات ولا منطق علمي/تنظيمي ولا تفويض
- آمنة للتوازي: لا — تعديل مركّز في ملف واحد، أي جولة أخرى تلمسه ستتعارض

## الخلفية والقرار المعماري

فحص القائد فعلياً Sources-System-Project/Services/ReportingService.cs (commit d30438b، 1550 سطراً). كل عناوين التقارير، أسماء أوراق Excel، ورؤوس الأعمدة، وعناوين الأقسام في كل من 20 دالة توليد تقرير (10 أنواع تقارير × Excel/PDF) هي سلاسل نصية عربية مكتوبة مباشرة في الكود، ولا تمر عبر TranslationHelper — فتظهر بالعربية دائماً بصرف النظر عن لغة الواجهة النشطة. الاتجاه (RTL) أيضاً مضبوط بشكل غير مشروط: worksheet.RightToLeft = true (12 موضعاً) و page.ContentFromRightToLeft() (12 موضعاً في PDF).

الاستثناء الوحيد: 5 نصوص مساعدة فقط تمر بالفعل عبر TranslationHelper.GetString(...) مع قيمة عربية احتياطية (LabelNoDataAvailable, ColEmissionCalibrationDate, TextNotRecorded, AuditUserSystemAutomated, AuditUserDeleted).

القرار المعتمد:
1. مصدر معرفة اللغة النشطة الموثوق والموجود فعلاً في المشروع هو Sources.Helpers.SettingsHelper.Language (خاصية ثابتة static string، تُقرأ من settings.ini، تُرجع "ar" افتراضياً أو "en") — وليس Thread.CurrentUICulture (قد لا يكون مضبوطاً بشكل موثوق في سياق تصدير قد يُستدعى بعيداً عن خيط الواجهة). كل نقطة قرار "عربي أم إنجليزي" في ReportingService.cs تستخدم SettingsHelper.Language == "en".
2. كل عنوان/رأس عمود/نص ثابت حالياً يُستبدَل باستدعاء TranslationHelper.GetString(key) ?? "<نفس النص العربي الحالي كقيمة احتياطية>" — بنفس نمط الدوال المساعدة الخمس الموجودة فعلاً (GetNoDataText() وأخواتها)، تفادياً لكسر أي شيء إن غاب المفتاح.
3. اتجاه الصفحة/الورقة (RightToLeft / ContentFromRightToLeft) يصبح شرطياً: SettingsHelper.Language != "en" بدل true الثابتة، وبديلها عند الإنجليزية استدعاء المقابل اليساري (worksheet.RightToLeft = false، و page.ContentFromLeftToRight() أو ما يعادلها في QuestPDF — يتحقق المُنفِّذ من الاسم الدقيق للدالة المقابلة في مكتبة QuestPDF المُستخدَمة قبل الكتابة).
4. reportTitle الممرَّر بالفعل كمعامل من الـViewModel في 4 دوال (GenerateInventoryReportExcelAsync/PdfAsync, GenerateLeakTestsReportExcelAsync/PdfAsync, GenerateFailedLeakTestsReport*, GenerateNeutronInventoryReport*) لا يُغيَّر — العنوان يأتي من طبقة الاستدعاء أصلاً؛ التوطين هنا يقتصر على الرؤوس/التسميات الثابتة داخل هذه الدوال (أعمدة الجدول، عناوين الأقسام)، وليس reportTitle نفسه (خارج نطاق هذه الجولة إن كان مصدره ViewModel آخر).
5. لا تغيير في منطق حساب البيانات، الفرز، الفلترة، أو أي قيمة رقمية/علمية — توطين نصوص العرض والاتجاه فقط.

## الأدلة والتشخيص (فحص كود فعلي)

- ReportingService.cs: 1550 سطراً، 20 دالة عامة توليد تقرير (GenerateLocationsReportExcelAsync/PdfAsync, GenerateInventoryReportExcelAsync/PdfAsync, GenerateBorrowHistoryExcelAsync/PdfAsync, GenerateLowActivityAlertReportExcelAsync/PdfAsync, GenerateGeneralReportExcelAsync/PdfAsync, GenerateUsersReportExcelAsync/PdfAsync, GenerateAuditLogsExcelAsync/PdfAsync, GenerateLeakTestsReportExcelAsync/PdfAsync, GenerateFailedLeakTestsReportExcelAsync/PdfAsync, GenerateNeutronInventoryReportExcelAsync/PdfAsync).
- 12 موضع worksheet.RightToLeft = true / ws.RightToLeft = true (أسطر: 119, 262, 415, 546, 668, 693, 719, 744, 914, 1028, 1138, 1286, 1426 — بعض الدوال بها أكثر من ورقة Excel واحدة).
- 12 موضع page.ContentFromRightToLeft() (أسطر: 171, 306, 460, 591, 788, 959, 1071, 1206, 1354, 1483).
- 5 نصوص فقط موطَّنة فعلاً عبر TranslationHelper.GetString بنمط دالة مساعدة خاصة (GetNoDataText() وما شابه، أسطر 47-103).
- مصدر اللغة النشطة: Sources.Helpers.SettingsHelper.Language (Helpers/SettingsHelper.cs:145-146) — خاصية ثابتة، افتراضي "ar"، تُكتَب من SettingsViewModel.cs (سطر 245 و257) عند تبديل اللغة عبر App.ApplyLanguage("ar"|"en").
- TranslationHelper.GetString(key) (Helpers/TranslationHelper.cs): يبحث عن المفتاح في Application.Current.Resources الحالية (القاموس المُبدَّل بين Strings.ar.xaml/Strings.en.xaml) — يعمل بشكل طبيعي من أي خيط طالما Application.Current غير null.

## المطلوب في هذه الجولة

### الخطوة 1 — التحقق قبل التنفيذ

أعد فحص الملف كاملاً بنفسك سطراً بسطر (لا تفترض القائمة أعلاه شاملة 100%) — ابحث تحديداً عن أي سلسلة نصية عربية إضافية (تسميات أوراق Excel عبر SanitizeSheetName، تنسيقات تاريخ، وحدات قياس مكتوبة كنص) فاتت هذا الفحص. سجّل قائمة نهائية كاملة بكل موضع قبل الشروع في التعديل.

### الخطوة 2 — دالة مساعدة موحَّدة

أنشئ دالة مساعدة خاصة واحدة في ReportingService.cs، مثل:
private static bool IsEnglish() => SettingsHelper.Language == "en";
private static string T(string key, string arFallback) => TranslationHelper.GetString(key) ?? arFallback;
استخدمها بدل تكرار نمط GetNoDataText() يدوياً في كل موضع جديد (الدوال المساعدة الخمس الموجودة يمكن تركها كما هي أو إعادة كتابتها بنفس الآلية — قرار المُنفِّذ، طالما لا تغيير في القيمة المُرجَعة).

### الخطوة 3 — توطين كل عنوان ورأس عمود

لكل من الدوال الـ20: استبدل كل سلسلة نصية عربية ثابتة (عناوين، أسماء أعمدة، عناوين أقسام) بـ T("مفتاح جديد مناسب", "النص العربي الأصلي"). أضف مفتاح ترجمة جديد لكل نص إلى كلا القاموسين Strings.ar.xaml (القيمة = نفس النص العربي الأصلي حرفياً، بلا تغيير) و Strings.en.xaml (ترجمة إنجليزية دقيقة ومهنية للمصطلح العلمي/التنظيمي المقابل — راجع الترجمات الإنجليزية الموجودة فعلاً في نفس القاموس لمصطلحات مشابهة من جولات سابقة (ب5) للحفاظ على الاتساق في المصطلحات).

### الخطوة 4 — الاتجاه الشرطي

استبدل كل worksheet.RightToLeft = true بـ worksheet.RightToLeft = !IsEnglish(); وكل page.ContentFromRightToLeft() بفرع شرطي: if (IsEnglish()) page.ContentFromLeftToRight(); else page.ContentFromRightToLeft(); (تحقَّق أولاً من الاسم الدقيق لدالة QuestPDF المقابلة لـContentFromLeftToRight أو ما يعادلها في الإصدار المُستخدَم فعلياً في Sources.csproj — إن لم توجد دالة مقابلة مباشرة، ابحث عن الطريقة الصحيحة لضبط الاتجاه يسار-لأصل في QuestPDF PageDescriptor/ContentDescriptor ووثّقها في تقرير الإنجاز).

### الخطوة 5 — الاختبارات

- اختبار وحدة جديد لكل نوع تقرير (أو مجموعة اختبارات مُعامَلة Parametrized): يولّد التقرير مرتين (SettingsHelper.Language = "ar" ثم "en")، ويتحقق أن نصاً معيناً معروفاً (مثل عنوان الجدول الرئيسي) يختلف فعلياً بين الحالتين ويطابق القيمة المتوقعة من كل قاموس — وليس فقط أن التصدير "لا يرمي استثناء".
- تأكد أن SettingsHelper.Language قابل للضبط والاستعادة بأمان داخل الاختبار (اقرأ القيمة الأصلية قبل التعديل وأعدها في finally/Dispose — تجنُّب تسريب حالة بين الاختبارات المتوازية، بنفس قاعدة المشروع بخصوص عزل حالة الاختبارات).
- تأكد أن كل الاختبارات القائمة التي تستدعي أياً من دوال ReportingService لا تزال تمر دون تعديل غير ضروري.

### الخطوة 6 — التوثيق

حدِّث docs/release-readiness.md: أضف قسماً موجزاً يوثّق أن تصدير PDF/Excel أصبح يتبع لغة الواجهة النشطة، ومصدر القرار (SettingsHelper.Language).

## قيود صارمة

- لا تغيير في منطق حساب البيانات، الفرز، الفلترة، أو أي قيمة رقمية/علمية/تنظيمية.
- لا تغيير في reportTitle الممرَّر من الـViewModel (خارج النطاق).
- لا تلمس LoginWindow, LoginView, SplashWindow.
- لا تلمس أياً من ملفات الـ5 تحذيرات CS8604 المحظورة.
- افتح Draft PR فقط، لا تدمج أبداً.
- لا تُصدر أي حكم دمج ("موافق على الدمج") — هذا القرار حصري للقائد بعد التحقق المستقل.
- إن وجدت أن مكتبة QuestPDF المُستخدَمة لا توفر مقابلاً مباشراً لـContentFromLeftToRight()، توقف وأرسل تقريراً بالخيارات الممكنة بدل الافتراض أو الحل الملتوي (workaround) غير الموثَّق.

## معايير القبول

1. تصدير أي تقرير مع SettingsHelper.Language = "en" يُنتج عناوين ورؤوس أعمدة إنجليزية بالكامل (عدا reportTitle الممرَّر من الخارج)، واتجاه يسار-لليمين في كل من Excel وPDF.
2. تصدير نفس التقرير مع SettingsHelper.Language = "ar" يُنتج نفس المخرجات العربية الحالية بالضبط (لا تغيير في السلوك الحالي).
3. لا تغيير في أي قيمة بيانات أو حساب.
4. كل الاختبارات القائمة (قبل هذه الجولة) لا تزال تمر.
5. توثيق في docs/release-readiness.md.

## الأوامر المطلوبة

dotnet build Sources.sln --configuration Release
dotnet test Sources.Tests/Sources.Tests.csproj --configuration Release

## بروتوكول الترحيل

لا ينطبق — لا تغيير في مخطط قاعدة البيانات.

## أساس الاختبار المتوقَّع

- محلي (Debug): يُقرَأ من نتيجة dotnet test الفعلية بعد التنفيذ.
- CI (Release): يُقرَأ من تشغيل GitHub Actions الفعلي على الـPR.

## التحقق البصري من إدريس

مطلوب — بدّل لغة الواجهة إلى الإنجليزية من الإعدادات، صدّر نموذجاً واحداً على الأقل من كل نوع تقرير (Excel وPDF) وتأكد بصرياً أن العناوين والاتجاه إنجليزي بالكامل ومقروء بشكل صحيح (لا نص مقلوب أو محاذاة مكسورة)، ثم أعد اللغة للعربية وتأكد أن المخرجات تعود كما كانت قبل هذه الجولة تماماً.

## متطلبات تقرير الإنجاز

- Commit الأساس والنتيجة.
- رابط Draft PR.
- قائمة كل ملف مُتأثر فعلياً مع نوع التغيير وتبرير سطر واحد.
- عدد مفاتيح الترجمة الجديدة المُضافة لكل قاموس (يجب أن يتطابق العددان).
- كيف حُلَّت مسألة ContentFromLeftToRight في QuestPDF (الاسم الدقيق المُستخدَم فعلياً).
- عدد الاختبارات الجديدة/المُعدَّلة ونتائجها الفعلية (Debug وRelease من CI).
- تحذيرات البناء.
- الانحرافات أو "لا يوجد".
- المخاطر المتبقية.
