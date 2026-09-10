# Round 149 — إصلاح خلل فقدان القيمة عند الضغط على Enter في الحقول الرقمية (Enter/LostFocus Bug)

## Identity

- Owner: Edrees
- Lead: Claude Code
- Base branch: `main`
- Base commit: `302280f`
- Working branch: `round-149-enter-lostfocus-numeric-fields`
- Risk: `medium` (لا تغيير في منطق العمل/الحسابات العلمية، لكن الحقول المتأثرة تخضع لثوابت المجال — `AnisotropyFactor`, النشاط، الطاقة، نصف العمر)
- Parallel-safe: `yes` (كاتب واحد، ملفين View + ViewModel واحد لكل نافذة + ملفي توثيق + ملفي اختبار جديدين)

## Goal

عند كتابة قيمة جديدة في أي من الحقول الرقمية الإحدى عشر أدناه، ثم الضغط على Enter مباشرة دون فقدان التركيز يدويًا أولاً، يجب أن تُستخدَم القيمة الجديدة (وليست القديمة/الفارغة) عند تنفيذ الحفظ.

## الاكتشاف الفعلي (يخالف افتراض العقد الأصلي — موثَّق ومؤكَّد من القائد قبل التنفيذ)

العدد الفعلي **11 حقلاً**، وليس 7 كما افترض العقد الأصلي، موزّعة على نافذتين بآليتي سباق مختلفتين:

### أ) `RadioisotopeFormWindow.xaml` — 4 حقول — آلية مطابقة لافتراض العقد الأصلي

`Window.InputBindings` يحمل `<KeyBinding Key="Return" Command="{Binding SaveCommand}"/>` صريحًا ([RadioisotopeFormWindow.xaml:20](../../Sources-System-Project/Views/RadioisotopeFormWindow.xaml)) — قرار مثبَّت من الجولة 146 (أمر حفظ واحد بلا تفريع حسب الخطوة). الحقول:

| السطر | الخاصية |
|---|---|
| 340 | `EditHalfLifeText` |
| 367 | `EditEnergyText` |
| 376 | `EditYieldText` |
| 408 | `EditGammaConstantText` |

### ب) `SourceFormWindow.xaml` — 7 حقول — آلية مختلفة عن الافتراض الأصلي

لا يوجد `KeyBinding` على مستوى النافذة إطلاقًا (قرار متعمد موثّق في تعليق أعلى الملف، الجولة 148، لتفادي حفظ الخطوة الأولى من المعالج متعدد الخطوات قبل اكتمالها). السباق يحدث عبر `IsDefault="True"` على زر الحفظ ([SourceFormWindow.xaml:1088](../../Sources-System-Project/Views/SourceFormWindow.xaml)) الذي **يظهر وينشط فقط في الخطوة 3** (`Visibility` مربوطة بـ`CurrentStep=3`). هذه هي نفس السبعة الحقول الموثقة سابقًا كدَين مؤجَّل من الجولة 128 في `docs/release-readiness.md` (كانت وقتها ضمن `SourcesView.xaml` القديم قبل تحويله في الجولة 148 إلى `SourceFormWindow`؛ الآلية تغيّرت من `KeyBinding` صريح إلى `IsDefault`، لكن الخلل نفسه باقٍ):

| السطر | الخاصية |
|---|---|
| 587 | `EditEmissionRateText` |
| 600 | `EditRelativeUncertaintyText` |
| 633 | `EditAnisotropyFactorText` |
| 646 | `EditCapsuleLengthText` |
| 657 | `EditCapsuleDiameterText` |
| 670 | `EditActivityText` |
| 739 | `EditInitialActivityText` |

### تحقّق التوافق (أُجري قبل التفويض — موثّق هنا لتفادي إعادة الفحص)

فُحصت جميع معالِجات `On<Property>TextChanged` الجزئية (partial) لكل الحقول الإحدى عشر في `SourcesViewModel.cs` و`RadioisotopesViewModel.cs`: جميعها تتعامل مع النص الفارغ/غير القابل للتحليل بإرجاع `null`/`0` بدل رمي استثناء أو إعادة كتابة الخاصية النصية نفسها (لا حلقة كتابة عكسية تُفسد موضع المؤشر). التبديل إلى `PropertyChanged` آمن دون أي تعديل في منطق التحقق.

## القرار المعماري

تغيير `UpdateSourceTrigger` من `LostFocus` إلى `PropertyChanged` على الحقول الإحدى عشر فقط. هذا يُبقي قيمة الخاصية متزامنة مع كل ضغطة مفتاح، فيُزيل شرط السباق (race) بغض النظر عن كون آلية الحفظ `KeyBinding` أو `IsDefault`، بدل محاولة إعادة ترتيب توقيت الأحداث أو معالجة `PreviewKeyDown` يدويًا (أكثر هشاشة وأصعب اختبارًا).

