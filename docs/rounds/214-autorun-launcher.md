# Round 214 — واجهة التشغيل التلقائي (AutoRun Launcher)

## Identity

- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `a893bce2b728d08d1fd80c37ade80a9fa87060a9`
- Working branch: `round-214-autorun-launcher`
- Risk: `low` (مشروع جديد معزول؛ لا مساس بالمنظومة ولا بقاعدة البيانات ولا بالمصادقة)
- Parallel-safe: `yes`

## Goal

برنامج تشغيل مستقل (`SourcesAutoRun.exe`) يُوزَّع بجوار مثبِّت المنظومة، يعرض شعار المنظومة،
وتبديل لغة الواجهة عربي/إنجليزي، ورقم تفعيل المنظومة مع زر نسخ، وزر فتح دليل الاستخدام بلغة الواجهة،
وزر تثبيت المنظومة (تحقق من البصمة ثم تشغيل المثبِّت). ومعه سكربت يجمع مجلد التوزيع النهائي مع `autorun.inf`.

## Evidence and diagnosis

- ملف التثبيت الحالي: `deploy/output/v1.1.2/SourcesSystemSetup_v1.1.2.exe` (~81MB) وبصمته `*.sha256` بصيغة `HASH  FILENAME`.
- `LicenseService` يتحقق من الرقم التسلسلي بمقارنة hash؛ النص الأصلي غير مخزَّن في الكود، فلا يمكن اشتقاقه من المنظومة.
  الرقم يُزوَّد من ملف إعدادات بجانب الواجهة (قرار إدريس، السؤال 3).
- ويندوز 10/11 يعطّل autorun على الفلاشات؛ `autorun.inf` يعمل للأقراص الضوئية وخيار AutoPlay فقط → الواجهة تعمل أيضاً بنقرتين.
- المستودع خاص، لكن الرقم لا يُلتزم به: `autorun.config` غير متتبَّع (gitignore) ويُحقن عند التجميع.

## Architectural decision

- **الإطار: `net48` (WPF) لا `net8.0`** — انحراف مُعلَن عن «Stack» في CLAUDE.md. السبب: الواجهة تعمل *قبل* التثبيت على جهاز قد لا يملك
  .NET 8؛ .NET Framework 4.8 مضمَّن في ويندوز 10 (1903+) و11، فينتج exe بحجم ~مئات الكيلوبايت بدل ~70MB+ لنشر ذاتي الاكتفاء.
- المنطق غير المرتبط بـ WPF (التحقق من البصمة، قراءة الإعداد، تحديد الملفات، النصوص ثنائية اللغة) في ملفات مستقلة
  تُضمَّن (Link) في `Sources.Tests` للاختبار على net8.0 دون مشروع اختبار جديد.
- حفظ اللغة في `%LocalAppData%\SourcesAutoRun\language.txt` (مجلد منفصل عن `LocalAppData\Sources` حتى لا تمسّه عمليات الاستعادة/التصفير في المنظومة؛
  الواجهة لا تستطيع الإشارة إلى `DatabasePaths` لأنها مشروع مستقل).
- فشل التحقق من البصمة أو غياب ملف `.sha256` ⇒ **رفض التشغيل** (fail-closed) مع رسالة واضحة.
- الواجهة تبقى مفتوحة بعد تشغيل المثبِّت ليتمكن المستخدم من نسخ رقم التفعيل أثناء التثبيت.

## Allowed files

- `Sources.AutoRun/**` (مشروع جديد)
- `Sources.sln` (إضافة المشروع)
- `Sources.Tests/AutoRun*Tests.cs` و`Sources.Tests/Sources.Tests.csproj` (ربط ملفات المنطق + `Compile Include`)
- `deploy/build-autorun-package.ps1`, `deploy/autorun/**`
- `.gitignore` (استثناء `deploy/autorun/autorun.config`)
- `docs/rounds/214-autorun-launcher.md`, `docs/release-readiness.md`, `docs/session-summary.md`

## Forbidden scope

- `LoginWindow`, `LoginView`, `SplashWindow`
- أي تعديل في `Sources-System-Project` أو `installer.iss` أو سير عمل CI
- توقيع الكود
- دفع مباشر لـ `main` أو دمج PR

## Acceptance criteria

1. النافذة تعرض الشعار، زر تبديل اللغة (عربي RTL / إنجليزي LTR) يحدّث كل النصوص فوراً، وتبدأ بلغة ويندوز وتتذكر آخر اختيار.
2. رقم التفعيل يُقرأ من `autorun.config`؛ زر النسخ يضعه في الحافظة ويُظهر تأكيداً، وفشل الحافظة يُظهر رسالة. غياب الرقم يعطّل النسخ ويُظهر نصاً بديلاً.
3. زر الدليل يفتح PDF بلغة الواجهة؛ غياب الملف أو قارئ PDF يُظهر رسالة.
4. زر التثبيت يتحقق من SHA-256 ثم يشغّل المثبِّت؛ مفقود/تالف/بلا بصمة ⇒ رسالة ولا تشغيل. إلغاء UAC يُعامل كإلغاء لا كخطأ.
5. اختبارات وحدة للمنطق (بصمة، إعداد، تحديد الملفات، تطابق مفاتيح اللغتين).
6. سكربت التجميع يُنتج المجلد النهائي ويتحقق من وجود كل مكوّناته.
7. بناء `Sources.sln` Release بلا تحذيرات جديدة؛ لا تراجع في اختبارات `Sources.Tests`.
8. `release-readiness.md` و`session-summary.md` محدَّثان في نفس الالتزام.

## Required commands

```powershell
dotnet build Sources.sln --configuration Release
dotnet test Sources.Tests/Sources.Tests.csproj --configuration Release --no-build
```

## Migration protocol

not applicable

## Expected test baseline

- Debug/local expected count: خط الأساس الحالي (انظر `release-readiness.md`) + اختبارات الجولة الجديدة
- Release/CI expected count: نفسه
- Documented conditional-test difference: لا جديد

## Visual verification by Edrees

يلتقط إدريس من مجلد التوزيع الناتج: (1) الواجهة بالعربية، (2) بالإنجليزية، (3) بعد نسخ رقم التفعيل، (4) رسالة غياب ملف التثبيت.

## Completion report requirements

- Base/result SHA, Draft PR URL, per-file justification, test counts, build warnings, deviations (الإطار net48)، remaining risks.
