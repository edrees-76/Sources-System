<#
.SYNOPSIS
    يحوّل شعارات PNG الموجودة في Assets إلى صور BMP بمقاسات معالج التثبيت (Inno Setup Wizard)
    وصور الاعتمادات (Credits)، باستخدام System.Drawing فقط (بلا أي اعتماد خارجي).

.DESCRIPTION
    - يُنتج wizard_large.bmp (164x314) من tnrc_3d_final.png لصفحة المعالج الكبيرة.
    - يُنتج wizard_small.bmp (55x58) من sources_logo.png لصفحة المعالج الصغيرة.
    - يُنتج ثلاث صور اعتمادات (90x90): credits_designer.bmp (EFH_3D_logo.png)،
      credits_tnrc.bmp (tnrc_3d_final.png)، credits_app.bmp (sources_logo.png).
    - كل صورة تُرسَم بخلفية بيضاء، بهامش 10% (الشعار يشغل 90% من المساحة)، وتحجيم
      عالي الجودة (HighQualityBicubic) مع توسيط الشعار أفقياً وعمودياً.

.NOTES
    لا يتطلب أي حزمة خارجية — System.Drawing متاح ضمن .NET/Windows مباشرة.
    يُنفَّذ هذا السكربت تلقائياً بواسطة deploy\build-installer.ps1 قبل استدعاء ISCC.
#>

[CmdletBinding()]
param(
    [string]$AssetsSourceDir = (Join-Path $PSScriptRoot "..\..\Sources-System-Project\Assets"),
    [string]$OutputDir = $PSScriptRoot
)

Add-Type -AssemblyName System.Drawing

function New-PaddedBmp {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePngPath,
        [Parameter(Mandatory = $true)][string]$DestBmpPath,
        [Parameter(Mandatory = $true)][int]$TargetWidth,
        [Parameter(Mandatory = $true)][int]$TargetHeight,
        [double]$MarginRatio = 0.10
    )

    if (-not (Test-Path -LiteralPath $SourcePngPath)) {
        throw "ملف الشعار المصدر غير موجود: $SourcePngPath"
    }

    $sourceImage = [System.Drawing.Image]::FromFile($SourcePngPath)
    try {
        $canvas = New-Object System.Drawing.Bitmap($TargetWidth, $TargetHeight)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($canvas)
            try {
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.Clear([System.Drawing.Color]::White)

                # المساحة المتاحة للشعار بعد طرح الهامش من كل جهة (90% من المساحة الكلية)
                $usableWidth = $TargetWidth * (1.0 - $MarginRatio)
                $usableHeight = $TargetHeight * (1.0 - $MarginRatio)

                # نسبة التحجيم تحافظ على أبعاد الشعار الأصلية بلا تشويه
                $scale = [Math]::Min($usableWidth / $sourceImage.Width, $usableHeight / $sourceImage.Height)
                $drawWidth = [int][Math]::Round($sourceImage.Width * $scale)
                $drawHeight = [int][Math]::Round($sourceImage.Height * $scale)

                $offsetX = [int][Math]::Round(($TargetWidth - $drawWidth) / 2)
                $offsetY = [int][Math]::Round(($TargetHeight - $drawHeight) / 2)

                $destRect = New-Object System.Drawing.Rectangle($offsetX, $offsetY, $drawWidth, $drawHeight)
                $graphics.DrawImage($sourceImage, $destRect)
            }
            finally {
                $graphics.Dispose()
            }

            $canvas.Save($DestBmpPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
            Write-Host "تم إنشاء: $DestBmpPath ($TargetWidth x $TargetHeight)"
        }
        finally {
            $canvas.Dispose()
        }
    }
    finally {
        $sourceImage.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$tnrcLogo = Join-Path $AssetsSourceDir "tnrc_3d_final.png"
$sourcesLogo = Join-Path $AssetsSourceDir "sources_logo.png"
$efhLogo = Join-Path $AssetsSourceDir "EFH_3D_logo.png"

# صور المعالج (Wizard) — أحجام Inno Setup القياسية
New-PaddedBmp -SourcePngPath $tnrcLogo -DestBmpPath (Join-Path $OutputDir "wizard_large.bmp") -TargetWidth 164 -TargetHeight 314
New-PaddedBmp -SourcePngPath $sourcesLogo -DestBmpPath (Join-Path $OutputDir "wizard_small.bmp") -TargetWidth 55 -TargetHeight 58

# صور صفحة الاعتمادات (Credits) — 90x90 لكل شعار
New-PaddedBmp -SourcePngPath $efhLogo -DestBmpPath (Join-Path $OutputDir "credits_designer.bmp") -TargetWidth 90 -TargetHeight 90
New-PaddedBmp -SourcePngPath $tnrcLogo -DestBmpPath (Join-Path $OutputDir "credits_tnrc.bmp") -TargetWidth 90 -TargetHeight 90
New-PaddedBmp -SourcePngPath $sourcesLogo -DestBmpPath (Join-Path $OutputDir "credits_app.bmp") -TargetWidth 90 -TargetHeight 90

Write-Host "اكتمل إنشاء جميع صور المعالج والاعتمادات في: $OutputDir"
