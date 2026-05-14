<#
.SYNOPSIS
  Bootstrapper helper: downloads Equalizer APO 1.4.2 (if not already installed) and runs its installer silently.

.NOTES
  Invoked by the Inno Setup wrapper (installer/installer.iss). Exit codes:
    0  - EQ APO is now installed (either was already, or installed successfully)
    1  - Download failed
    2  - EQ APO installer exited non-zero
    3  - User cancelled

  Equalizer APO requires a reboot after install; this script returns 0 and the
  wrapper installer marks AlwaysRestart=yes so Inno Setup handles the reboot prompt.
#>

param(
  [string]$DownloadUrl = 'https://downloads.sourceforge.net/project/equalizerapo/1.4.2/EqualizerAPO64-1.4.2.exe',
  [string]$BackupUrl   = 'https://sourceforge.net/projects/equalizerapo/files/1.4.2/EqualizerAPO64-1.4.2.exe/download'
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Detect existing install via registry
$installPath = $null
try {
  $installPath = (Get-ItemProperty -Path 'HKLM:\SOFTWARE\EqualizerAPO' -Name 'InstallPath' -ErrorAction Stop).InstallPath
} catch { }

if ($installPath -and (Test-Path $installPath)) {
  Write-Host "Equalizer APO already installed at $installPath. Skipping download."
  exit 0
}

$temp = Join-Path $env:TEMP 'WarzoneEQ-bootstrap'
New-Item -ItemType Directory -Force -Path $temp | Out-Null
$installer = Join-Path $temp 'EqualizerAPO64-1.4.2.exe'

function Try-Download($url) {
  try {
    Write-Host "Downloading Equalizer APO from $url ..."
    Invoke-WebRequest -Uri $url -OutFile $installer -UseBasicParsing -UserAgent 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'
    $info = Get-Item $installer
    if ($info.Length -lt 1MB) {
      Write-Host "  Downloaded $($info.Length) bytes - too small, treating as redirect/error."
      return $false
    }
    return $true
  } catch {
    Write-Host "  Download failed: $($_.Exception.Message)"
    return $false
  }
}

if (-not (Try-Download $DownloadUrl)) {
  if (-not (Try-Download $BackupUrl)) {
    Write-Error "Could not download Equalizer APO. Check your internet connection or download manually from https://equalizerapo.com"
    exit 1
  }
}

Write-Host "Running Equalizer APO installer (silent)..."
$proc = Start-Process -FilePath $installer -ArgumentList '/SILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait -PassThru
if ($proc.ExitCode -ne 0) {
  Write-Error "Equalizer APO installer exited with code $($proc.ExitCode)."
  exit 2
}

Write-Host "Equalizer APO installed. A reboot is required for it to take effect."
exit 0
