<#
.SYNOPSIS
  Post-install: runs WarzoneEQ.exe --auto so the config is in place immediately.
  Best-effort: failures are non-fatal because the user can run it manually.
#>

param(
  [Parameter(Mandatory = $true)][string]$ExePath
)

if (-not (Test-Path $ExePath)) {
  Write-Host "HearItLoud.exe not found at $ExePath; skipping auto-tune."
  exit 0
}

try {
  Write-Host "Running auto-detect and install..."
  & $ExePath --auto 2>&1 | ForEach-Object { Write-Host "  $_" }
} catch {
  Write-Host "Auto-tune step failed (non-fatal): $($_.Exception.Message)"
}
exit 0
