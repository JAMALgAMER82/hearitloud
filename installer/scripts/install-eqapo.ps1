<#
.SYNOPSIS
  Bootstrapper helper: downloads Equalizer APO 1.4.2 (if not already installed) and runs its installer silently.

.NOTES
  Invoked by the Inno Setup wrapper (installer/installer.iss). Exit codes:
    0  - EQ APO is now installed (either was already, or installed successfully)
    1  - Download failed across all mirrors
    2  - EQ APO installer exited non-zero

  Equalizer APO requires a reboot after install. We pass /NORESTART here and let
  the Inno Setup wrapper handle the reboot prompt (AlwaysRestart=yes).

  IMPORTANT: SourceForge's filename for the 64-bit installer is
  `EqualizerAPO-x64-1.4.2.exe` (hyphen + x64), NOT `EqualizerAPO64-1.4.2.exe`.
  Using the wrong filename returns a 404 -> HTML redirect page that looks like
  a successful tiny download. Always verify size > 1 MB after download.
#>

param(
  [string[]] $Urls = @(
    'https://sourceforge.net/projects/equalizerapo/files/1.4.2/EqualizerAPO-x64-1.4.2.exe/download',
    'https://downloads.sourceforge.net/project/equalizerapo/1.4.2/EqualizerAPO-x64-1.4.2.exe',
    'https://master.dl.sourceforge.net/project/equalizerapo/1.4.2/EqualizerAPO-x64-1.4.2.exe'
  )
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13

# Detect existing install via registry
$installPath = $null
try {
  $installPath = (Get-ItemProperty -Path 'HKLM:\SOFTWARE\EqualizerAPO' -Name 'InstallPath' -ErrorAction Stop).InstallPath
} catch { }

if ($installPath -and (Test-Path $installPath)) {
  Write-Host "Equalizer APO already installed at $installPath. Skipping download."
  exit 0
}

$temp = Join-Path $env:TEMP 'HearItLoud-bootstrap'
New-Item -ItemType Directory -Force -Path $temp | Out-Null
$installer = Join-Path $temp 'EqualizerAPO-x64-1.4.2.exe'

function Try-Download($url) {
  try {
    Write-Host "Downloading Equalizer APO from $url ..."
    Invoke-WebRequest -Uri $url -OutFile $installer -UseBasicParsing `
      -UserAgent 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
    $info = Get-Item $installer
    if ($info.Length -lt 1MB) {
      Write-Host "  Downloaded $($info.Length) bytes - too small (probably an HTML redirect). Skipping."
      Remove-Item $installer -Force -ErrorAction SilentlyContinue
      return $false
    }
    Write-Host "  Downloaded $([math]::Round($info.Length / 1MB, 2)) MB."
    return $true
  } catch {
    Write-Host "  Download failed: $($_.Exception.Message)"
    return $false
  }
}

$downloaded = $false
foreach ($u in $Urls) {
  if (Try-Download $u) { $downloaded = $true; break }
}

if (-not $downloaded) {
  Write-Host ""
  Write-Host "ERROR: Could not download Equalizer APO from any mirror."
  Write-Host "Please install it manually from https://equalizerapo.com and re-run this installer."
  Write-Error "Equalizer APO download failed."
  exit 1
}

Write-Host "Running Equalizer APO installer (silent)..."
$proc = Start-Process -FilePath $installer -ArgumentList '/SILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait -PassThru
if ($proc.ExitCode -ne 0) {
  Write-Error "Equalizer APO installer exited with code $($proc.ExitCode)."
  exit 2
}

Write-Host "Equalizer APO installed. A reboot is required for it to take effect."
exit 0
