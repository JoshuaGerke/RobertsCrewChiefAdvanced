# =========================
# Crew Chief Cleanup Script
# =========================
# 
# Crew Chief stores data in various locations, this Powershell script runs the installer
# then opens a File Explorer window for each folder so you can choose to delete folders
# and/or files to completely remove all trace."

param(
    [switch]$Elevated,
    [string]$ExeName = "crewChiefV4.exe",
    [string]$UninstallerPath = "C:\Program Files\MyApp\uninstall.exe",
    [string]$UninstallerArgs = ""
)


function Test-IsElevated {
    $identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# ---- ELEVATION HANDLING (RUN FIRST, BEFORE ANY LOGIC) ----
if (-not (Test-IsElevated)) {

    if (-not $Elevated) {
        $choice = Read-Host "`nSome locations may require admin rights. Relaunch script as admin? (Y/N)"
        if ($choice -match '^[Yy]$') {
		Write-Host "Not elevated. Relaunching as administrator..." -ForegroundColor Yellow

		$argList = @(
		    "-ExecutionPolicy Bypass"
		    "-File `"$PSCommandPath`""
		    "-Elevated"
		)

		# Preserve original arguments if needed
		if ($args.Count -gt 0) {
		    $argList += $args
		}

		Start-Process powershell.exe -ArgumentList $argList -Verb RunAs
		exit
	}
    }
    else {
        Write-Error "Failed to elevate privileges."
        exit
    }
}

# ---- FROM HERE DOWN: RUNS ONLY ONCE, ELEVATED ----
Write-Host "Running with administrator privileges." -ForegroundColor Green
Write-Host ""

Write-Host "Crew Chief stores data in various locations, this Powershell script runs the installer then opens a File Explorer window for each folder so you can choose to delete folders and/or files to completely remove all trace."
Write-Host ""

# ---- CONFIGURABLE DATA LOCATIONS ----
# Designer-defined list of folders and explanations
$dataLocations = @(
#    @{
#        Path = "$env:APPDATA\MyApp"
#        Description = "User-specific settings, preferences, and cached session data."
#        RequiresAdmin = $false
#    },
    @{
        Path = "$env:LOCALAPPDATA\Britton_IT_Ltd"
        Description = "Property settings used by Crew Chief (also for older versions if you installed several)."
        RequiresAdmin = $true
    },
    @{
        Path = "$env:LOCALAPPDATA\CrewChiefV4"
        Description = "Sound files (around 2GB) and splash image."
        RequiresAdmin = $true
    },
    @{
        Path = Join-Path ([Environment]::GetFolderPath("MyDocuments")) "CrewChiefV4"
        Description = "Debug logs, Profiles and other JSON config files."
        RequiresAdmin = $false
    }
)

# ---- FUNCTION: Relaunch as Admin if Needed ----
function Ensure-Admin {
    $isElevated = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

    if (-not $isElevated) {
        Write-Host "Restarting with administrator privileges..."
        Start-Process powershell `
            "-ExecutionPolicy Bypass -File `"$PSCommandPath`"" `
            -Verb RunAs
        exit
    }
}


# ---- STEP 2: SEARCH UNINSTALL REGISTRY KEYS ----
$registryPaths = @(
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*"
)

$uninstallEntry = $null

foreach ($regPath in $registryPaths) {
    $entries = Get-ItemProperty $regPath -ErrorAction SilentlyContinue

    foreach ($entry in $entries) {
        if ($entry.DisplayName -and $entry.DisplayName -match "CrewChiefV4") {
            $uninstallEntry = $entry
            break
        }
    }

    if ($uninstallEntry) { break }
}

# ---- STEP 3: EXECUTE UNINSTALL ----
if ($uninstallEntry) {
    Write-Host "`nFound uninstall entry:" -ForegroundColor Cyan
    Write-Host "Name: $($uninstallEntry.DisplayName)"
    Write-Host "Command: $($uninstallEntry.UninstallString)`n"

    $choice = Read-Host "Run uninstaller now? (Y/N)"
    if ($choice -match '^[Yy]$') {

        $cmd = $uninstallEntry.UninstallString

        # Some uninstall strings are quoted with arguments
        if ($cmd.StartsWith('"')) {
            $exe = $cmd.Split('"')[1]
            $args = $cmd.Substring($exe.Length + 2).Trim()
        } else {
            $parts = $cmd.Split(" ", 2)
            $exe = $parts[0]
            $args = if ($parts.Count -gt 1) { $parts[1] } else { "" }
        }

        Write-Host "Launching uninstaller..." -ForegroundColor Yellow
        Start-Process -FilePath $exe -ArgumentList $args -Wait
        Write-Host "Uninstall completed." -ForegroundColor Green
    }
} else {
    Write-Warning "No uninstall entry found in registry."
}

# ---- STEP 4: PROCESS DATA LOCATIONS ----
foreach ($entry in $dataLocations) {
    $path = $entry.Path
    $desc = $entry.Description
    $admin = $entry.RequiresAdmin

    if (Test-Path $path) {
        Write-Host "`n====================================" -ForegroundColor Yellow
        Write-Host "Location: $path" -ForegroundColor White
        Write-Host "Description: $desc" -ForegroundColor Gray
        Write-Host "====================================" -ForegroundColor Yellow

        # Prompt user before opening
        $choice = Read-Host "Open this location in Explorer? (Y/N)"
        if ($choice -match '^[Yy]$') {

            if ($admin) {
                Write-Host "Opening with admin privileges..."
                Start-Process "explorer.exe" $path -Verb RunAs
            } else {
                Start-Process "explorer.exe" $path
            }
        }
    } else {
        Write-Host "`n[Skipped] $path does not exist." -ForegroundColor DarkGray
    }
}

Write-Host "`nCleanup assistant complete." -ForegroundColor Green