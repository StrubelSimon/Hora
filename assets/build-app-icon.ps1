<#
.SYNOPSIS
    Erzeugt aus der Icon-Vorlage hora-icon.svg die Anwendungsdatei hora.ico.

.DESCRIPTION
    Die Geometrie unten ist eine 1:1-Übertragung von hora-icon.svg (Koordinatensystem 64x64):
    zwei senkrechte Balken als "H" und ein dazwischen liegender Querbalken, der per skewY(-13)
    ansteigt. Die Eckpunkte des Querbalkens sind vorab ausgerechnet (siehe CROSSBAR), weil
    System.Drawing keine Scherung auf abgerundete Rechtecke anwendet.

    Warum eine Kachel hinter dem Zeichen: Die Vorlage zeichnet das "H" in #2C2C2C auf
    durchsichtigem Grund. In der Windows-Taskleiste und im Alt-Tab-Wechsler liegt dieser Ton bei
    dunklem Systemdesign praktisch unsichtbar auf dunklem Grund. Die helle Kachel trägt das
    Zeichen auf jedem Untergrund. Mit -NoTile entsteht stattdessen die durchsichtige Fassung.

    Das erzeugte hora.ico wird eingecheckt; dieses Skript ist die nachvollziehbare Herkunft und
    läuft nicht im Build mit.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File assets\build-app-icon.ps1
#>
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\src\Hora.App\Assets\hora.ico'),
    [switch]$NoTile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Farbwerte der Vorlage. Sie gehören bewusst NICHT zu den Farbtoken in Tokens.Light/Dark.xaml:
# eine .ico-Datei kann nicht mit dem Theme umschalten, sie braucht feste Werte.
$INK       = [System.Drawing.Color]::FromArgb(0xFF, 0x2C, 0x2C, 0x2C)
$ACCENT    = [System.Drawing.Color]::FromArgb(0xFF, 0x00, 0xAA, 0x70)
$TILE      = [System.Drawing.Color]::FromArgb(0xFF, 0xFF, 0xFF, 0xFF)
$TILE_EDGE = 12.0   # Eckradius der Kachel im 64er-Raster, Windows-11-Anmutung

# Querbalken nach skewY(-13) und translate(0,6): y' = (y + 6) + x * tan(-13 Grad)
$CROSSBAR = @(
    (New-Object System.Drawing.PointF 17.0, 30.075),
    (New-Object System.Drawing.PointF 47.0, 23.149),
    (New-Object System.Drawing.PointF 47.0, 30.149),
    (New-Object System.Drawing.PointF 17.0, 37.075)
)

# Die in der .ico enthaltenen Kantenlängen. Windows wählt je nach Anzeigeort die passende.
$SIZES = @(16, 24, 32, 48, 64, 128, 256)

# Ab dieser Kantenlänge wird der Eintrag als PNG abgelegt (seit Windows Vista üblich und
# deutlich kleiner); darunter als unkomprimiertes DIB, das jeder Anzeigepfad sicher liest.
$PNG_THRESHOLD = 128

function New-RoundedPath {
    param([single]$X, [single]$Y, [single]$Width, [single]$Height, [single]$Radius)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $Radius * 2.0
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $Width - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $Width - $d, $Y + $Height - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $Height - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap {
    param([int]$Size, [bool]$WithTile)

    # Vierfach zeichnen und herunterrechnen: die 16er- und 24er-Fassungen werden dadurch
    # spürbar sauberer als bei direktem Zeichnen in Zielgröße.
    $oversample = 4
    $big = New-Object System.Drawing.Bitmap ($Size * $oversample), ($Size * $oversample)
    $g = [System.Drawing.Graphics]::FromImage($big)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)
        $scale = ($Size * $oversample) / 64.0
        $g.ScaleTransform($scale, $scale)

        if ($WithTile) {
            $tilePath = New-RoundedPath 0 0 64 64 $TILE_EDGE
            $tileBrush = New-Object System.Drawing.SolidBrush $TILE
            $g.FillPath($tileBrush, $tilePath)
            $tileBrush.Dispose()
            $tilePath.Dispose()
        }

        $inkBrush = New-Object System.Drawing.SolidBrush $INK
        foreach ($left in @(17, 40)) {
            $bar = New-RoundedPath $left 16 7 32 1
            $g.FillPath($inkBrush, $bar)
            $bar.Dispose()
        }
        $inkBrush.Dispose()

        $accentBrush = New-Object System.Drawing.SolidBrush $ACCENT
        $g.FillPolygon($accentBrush, $CROSSBAR)
        $accentBrush.Dispose()
    }
    finally {
        $g.Dispose()
    }

    $target = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gt = [System.Drawing.Graphics]::FromImage($target)
    try {
        $gt.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $gt.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $gt.Clear([System.Drawing.Color]::Transparent)
        $gt.DrawImage($big, 0, 0, $Size, $Size)
    }
    finally {
        $gt.Dispose()
        $big.Dispose()
    }
    return $target
}

