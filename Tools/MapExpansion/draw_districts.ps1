# 구역 블록(단계별 칸) · 부지 칸을 전체 지도 위에 그린 확인용 그림 (구역·이사 기획). 단계 번호는 블록이 커지는 차례 (1 = 공터 쪽).
# 데이터는 Unity가 만든 에셋(Assets/Scriptable Obejects/Settlement/Districts/District_*.asset)을 그대로 읽는다.
#   powershell -ExecutionPolicy Bypass -File Tools/MapExpansion/draw_districts.ps1 [-Out Docs/Images/구역_배치안.png] [-Scale 0.25]
# 좌표: 꾸미기 격자 세이브 좌표 (x, y) → 지도 픽셀 (40x + 3061, 3466 − 40y)  (Plaza_WorldMap.jpg, 4072×3984)
param(
    [string]$Out = "Docs/Images/구역_배치안.png",
    [double]$Scale = 0.25
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Add-Type -AssemblyName System.Drawing

function Read-Block([string]$path) {
    $lines = [System.IO.File]::ReadAllLines($path)
    $block = @{ id = ""; name = ""; fixed = 0; suggested = 0; areas = @(); slots = @(); stage = 0 }
    $rect = $null; $inSlots = $false; $slotId = $null
    foreach ($line in $lines) {
        if ($line -match '^\s*_blockId: (.+)$') { $block.id = $Matches[1] }
        elseif ($line -match '^\s*_displayName: "(.*)"$') { $block.name = [regex]::Unescape($Matches[1]) }
        elseif ($line -match '^\s*_displayName: (.+)$') { $block.name = $Matches[1] }
        elseif ($line -match '^\s*_fixedType: (\d+)') { $block.fixed = [int]$Matches[1] }
        elseif ($line -match '^\s*_suggestedType: (\d+)') { $block.suggested = [int]$Matches[1] }
        elseif ($line -match '^\s*_slots:') { $inSlots = $true }
        elseif ($line -match '^\s*- _development:') { $block.stage++ }
        elseif ($line -match '^\s*- _slotId: (.+)$') { $slotId = $Matches[1] }
        elseif ($line -match '^\s*-? ?x: (-?\d+)') { $rect = @{ x = [int]$Matches[1] } }
        elseif ($line -match '^\s*y: (-?\d+)') { $rect.y = [int]$Matches[1] }
        elseif ($line -match '^\s*width: (\d+)') { $rect.w = [int]$Matches[1] }
        elseif ($line -match '^\s*height: (\d+)') {
            $rect.h = [int]$Matches[1]
            if ($inSlots) { $rect.id = $slotId; $block.slots += , $rect } else { $rect.stage = $block.stage; $block.areas += , $rect }
        }
    }
    return $block
}

$names = @{ 1 = "중앙 (행정)"; 2 = "주거"; 3 = "상업"; 4 = "생산"; 5 = "농업" }
$colors = @{ 1 = [System.Drawing.Color]::MediumPurple; 2 = [System.Drawing.Color]::Tomato; 3 = [System.Drawing.Color]::Gold
    4 = [System.Drawing.Color]::DodgerBlue; 5 = [System.Drawing.Color]::LimeGreen }

$src = [System.Drawing.Image]::FromFile((Join-Path $root "Assets/Art/Plaza/Plaza_WorldMap.jpg"))
$w = [int]($src.Width * $Scale); $h = [int]($src.Height * $Scale)
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = "HighQualityBicubic"; $g.SmoothingMode = "AntiAlias"; $g.TextRenderingHint = "AntiAliasGridFit"
$g.DrawImage($src, 0, 0, $w, $h)
function MX([double]$x) { return [float]((40 * $x + 3061) * $Scale) }
function MY([double]$y) { return [float]((3466 - 40 * $y) * $Scale) }

$font = New-Object System.Drawing.Font "Malgun Gothic", 13, ([System.Drawing.FontStyle]::Bold)
$small = New-Object System.Drawing.Font "Malgun Gothic", 9
$label = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(185, 20, 20, 20))
$total = 0
$blocks = @()
foreach ($file in Get-ChildItem (Join-Path $root "Assets/Scriptable Obejects/Settlement/Districts") -Filter "District_*.asset") {
    $blocks += , (Read-Block $file.FullName)
}
# 블록 영역 (그림 좌표) — 이름표가 다른 블록을 가리지 않게 고를 때 씀
$areaRects = @()
foreach ($b in $blocks) {
    foreach ($a in $b.areas) {
        $areaRects += , (New-Object System.Drawing.RectangleF (MX $a.x), (MY ($a.y + $a.h)), ((MX ($a.x + $a.w)) - (MX $a.x)), ((MY $a.y) - (MY ($a.y + $a.h))))
    }
}
$script:labelRects = @()
function Label-Rect($a, [float]$bw, [float]$bh, [bool]$inside) {
    $cx = ((MX $a.x) + (MX ($a.x + $a.w))) / 2
    $top = MY ($a.y + $a.h); $bottom = MY $a.y
    $candidates = if ($inside) { @($top + 5) } else { @(($top - $bh - 3), ($bottom + 3), ($top + 5)) }
    foreach ($y in $candidates) {
        $r = New-Object System.Drawing.RectangleF ($cx - $bw / 2), $y, $bw, $bh
        if ($y -lt 0 -or $y + $bh -gt $h) { continue }
        $hit = $false
        foreach ($o in ($areaRects + $script:labelRects)) { if ($o.IntersectsWith($r)) { $hit = $true; break } }
        if (-not $hit -or $y -eq $candidates[-1]) { return $r }
    }
    return New-Object System.Drawing.RectangleF ($cx - $bw / 2), ($top + 5), $bw, $bh
}
foreach ($b in $blocks) {
    $type = if ($b.fixed -ne 0) { $b.fixed } else { $b.suggested }
    $c = $colors[$type]
    foreach ($a in $b.areas) {
        $x0 = MX $a.x; $x1 = MX ($a.x + $a.w); $y0 = MY ($a.y + $a.h); $y1 = MY $a.y
        $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(55, $c))), $x0, $y0, $x1 - $x0, $y1 - $y0)
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(235, $c)), 3
        if ($b.fixed -eq 0) { $pen.DashStyle = "Dash" }
        $g.DrawRectangle($pen, $x0, $y0, $x1 - $x0, $y1 - $y0)
    }
    foreach ($s in $b.slots) {
        $x0 = MX $s.x; $x1 = MX ($s.x + $s.w); $y0 = MY ($s.y + $s.h); $y1 = MY $s.y
        $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(130, $c))), $x0, $y0, $x1 - $x0, $y1 - $y0)
        $g.DrawRectangle((New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 2), $x0, $y0, $x1 - $x0, $y1 - $y0)
        $total++
    }
    # 이름표 자리: 모든 단계를 감싸는 칸
    $minX = ($b.areas | ForEach-Object { $_.x } | Measure-Object -Minimum).Minimum
    $minY = ($b.areas | ForEach-Object { $_.y } | Measure-Object -Minimum).Minimum
    $maxX = ($b.areas | ForEach-Object { $_.x + $_.w } | Measure-Object -Maximum).Maximum
    $maxY = ($b.areas | ForEach-Object { $_.y + $_.h } | Measure-Object -Maximum).Maximum
    $a = @{ x = $minX; y = $minY; w = $maxX - $minX; h = $maxY - $minY }
    if ($b.fixed -eq 0) {
        foreach ($st in $b.areas) {
            $g.DrawString("$($st.stage)", $small, [System.Drawing.Brushes]::White, (MX $st.x) + 3, (MY ($st.y + $st.h)) + 2)
        }
    }
    $title = if ($b.fixed -ne 0) { "$($b.name) = 새 시작" } else { "$($names[$type]) (예시)" }
    $sub = if ($b.fixed -ne 0) { "게시판 → 마을회관 → 성" } else { "$($b.name) · 부지 $($b.slots.Count)칸" }
    $s1 = $g.MeasureString($title, $font); $s2 = $g.MeasureString($sub, $small)
    $bw = [Math]::Max($s1.Width, $s2.Width) + 8
    $r = Label-Rect $a $bw ($s1.Height + $s2.Height + 2) ($b.slots.Count -eq 0)
    $script:labelRects += , $r
    $cx = $r.X + $r.Width / 2
    $g.FillRectangle($label, $r)
    $g.DrawString($title, $font, [System.Drawing.Brushes]::White, $cx - $s1.Width / 2, $r.Y)
    $g.DrawString($sub, $small, [System.Drawing.Brushes]::White, $cx - $s2.Width / 2, $r.Y + $s1.Height)
}

# 지금 시작 위치 (남동쪽 광장) → 2단계에서 선착장 · 바닷가로
$ox0 = MX -0.8; $ox1 = MX 25.3; $oy0 = MY 33.6; $oy1 = MY -4.8
$dash = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(220, 30, 30, 30)), 2
$dash.DashStyle = "Dot"
$g.DrawRectangle($dash, $ox0, $oy0, $ox1 - $ox0, $oy1 - $oy0)
$t = "지금 시작 위치 → 선착장·바닷가"
$st = $g.MeasureString($t, $small)
$g.FillRectangle($label, $ox0 + 4, $oy0 + 4, $st.Width + 6, $st.Height)
$g.DrawString($t, $small, [System.Drawing.Brushes]::White, $ox0 + 7, $oy0 + 4)

$outPath = Join-Path $root $Out
New-Item -ItemType Directory -Force (Split-Path $outPath) | Out-Null
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose(); $src.Dispose()
"saved $outPath ($w x $h, slots $total)"
