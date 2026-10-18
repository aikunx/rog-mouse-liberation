# run_all_tests.ps1
$testFiles = Get-ChildItem tests\test_*.ps1
$allPassed = $true
$summary = @()

foreach ($tf in $testFiles) {
    Write-Host ("[TESTING] " + $tf.Name) -ForegroundColor Cyan
    $proc = Start-Process powershell -ArgumentList "-ExecutionPolicy Bypass -File `"$($tf.FullName)`"" -Wait -PassThru -NoNewWindow
    if ($proc.ExitCode -eq 0) {
        $summary += "[PASS] $($tf.Name)"
        Write-Host " -> OK" -ForegroundColor Green
    } else {
        $allPassed = $false
        $summary += "[FAIL ($($proc.ExitCode))] $($tf.Name)"
        Write-Host " -> FAILED with code $($proc.ExitCode)" -ForegroundColor Red
    }
}

Write-Host "`n================ TEST SUITE SUMMARY ================" -ForegroundColor Yellow
$summary | ForEach-Object { Write-Host $_ }

if (-not $allPassed) {
    exit 1
} else {
    exit 0
}
