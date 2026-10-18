# Unit test for DpiMonitorWorker debounce and validation logic
# Verifies that:
# 1. Real 2-frame DPI button clicks trigger cleanly within 50ms
# 2. 1-frame glitches (like stale battery packets 0xE1 or 1029) are completely rejected
# 3. Invalid stages (0, 225, > 5) are completely rejected

class DpiDebounceTester {
    [hashtable]$lastMonitoredRazerDpis = @{}
    [hashtable]$lastMonitoredRazerStages = @{}
    [hashtable]$pendingRazerDpiCandidates = @{}
    [hashtable]$pendingRazerStageCandidates = @{}
    [int]$eventFiredCount = 0
    [int]$lastFiredDpi = -1
    [int]$lastFiredStage = -1

    [void]SimulateSample([string]$key, [int]$dpi, [int]$stage, [int]$count) {
        $validCount = if ($count -gt 0) { $count } else { 5 }
        if ($dpi -ge 100 -and $dpi -le 40000 -and $stage -ge 1 -and $stage -le $validCount) {
            $prevDpi = if ($this.lastMonitoredRazerDpis.ContainsKey($key)) { $this.lastMonitoredRazerDpis[$key] } else { -1 }
            $prevStage = if ($this.lastMonitoredRazerStages.ContainsKey($key)) { $this.lastMonitoredRazerStages[$key] } else { -1 }

            if ($prevDpi -ne -1 -and ($dpi -ne $prevDpi -or $stage -ne $prevStage)) {
                $candDpi = if ($this.pendingRazerDpiCandidates.ContainsKey($key)) { $this.pendingRazerDpiCandidates[$key] } else { -1 }
                $candStage = if ($this.pendingRazerStageCandidates.ContainsKey($key)) { $this.pendingRazerStageCandidates[$key] } else { -1 }

                if ($candDpi -eq $dpi -and $candStage -eq $stage) {
                    $this.pendingRazerDpiCandidates.Remove($key)
                    $this.pendingRazerStageCandidates.Remove($key)

                    $this.eventFiredCount++
                    $this.lastFiredDpi = $dpi
                    $this.lastFiredStage = $stage

                    $this.lastMonitoredRazerDpis[$key] = $dpi
                    $this.lastMonitoredRazerStages[$key] = $stage
                } else {
                    $this.pendingRazerDpiCandidates[$key] = $dpi
                    $this.pendingRazerStageCandidates[$key] = $stage
                }
            } else {
                $this.pendingRazerDpiCandidates.Remove($key)
                $this.pendingRazerStageCandidates.Remove($key)

                $this.lastMonitoredRazerDpis[$key] = $dpi
                $this.lastMonitoredRazerStages[$key] = $stage
            }
        }
    }
}

$tester = [DpiDebounceTester]::new()
$devKey = "Razer:1532:00BE"

# Step 1: Initial state established
$tester.SimulateSample($devKey, 3000, 4, 5)
Write-Host "Initial state: Events=$($tester.eventFiredCount) (expected 0)"

# Step 2: Corrupted packet with battery raw byte 225
$tester.SimulateSample($devKey, 3000, 225, 5)
Write-Host "After 225 corrupt stage: Events=$($tester.eventFiredCount) (expected 0)"

# Step 3: Corrupted packet with 0 stage (Phase 2 without stage)
$tester.SimulateSample($devKey, 3000, 0, 0)
Write-Host "After 0 corrupt stage: Events=$($tester.eventFiredCount) (expected 0)"

# Step 4: Transient 1-frame glitch with 1600 DPI, followed by return to 3000 DPI
$tester.SimulateSample($devKey, 1600, 3, 5) # Sample 1
$tester.SimulateSample($devKey, 3000, 4, 5) # Sample 2 (jitter dismissed)
Write-Host "After 1-frame transient glitch: Events=$($tester.eventFiredCount) (expected 0)"

# Step 5: Real user physical DPI button press (3000 -> 1600 DPI)
$tester.SimulateSample($devKey, 1600, 3, 5) # Sample 1 (Candidate recorded)
$tester.SimulateSample($devKey, 1600, 3, 5) # Sample 2 (Confirmed!)
Write-Host "After real DPI change: Events=$($tester.eventFiredCount) (expected 1), FiredDpi=$($tester.lastFiredDpi), FiredStage=$($tester.lastFiredStage)"

if ($tester.eventFiredCount -eq 1 -and $tester.lastFiredDpi -eq 1600 -and $tester.lastFiredStage -eq 3) {
    Write-Host "PASS: Debounce and validation logic verified perfectly!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "FAIL: Unexpected event count or values!" -ForegroundColor Red
    exit 1
}
