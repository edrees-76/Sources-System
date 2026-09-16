<#
.SYNOPSIS
    يبني مثبِّت Windows حقيقي (Setup.exe) لمنظومة مصادر - Sources System.

.DESCRIPTION
    الخطوات بالترتيب:
    1. dotnet publish نشر ذاتي الاكتفاء (self-contained) لمعمارية win-x64.
       PublishSingleFile=false دائماً — ملف واحد ممنوع صراحة في هذه الجولة.
    2. تشغيل generate-wizard-images.ps1 لإنتاج صور المعالج (BMP) من شعارات
       Assets الحالية.
    3. استدعاء ISCC.exe (مُصرِّف Inno Setup 6) على installer.iss مع تمرير
       مسار مجلد النشر عبر /DPublishDir.

.PARAMETER Configuration
    إعداد البناء (افتراضياً Release).

.PARAMETER InnoSetupCompiler
    المسار الكامل إلى ISCC.exe. الافتراضي هو مسار التثبيت القياسي لـ Inno Setup 6.

.NOTES
    يتطلب تثبيت Inno Setup 6 على جهاز البناء (لا يُثبَّت تلقائياً من هذا السكربت).
    لا يُنتج هذا السكربت توقيعاً رقمياً لأي ملف — لا SignTool ولا أي أداة توقيع أخرى.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$InnoSetupCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"

$deployDir = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $deployDir "..")
$projectPath = Join-Path $repoRoot "Sources-System-Project\Sources.csproj"
$publishDir = Join-Path $repoRoot "Sources-System-Project\bin\$Configuration\net8.0-windows\$RuntimeIdentifier\publish"
$issPath = Join-Path $deployDir "installer.iss"

Write-Host "=== الخطوة 1/3: dotnet publish ($RuntimeIdentifier, $Configuration، ذاتي الاكتفاء، ملف واحد: لا) ==="
& dotnet publish $projectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "فشل dotnet publish (رمز الخروج $LASTEXITCODE). راجع مخرجات البناء أعلاه قبل المتابعة."
}

if (-not (Test-Path (Join-Path $publishDir "Sources.exe"))) {
    throw "اكتمل dotnet publish بلا خطأ ظاهر لكن Sources.exe غير موجود في: $publishDir — لا يمكن المتابعة."
}

Write-Host "=== الخطوة 2/3: توليد صور معالج التثبيت (BMP) ==="
$generateImagesScript = Join-Path $deployDir "assets\generate-wizard-images.ps1"
if (-not (Test-Path $generateImagesScript)) {
    throw "سكربت توليد الصور غير موجود: $generateImagesScript"
}
& $generateImagesScript
if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
    throw "فشل توليد صور معالج التثبيت (رمز الخروج $LASTEXITCODE)."
}

Write-Host "=== الخطوة 3/3: تصريف مثبِّت Inno Setup (ISCC.exe) ==="
if (-not (Test-Path $InnoSetupCompiler)) {
    throw "لم يُعثر على مُصرِّف Inno Setup 6 (ISCC.exe) في المسار: $InnoSetupCompiler`nيجب تثبيت Inno Setup 6 على جهاز البناء، أو تمرير المسار الصحيح عبر -InnoSetupCompiler."
}

& $InnoSetupCompiler "/DPublishDir=$publishDir" $issPath

if ($LASTEXITCODE -ne 0) {
    throw "فشل تصريف مثبِّت Inno Setup (رمز الخروج $LASTEXITCODE). راجع مخرجات ISCC أعلاه."
}

Write-Host "=== اكتمل بناء المثبِّت بنجاح. الناتج في: $(Join-Path $deployDir 'output') ==="
