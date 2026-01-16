# ShareX vs XerahS Capture Comparison Test
# This script captures the same region using both tools nearly simultaneously

param(
    [int]$Width = 500,
    [int]$Height = 500,
    [int]$Seed = 0,
    [int]$X = -1,
    [int]$Y = -1,
    [string]$OutputDir = "C:\Users\liveu\OneDrive\Documents\XerahS\CaptureTroubleshooting\ShareXvXerahS",
    [int]$Tolerance = 0
)

# Use timestamp-based seed if not provided
if ($Seed -eq 0) {
    $Seed = [int](Get-Date -UFormat %s)
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$iterDir = Join-Path $OutputDir "iter_$timestamp"
New-Item -ItemType Directory -Force -Path $iterDir | Out-Null

Write-Host "=== ShareX vs XerahS A/B Test ===" -ForegroundColor Cyan
Write-Host "Seed: $Seed"
Write-Host "Size: ${Width}x${Height}"
Write-Host "Output: $iterDir"
Write-Host ""

# Step 1: Capture with ShareX (this establishes the coordinates)
Write-Host "Step 1: Capturing with ShareX..." -ForegroundColor Yellow
$sharexCli = "C:\Users\liveu\source\repos\McoreD\ShareX\ShareX.CLI\bin\Debug\win-x64\ShareX.CLI.exe"

if ($X -ge 0 -and $Y -ge 0) {
    & $sharexCli capture --outputDir $iterDir --width $Width --height $Height --x $X --y $Y --log
} else {
    & $sharexCli capture --outputDir $iterDir --width $Width --height $Height --seed $Seed --log
}

# Find the ShareX capture files
$sharexCapture = Get-ChildItem $iterDir -Filter "sharex_capture_*.png" | Select-Object -First 1
$sharexMetadata = Get-ChildItem $iterDir -Filter "sharex_capture_*.json" | Select-Object -First 1

if (-not $sharexCapture) {
    Write-Host "ERROR: ShareX capture failed!" -ForegroundColor Red
    exit 2
}

# Parse the rectangle from ShareX metadata
$metadata = Get-Content $sharexMetadata.FullName | ConvertFrom-Json
$rectX = $metadata.Rectangle.X
$rectY = $metadata.Rectangle.Y
$rectW = $metadata.Rectangle.Width
$rectH = $metadata.Rectangle.Height

Write-Host "ShareX captured: $($sharexCapture.Name)"
Write-Host "Rectangle: $rectX,$rectY,$rectW,$rectH"
Write-Host ""

# Step 2: Immediately capture the same region with XerahS
Write-Host "Step 2: Capturing same region with XerahS..." -ForegroundColor Yellow
$xerahsCli = "C:\Users\liveu\source\repos\ShareX Team\ShareX.Avalonia\src\XerahS.CLI\bin\Debug\net10.0-windows10.0.26100.0\xerahs.exe"

& $xerahsCli compare-capture --baseline $sharexCapture.FullName --region "$rectX,$rectY,$rectW,$rectH" --output-dir $iterDir --tolerance $Tolerance

$exitCode = $LASTEXITCODE
Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan
Write-Host "Results directory: $iterDir"
Write-Host "Exit code: $exitCode"

exit $exitCode
