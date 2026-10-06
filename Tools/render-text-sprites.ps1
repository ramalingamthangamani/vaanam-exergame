# Renders every phrase in Assets/_Vaanam/Scripts/Core/Localization.cs to PNG,
# once in English and once in Tamil, plus the icon set the UI uses.
#
# Why: Unity's text components cannot shape Tamil (vowel signs end up in the
# wrong place). Windows' own text engine (GDI+) shapes it correctly, so we draw
# the words here and the game shows pictures. Re-run after editing phrases.
#
# Usage (PowerShell):  .\Tools\render-text-sprites.ps1
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)
Add-Type -AssemblyName System.Drawing

$locFile = Join-Path $ProjectRoot "Assets\_Vaanam\Scripts\Core\Localization.cs"
$outRoot = Join-Path $ProjectRoot "Assets\_Vaanam\Resources\UI"
New-Item -ItemType Directory -Force (Join-Path $outRoot "Text\en") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $outRoot "Text\ta") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $outRoot "Icons") | Out-Null

# ---- parse the phrase table ---------------------------------------------------
$lines = Get-Content $locFile -Encoding UTF8
$phrases = @()
foreach ($line in $lines) {
    if ($line -match '\{\s*"([a-z0-9_]+)",\s*\("([^"]*)",\s*"([^"]*)"\)\s*\}') {
        $phrases += [pscustomobject]@{ Key = $Matches[1]; En = $Matches[2]; Ta = $Matches[3] }
    }
}
Write-Host "Found $($phrases.Count) phrases"

function Render-Phrase([string]$text, [string]$fontName, [string]$path, [float]$size) {
    $font = New-Object System.Drawing.Font($fontName, $size, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $measure = New-Object System.Drawing.Bitmap 10, 10
    $mg = [System.Drawing.Graphics]::FromImage($measure)
    $mg.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $sz = $mg.MeasureString($text, $font)
    $mg.Dispose(); $measure.Dispose()

    $pad = [int]($size * 0.35)
    $w = [int]([math]::Ceiling($sz.Width)) + $pad * 2
    $h = [int]([math]::Ceiling($sz.Height)) + $pad * 2
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

    # Outline + fill via a path so it stays readable over any sky.
    $gp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $fmt = [System.Drawing.StringFormat]::GenericTypographic
    $gp.AddString($text, $font.FontFamily, [int]$font.Style, $size, (New-Object System.Drawing.PointF($pad, $pad)), $fmt)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(230, 20, 30, 60), ($size * 0.16))
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $gp)
    $g.FillPath([System.Drawing.Brushes]::White, $gp)

    $g.Dispose(); $pen.Dispose(); $gp.Dispose(); $font.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

foreach ($p in $phrases) {
    $size = if ($p.Key -eq "title") { 160 } else { 96 }
    Render-Phrase $p.En "Arial Rounded MT Bold" (Join-Path $outRoot "Text\en\$($p.Key).png") $size
    Render-Phrase $p.Ta "Nirmala UI" (Join-Path $outRoot "Text\ta\$($p.Key).png") $size
}
Write-Host "Rendered text sprites to $outRoot\Text"

# ---- icons -------------------------------------------------------------------
# Simple, chunky line drawings: white strokes with a dark outline, 512x512.
$S = 512
function New-Canvas {
    $bmp = New-Object System.Drawing.Bitmap $S, $S, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    return @($bmp, $g)
}
$outline = [System.Drawing.Color]::FromArgb(235, 20, 30, 60)
function Pen([System.Drawing.Color]$c, [float]$w) {
    $p = New-Object System.Drawing.Pen($c, $w); $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p.LineJoin = 'Round'; return $p
}
# Draw a polyline twice: thick dark, then thinner colour.
function Stroke($g, [System.Drawing.PointF[]]$pts, [System.Drawing.Color]$color, [float]$w) {
    $g.DrawLines((Pen $outline ($w + 14)), $pts)
    $g.DrawLines((Pen $color $w), $pts)
}
function Line($g, $x1, $y1, $x2, $y2, $color, $w) { Stroke $g @((New-Object System.Drawing.PointF($x1, $y1)), (New-Object System.Drawing.PointF($x2, $y2))) $color $w }
function Disc($g, $cx, $cy, $r, [System.Drawing.Color]$fill) {
    $g.FillEllipse((New-Object System.Drawing.SolidBrush($outline)), ($cx - $r - 7), ($cy - $r - 7), ($r * 2 + 14), ($r * 2 + 14))
    $g.FillEllipse((New-Object System.Drawing.SolidBrush($fill)), ($cx - $r), ($cy - $r), ($r * 2), ($r * 2))
}
function RingShape($g, $cx, $cy, $r, [System.Drawing.Color]$color, $w) {
    $g.DrawEllipse((Pen $outline ($w + 14)), ($cx - $r), ($cy - $r), ($r * 2), ($r * 2))
    $g.DrawEllipse((Pen $color $w), ($cx - $r), ($cy - $r), ($r * 2), ($r * 2))
}
$white = [System.Drawing.Color]::White
$skin = [System.Drawing.Color]::FromArgb(255, 255, 205, 150)
$orange = [System.Drawing.Color]::FromArgb(255, 255, 140, 40)
$yellow = [System.Drawing.Color]::FromArgb(255, 255, 220, 70)
$blue = [System.Drawing.Color]::FromArgb(255, 90, 170, 255)
$green = [System.Drawing.Color]::FromArgb(255, 110, 210, 120)

