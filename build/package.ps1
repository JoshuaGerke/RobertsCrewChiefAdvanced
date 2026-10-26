param(
    [string]$OutDir = "dist/RobertsCrewChiefAdvanced",
    [string]$Configuration = "Release"
)
# Stellt das Programmverzeichnis so zusammen wie der Original-Installer (CrewChiefV4_installer.wixproj, BeforeBuild):
# Build-Ausgabe plus Datenordner und Standarddateien, die Crew Chief neben der exe erwartet.
# Ohne diese Dateien stürzt "Start Crew Chief" ab (fehlende saved_command_macros.json) und es fehlen
# Sprachbefehle, Fahrzeugklassen und Kurvendaten.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'CrewChiefV4'
$bin = Join-Path $src "bin/$Configuration"

if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }
New-Item -ItemType Directory -Force $OutDir | Out-Null
Copy-Item -Recurse "$bin/*" $OutDir
Get-ChildItem $OutDir -Recurse -Include *.pdb | Remove-Item

foreach ($folder in 'plugins', 'Resources') {
    Copy-Item -Recurse -Force (Join-Path $src $folder) (Join-Path $OutDir $folder)
}
$dataFiles = 'carClassData.json', 'controllerConfigurationData.json', 'saved_command_macros.json', 'chart_subscriptions.json',
    'sounds_config.txt', 'speech_recognition_config.txt', 'trackLandmarksData.json', 'mqtt_telemetry.json', 'iracing_formation.json'
foreach ($file in $dataFiles) {
    Copy-Item -Force (Join-Path $src $file) (Join-Path $OutDir $file)
}
New-Item -ItemType Directory -Force (Join-Path $OutDir 'ui_text') | Out-Null
Copy-Item -Force (Join-Path $src 'ui_text/en.txt') (Join-Path $OutDir 'ui_text/en.txt')
foreach ($file in 'help.txt', 'Newtonsoft_Json_licence.txt', 'SharpDX_licence.txt', 'openvr_licence.txt', 'zlib_licence.txt') {
    Copy-Item -Force (Join-Path $root $file) (Join-Path $OutDir $file)
}
Copy-Item -Force (Join-Path $root 'LICENSE') (Join-Path $OutDir 'LICENSE_CrewChief_MIT.txt')
Copy-Item -Force (Join-Path $root 'docs/ANLEITUNG_KI-TEAM.md') (Join-Path $OutDir 'ANLEITUNG_KI-TEAM.md')

# Selbstprüfung: diese Dateien müssen im Paket liegen
foreach ($required in @('CrewChiefV4.exe', 'saved_command_macros.json', 'speech_recognition_config.txt', 'trackLandmarksData.json', 'carClassData.json', 'ui_text/en.txt')) {
    if (-not (Test-Path (Join-Path $OutDir $required))) { throw "Paket unvollständig: $required fehlt" }
}
Write-Output "Paket zusammengestellt: $OutDir"
