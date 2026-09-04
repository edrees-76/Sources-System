# Start a Sources System round

Replace the placeholders, then give this prompt to the Claude Code lead after `/clear`:

```text
ابدأ الجولة [ROUND-ID] من العقد:
[RELATIVE-PATH-TO-ROUND-CONTRACT]

التزم بـ CLAUDE.md. لا تبدأ التنفيذ فورًا.

1. تحقق من git status والفرع والـHEAD وقارنه بـBase commit في العقد.
2. اقرأ docs/release-readiness.md والعقد فقط أولًا.
3. استخدم code-explorer للقراءة الموجهة إذا احتاج التشخيص عدة ملفات.
4. اعرض الحقائق المشاهدة، السبب الجذري، القرار المعماري، نطاق الملفات، والمخاطر.
5. توقف لعرض قرارك قبل تشغيل round-implementer إذا كان القرار يغيّر المخطط، الصلاحيات، النسخ/الاستعادة، الحسابات العلمية، أو النشر.
6. بعد اعتماد القرار، شغّل round-implementer في worktree مستقل.
7. بعد فتح Draft PR، شغّل change-verifier.
8. اقرأ أنت الـdiff الفعلي والملفات عالية الخطورة كاملة، وفرز تعليقات CodeRabbit فرديًا.
9. لا تدمج PR. اختم بحكم واضح ولا تستخدم عبارة «موافق على الدمج» إلا بعد اكتمال جميع الأدلة.
10. بعد موافقة إدريس والدمج، استخدم ci-monitor للتحقق من SHA ورقم الاختبارات وتحذيرات Build Solution.

الحد الأقصى وكيلان نشطان. وكيل كاتب واحد فقط لهذا PR. أبلغ عن أي انحراف صراحةً.
```