function ConvertTo-PngBytes {
    param([System.Drawing.Bitmap]$Bitmap)

    $stream = New-Object System.IO.MemoryStream
    try {
        $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        $stream.Dispose()
    }
}

function ConvertTo-DibBytes {
    param([System.Drawing.Bitmap]$Bitmap)

    $w = $Bitmap.Width
    $h = $Bitmap.Height
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixels = New-Object byte[] ($data.Stride * $h)
    try {
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    }
    finally {
        $Bitmap.UnlockBits($data)
    }

    # AND-Maske: 1 Bit je Bildpunkt, Zeilen auf 4 Byte aufgefüllt. Bleibt vollständig 0,
    # die Durchsichtigkeit steckt im Alphakanal der 32-Bit-Daten.
    $maskStride = [int][Math]::Floor(($w + 31) / 32) * 4
    $xorSize = $w * 4 * $h
    $andSize = $maskStride * $h

    $out = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $out
    try {
        $writer.Write([uint32]40)        # biSize
        $writer.Write([int32]$w)         # biWidth
        $writer.Write([int32]($h * 2))   # biHeight: Bild plus AND-Maske
        $writer.Write([uint16]1)         # biPlanes
        $writer.Write([uint16]32)        # biBitCount
        $writer.Write([uint32]0)         # biCompression = BI_RGB
        $writer.Write([uint32]($xorSize + $andSize))
        $writer.Write([int32]0); $writer.Write([int32]0)
        $writer.Write([uint32]0); $writer.Write([uint32]0)

        # DIB-Zeilen laufen von unten nach oben.
        for ($y = $h - 1; $y -ge 0; $y--) {
            $writer.Write($pixels, $y * $data.Stride, $w * 4)
        }
        $writer.Write((New-Object byte[] $andSize), 0, $andSize)
        $writer.Flush()
        return $out.ToArray()
    }
    finally {
        $writer.Dispose()
        $out.Dispose()
    }
}

$entries = @(foreach ($size in $SIZES) {
    $bitmap = New-IconBitmap -Size $size -WithTile (-not $NoTile.IsPresent)
    try {
        $bytes = if ($size -ge $PNG_THRESHOLD) { ConvertTo-PngBytes $bitmap } else { ConvertTo-DibBytes $bitmap }
        [pscustomobject]@{ Size = $size; Bytes = $bytes }
    }
    finally {
        $bitmap.Dispose()
    }
})

$resolved = [System.IO.Path]::GetFullPath($OutputPath)
$directory = [System.IO.Path]::GetDirectoryName($resolved)
if (-not (Test-Path $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }

$file = [System.IO.File]::Create($resolved)
$writer = New-Object System.IO.BinaryWriter $file
try {
    $writer.Write([uint16]0)                  # reserviert
    $writer.Write([uint16]1)                  # Typ 1 = Icon
    $writer.Write([uint16]$entries.Count)

    $offset = 6 + (16 * $entries.Count)
    foreach ($entry in $entries) {
        # 256 wird im Verzeichniseintrag als 0 geschrieben, das Feld ist nur ein Byte breit.
        $dim = if ($entry.Size -ge 256) { 0 } else { $entry.Size }
        $writer.Write([byte]$dim)             # Breite
        $writer.Write([byte]$dim)             # Höhe
        $writer.Write([byte]0)                # keine Farbtabelle
        $writer.Write([byte]0)                # reserviert
        $writer.Write([uint16]1)              # Ebenen
        $writer.Write([uint16]32)             # Bit je Bildpunkt
        $writer.Write([uint32]$entry.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $entry.Bytes.Length
    }
    foreach ($entry in $entries) {
        $writer.Write($entry.Bytes, 0, $entry.Bytes.Length)
    }
}
finally {
    $writer.Dispose()
    $file.Dispose()
}

$total = (Get-Item $resolved).Length
Write-Host ("hora.ico geschrieben: {0} ({1} Größen, {2} Byte)" -f $resolved, $entries.Count, $total)