# A stick figure. armL/armR angles in degrees (0 = straight down, 90 = out, 180 = up). lean in degrees.
function Figure($g, $cx, $cy, [float]$armL, [float]$armR, [float]$lean = 0, [float]$scale = 1, [bool]$crouch = $false) {
    $state = $g.Save()
    $g.TranslateTransform($cx, $cy)
    $g.RotateTransform($lean)
    $g.ScaleTransform($scale, $scale)
    $headY = -150; $neck = -100; $hip = 40; $foot = 170
    if ($crouch) { $headY = -80; $neck = -40; $hip = 40; $foot = 120 }
    Disc $g 0 $headY 42 $skin
    Line $g 0 $neck 0 $hip $white 26
    $al = $armL * [math]::PI / 180; $ar = $armR * [math]::PI / 180
    $armLen = 120
    Line $g 0 ($neck + 15) (-[math]::Sin($al) * $armLen) ($neck + 15 + [math]::Cos($al) * $armLen) $white 26
    Line $g 0 ($neck + 15) ([math]::Sin($ar) * $armLen) ($neck + 15 + [math]::Cos($ar) * $armLen) $white 26
    if ($crouch) {
        Line $g 0 $hip -70 ($hip + 40) $white 26; Line $g -70 ($hip + 40) -60 $foot $white 26
        Line $g 0 $hip 70 ($hip + 40) $white 26; Line $g 70 ($hip + 40) 60 $foot $white 26
    } else {
        Line $g 0 $hip -45 $foot $white 26
        Line $g 0 $hip 45 $foot $white 26
    }
    $g.Restore($state)
}
function Arrow($g, $x1, $y1, $x2, $y2, $color) {
    Line $g $x1 $y1 $x2 $y2 $color 22
    $ang = [math]::Atan2($y2 - $y1, $x2 - $x1)
    $hl = 55
    Line $g $x2 $y2 ($x2 - $hl * [math]::Cos($ang - 0.6)) ($y2 - $hl * [math]::Sin($ang - 0.6)) $color 22
    Line $g $x2 $y2 ($x2 - $hl * [math]::Cos($ang + 0.6)) ($y2 - $hl * [math]::Sin($ang + 0.6)) $color 22
}
function Save($bmp, $g, $name) { $g.Dispose(); $bmp.Save((Join-Path $outRoot "Icons\$name.png"), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose() }

# stand: figure, arms down, on a ground line
$c = New-Canvas; $g = $c[1]; Line $g 60 470 450 470 $white 18; Figure $g 256 290 15 15; Save $c[0] $g "stand"
# raise: arms up and out
$c = New-Canvas; $g = $c[1]; Figure $g 256 300 150 150; Save $c[0] $g "raise"
# tilt: arms out, leaning right, curved arrow
$c = New-Canvas; $g = $c[1]; Figure $g 256 300 95 95 22; Arrow $g 120 110 60 180 $yellow; Save $c[0] $g "tilt"
# duck: crouched figure, down arrow
$c = New-Canvas; $g = $c[1]; Figure $g 230 330 60 60 0 1 $true; Arrow $g 440 120 440 260 $yellow; Save $c[0] $g "duck"
# jump: figure above the ground line, up arrow
$c = New-Canvas; $g = $c[1]; Line $g 60 480 450 480 $white 18; Figure $g 230 250 140 140 0 0.9; Arrow $g 440 300 440 150 $yellow; Save $c[0] $g "jump"
# star
$c = New-Canvas; $g = $c[1]
$pts = @(); for ($i = 0; $i -lt 10; $i++) { $a = $i / 10 * 2 * [math]::PI - [math]::PI / 2; $r = if ($i % 2 -eq 0) { 210 } else { 95 }; $pts += New-Object System.Drawing.PointF((256 + [math]::Cos($a) * $r), (256 + [math]::Sin($a) * $r)) }
$g.FillPolygon((New-Object System.Drawing.SolidBrush($outline)), $pts); $g.DrawPolygon((Pen $outline 28), $pts)
$g.FillPolygon((New-Object System.Drawing.SolidBrush($yellow)), $pts)
Save $c[0] $g "star"
# ring
$c = New-Canvas; $g = $c[1]; RingShape $g 256 256 190 $orange 46; Save $c[0] $g "ring"
# sun
$c = New-Canvas; $g = $c[1]
for ($i = 0; $i -lt 12; $i++) { $a = $i / 12 * 2 * [math]::PI; Line $g (256 + [math]::Cos($a) * 170) (256 + [math]::Sin($a) * 170) (256 + [math]::Cos($a) * 235) (256 + [math]::Sin($a) * 235) $yellow 24 }
Disc $g 256 256 135 $yellow
Save $c[0] $g "sun"
# bird: body, head, beak, wings up
$c = New-Canvas; $g = $c[1]
Disc $g 256 290 95 $orange; Disc $g 330 200 55 $orange
$beak = @((New-Object System.Drawing.PointF(380, 195)), (New-Object System.Drawing.PointF(440, 210)), (New-Object System.Drawing.PointF(380, 225)))
$g.FillPolygon((New-Object System.Drawing.SolidBrush($outline)), $beak); $g.FillPolygon((New-Object System.Drawing.SolidBrush($yellow)), $beak)
Disc $g 345 190 10 $outline
Line $g 200 270 90 140 $orange 40; Line $g 300 270 400 120 $orange 40
Save $c[0] $g "bird"
# task_water: a glass
$c = New-Canvas; $g = $c[1]
Stroke $g @((New-Object System.Drawing.PointF(150, 120)), (New-Object System.Drawing.PointF(180, 430)), (New-Object System.Drawing.PointF(332, 430)), (New-Object System.Drawing.PointF(362, 120))) $white 24
$g.FillPolygon((New-Object System.Drawing.SolidBrush($blue)), @((New-Object System.Drawing.PointF(165, 250)), (New-Object System.Drawing.PointF(182, 418)), (New-Object System.Drawing.PointF(330, 418)), (New-Object System.Drawing.PointF(347, 250))))
Line $g 150 120 362 120 $white 24
Save $c[0] $g "task_water"
# task_kolam: dot grid with loops
$c = New-Canvas; $g = $c[1]
for ($i = 0; $i -lt 3; $i++) { for ($j = 0; $j -lt 3; $j++) { Disc $g (156 + $i * 100) (156 + $j * 100) 14 $white } }
RingShape $g 256 256 150 $white 20; RingShape $g 256 256 70 $white 20
for ($i = 0; $i -lt 4; $i++) { $a = $i / 4 * 2 * [math]::PI + [math]::PI / 4; RingShape $g (256 + [math]::Cos($a) * 150) (256 + [math]::Sin($a) * 150) 45 $white 20 }
Save $c[0] $g "task_kolam"
# task_hug: two figures leaning in
$c = New-Canvas; $g = $c[1]; Figure $g 190 300 20 110 -12 0.95; Figure $g 330 300 110 20 12 0.95; Save $c[0] $g "task_hug"
# task_stretch: big arms-up figure
$c = New-Canvas; $g = $c[1]; Figure $g 256 300 165 165 0 1.05; Save $c[0] $g "task_stretch"
# task_window: window frame with sky and sun
$c = New-Canvas; $g = $c[1]
$g.FillRectangle((New-Object System.Drawing.SolidBrush($outline)), 86, 86, 340, 340)
$g.FillRectangle((New-Object System.Drawing.SolidBrush($blue)), 100, 100, 312, 312)
Disc $g 330 180 50 $yellow
Line $g 256 100 256 412 $white 22; Line $g 100 256 412 256 $white 22
$g.DrawRectangle((Pen $white 22), 100, 100, 312, 312)
Save $c[0] $g "task_window"
# task_sing: figure with music notes
$c = New-Canvas; $g = $c[1]; Figure $g 200 300 40 120 0 0.9
Line $g 390 120 390 230 $white 20; Disc $g 372 240 26 $white
Line $g 450 160 450 270 $white 20; Disc $g 432 280 26 $white
Save $c[0] $g "task_sing"

Write-Host "Rendered icons to $outRoot\Icons"