## Allowed files

- `Sources-System-Project/Views/RadioisotopeFormWindow.xaml`
- `Sources-System-Project/Views/SourceFormWindow.xaml`
- `Sources.Tests/RadioisotopeFormWindowTests.cs`
- `Sources.Tests/SourceFormWindowTests.cs`
- `docs/release-readiness.md`
- `docs/session-summary.md`

**ممنوع لمس** أي ملف ViewModel — التعديل XAML فقط، لأن معالِجات `On<Property>TextChanged` الجزئية متوافقة مسبقًا (موثّق أعلاه).

## Forbidden scope

- `LoginWindow`, `LoginView`, `SplashWindow`
- أي تعديل في `SourcesViewModel.cs` أو `RadioisotopesViewModel.cs` أو أي منطق حساب علمي
- الملفات الخمسة المحظورة لتحذيرات CS8604 (لا تُلمس نهائيًا)
- أي تعديل في `DatabasePaths`, الهجرات، أو منطق التفويض/الصلاحيات
- دمج الـ PR أو إصدار أي حكم دمج

## Acceptance criteria

1. جميع الحقول الإحدى عشر المذكورة أعلاه تحمل `UpdateSourceTrigger=PropertyChanged` بدل `LostFocus`.
2. 11 اختبار انحدار جديد (واحد لكل حقل) يحاكي: فتح النافذة (بما يشمل الانتقال إلى الخطوة التي يظهر فيها الحقل/زر الحفظ الفعّال — الخطوة 2 لـ`RadioisotopeFormWindow`، الخطوة 3 لـ`SourceFormWindow`) → كتابة قيمة رقمية جديدة في الحقل عبر تحديث `Text` وإطلاق حدث `TextChanged`/تحديث الربط (وليس فقدان تركيز يدوي) → محاكاة الضغط على Enter (KeyDown مع `Key.Enter` على العنصر المركَّز، دون استدعاء `LostFocus`/`Focus()` على عنصر آخر) → التحقق أن القيمة الجديدة وصلت إلى خاصية الـ ViewModel المرتبطة (وبالتبعية إلى ما يستخدمه `SaveCommand`)، وليست القديمة/الفارغة.
3. لكل اختبار: يجب إثبات فشله فعليًا على السلوك القديم (`git stash push -u -m "round-149-pre-fix"` على ملفي الإنتاج XAML فقط، ثم `git stash apply <sha المسجَّل>` — ممنوع `pop` لأن مكدس stash مشترك مع جلسات أخرى — لتشغيل الاختبار الجديد وحده على الحالة القديمة قبل تطبيق أي تعديل XAML، ثم إسقاط الإدخال بعد التحقق). أرفق مخرجات هذا التحقق كاملة في تقرير الالتزام.
4. نمط اختبار WPF حقيقي معتمد: `Window` حقيقية + `.Show()` + `.UpdateLayout()` (ليس `Measure()`/`Arrange()` فقط)، بنفس أسلوب `RadioisotopeFormWindowTests.cs`/`SourceFormWindowTests.cs` القائم (`WpfStaFixture.RunInSta`).
5. صفر رسائل تحذير بناء جديدة (تحذيرات CS8604 الخمسة القائمة تبقى كما هي دون تغيير).
6. تحديث `docs/release-readiness.md`: إغلاق البند الموثّق في السطر ~462 (الدَين المؤجَّل من الجولة 128) بالإشارة الصريحة لهذه الجولة كحل نهائي، مع ذكر أن `RadioisotopeFormWindow` كانت تحمل نفس الخلل بآلية `KeyBinding` مباشرة (وليست جزءًا من الدَين الموثّق سابقًا لأنها نافذة مختلفة اكتُشفت في هذه الجولة).
7. تحديث `docs/session-summary.md` بنفس الالتزام (commit) وفق العادة الثابتة في المشروع.
8. لا انحراف غير مُبلَّغ عن النطاق أعلاه.

## Required commands

```powershell
dotnet build Sources.sln 2>&1 | Select-String -Pattern "Warning|Error"
dotnet test Sources.sln --configuration Debug
dotnet test Sources.sln --configuration Release
```

أرفق عدد الاختبارات ونتائجها (Debug/Release) كاملة، وأي تحذيرات بناء حرفيًا.

## Migration protocol

Not applicable — لا تغيير في المخطط أو EF Core.

## PR

Draft PR فقط من الفرع `round-149-enter-lostfocus-numeric-fields` إلى `main`. لا دمج. لا حكم دمج ("موافق على الدمج") — هذا حصري للقائد بعد `change-verifier` وموافقة إدريس الصريحة.
