param([string]$Path)
if (-not (Test-Path $Path)) { Write-Error "Path not found: $Path"; exit 1 }
$bytes = [System.IO.File]::ReadAllBytes($Path)
if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    Write-Host "Already has BOM: $Path"
    exit 0
}
$text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
$utf8WithBom = New-Object System.Text.UTF8Encoding($true)
[System.IO.File]::WriteAllText($Path, $text, $utf8WithBom)
Write-Host "Added UTF-8 BOM: $Path"
exit 0
