param(
    [string]$OutDir = "dist/RobertsCrewChiefAdvanced",
    [string]$Configuration = "Release",
    [switch]$PublicDistribution,
    [string]$Version = "v0.9-alpha3"
)
# Stellt das Programmverzeichnis so zusammen wie der Original-Installer (CrewChiefV4_installer.wixproj, BeforeBuild):
# Build-Ausgabe plus Datenordner und Standarddateien, die Crew Chief neben der exe erwartet.
# Ohne diese Dateien stürzt "Start Crew Chief" ab (fehlende saved_command_macros.json) und es fehlen
# Sprachbefehle, Fahrzeugklassen und Kurvendaten.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'CrewChiefV4'
$bin = Join-Path $src "bin/$Configuration"

$OutDir = [IO.Path]::GetFullPath($OutDir)
if (Test-Path -LiteralPath $OutDir) { throw "Ziel existiert bereits; zum Schutz vorhandener Dateien einen neuen Paketordner angeben: $OutDir" }
New-Item -ItemType Directory -Path $OutDir | Out-Null
# Production allowlist: never sweep a developer's bin directory into a public archive.
foreach ($relative in Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-files.txt')) {
    if ([string]::IsNullOrWhiteSpace($relative) -or $relative.StartsWith('#')) { continue }
    $file = [IO.Path]::GetFullPath((Join-Path $bin $relative))
    if (-not $file.StartsWith([IO.Path]::GetFullPath($bin) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Ungültiger Runtime-Pfad" }
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Runtime fehlt: $relative" }
    $destination = Join-Path $OutDir $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath $file -Destination $destination
}
$ui = Join-Path $OutDir 'AiTeam/ui'
New-Item -ItemType Directory -Force -Path (Join-Path $ui 'fonts') | Out-Null
foreach ($name in 'rrca.html','rrca.css','rrca.js','rrca.i18n.js','logo.jpg') {
    Copy-Item -LiteralPath (Join-Path $src "AiTeam/ui/$name") -Destination (Join-Path $ui $name)
}
foreach ($font in Get-ChildItem -LiteralPath (Join-Path $src 'AiTeam/ui/fonts') -File) {
    if ($font.Extension -eq '.woff2' -or $font.Name -like '*OFL.txt') { Copy-Item -LiteralPath $font.FullName -Destination (Join-Path $ui "fonts/$($font.Name)") }
}
Copy-Item -LiteralPath (Join-Path $src 'AiTeam/Rennfunk.de.json') -Destination (Join-Path $OutDir 'Rennfunk.de.json')
if ($PublicDistribution) {
    $config = Join-Path $OutDir 'CrewChiefV4.exe.config'
    $text = [IO.File]::ReadAllText($config)
    $pattern = '(<add\s+key="RRCAStandaloneData"\s+value=")false("\s*/>)'
    if ([regex]::Matches($text,$pattern).Count -ne 1) { throw 'Standalone-Schalter fehlt oder ist nicht eindeutig' }
    $text = [regex]::Replace($text,$pattern,'${1}true${2}')
    [IO.File]::WriteAllText($config,$text,[Text.UTF8Encoding]::new($false))
}

foreach ($folder in 'plugins', 'Resources') {
    Copy-Item -Recurse -Force (Join-Path $src $folder) (Join-Path $OutDir $folder)
}
$dataFiles = 'carClassData.json', 'controllerConfigurationData.json', 'saved_command_macros.json', 'chart_subscriptions.json',
    'sounds_config.txt', 'speech_recognition_config.txt', 'trackLandmarksData.json', 'mqtt_telemetry.json', 'iracing_formation.json'
foreach ($file in $dataFiles) {
    Copy-Item -Force (Join-Path $src $file) (Join-Path $OutDir $file)
}
New-Item -ItemType Directory -Force (Join-Path $OutDir 'ui_text') | Out-Null
# Alle Oberflächensprachen von Crew Chief mitliefern, nicht nur Englisch
foreach ($uiText in Get-ChildItem -LiteralPath (Join-Path $src 'ui_text') -Filter '*.txt' -File) {
    Copy-Item -Force -LiteralPath $uiText.FullName -Destination (Join-Path $OutDir "ui_text/$($uiText.Name)")
}
foreach ($file in 'help.txt', 'Newtonsoft_Json_licence.txt', 'SharpDX_licence.txt', 'openvr_licence.txt', 'zlib_licence.txt') {
    Copy-Item -Force (Join-Path $root $file) (Join-Path $OutDir $file)
}
Copy-Item -Force (Join-Path $root 'LICENSE') (Join-Path $OutDir 'LICENSE_CrewChief_MIT.txt')
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $OutDir 'README_EN.md')
foreach ($note in 'START_HIER_DE.md','README_DE.md','README_ES.md','HINWEISE_UND_DANK.md','NOTICES_EN.md','NOTICES_ES.md','THIRD_PARTY_NOTICES.md') {
    Copy-Item -LiteralPath (Join-Path $root $note) -Destination (Join-Path $OutDir $note)
}
Copy-Item -LiteralPath (Join-Path $root 'ThirdPartyLicenses') -Destination (Join-Path $OutDir 'ThirdPartyLicenses') -Recurse
# Bebilderte Anleitungen (DE/EN/ES) liegen direkt neben der exe
$guides = 'ANLEITUNG_RRCA_DE.pdf','GUIDE_RRCA_EN.pdf','GUIA_RRCA_ES.pdf'
foreach ($guide in $guides) {
    Copy-Item -LiteralPath (Join-Path $root "docs/anleitung/$guide") -Destination (Join-Path $OutDir $guide)
}
# Selbstprüfung: diese Dateien müssen im Paket liegen
# Microsoft.Speech.dll darf nicht ins Paket (Weitergaberecht ungeklärt); Crew Chief nutzt dann die Windows-Spracherkennung
if (Get-ChildItem -LiteralPath $OutDir -Recurse -Filter 'Microsoft.Speech.dll' -File) { throw 'Microsoft.Speech.dll gehört nicht ins Paket' }
foreach ($required in @('CrewChiefV4.exe', 'saved_command_macros.json', 'speech_recognition_config.txt', 'trackLandmarksData.json', 'carClassData.json', 'ui_text/en.txt', 'AiTeam/ui/rrca.i18n.js', 'Rennfunk.de.json', 'README_DE.md', 'README_EN.md') + $guides) {
    if (-not (Test-Path (Join-Path $OutDir $required))) { throw "Paket unvollständig: $required fehlt" }
}
# Generated proof contains relative names only. Credentials and personal runtime data are never package inputs.
$files = @(Get-ChildItem -LiteralPath $OutDir -Recurse -File)
foreach ($file in $files) {
    if ($file.Extension -in '.pdb','.wav','.bin','.zip','.log','.jsonl','.pcm' -or $file.Name -like '.env*' -or
        ($file.Extension -eq '.exe' -and $file.Name -ne 'CrewChiefV4.exe')) { throw "Unzulässige Paketdatei: $($file.Name)" }
}
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[a-f0-9]{40}$') { throw 'Quellcommit nicht ermittelbar' }
[IO.File]::WriteAllText((Join-Path $OutDir 'VERSION.txt'),"Version: $Version`r`nCommit: $commit`r`nStandaloneData: $([bool]$PublicDistribution)`r`n",[Text.UTF8Encoding]::new($false))
$manifest = @(Get-ChildItem -LiteralPath $OutDir -Recurse -File | ForEach-Object {
    [ordered]@{path=$_.FullName.Substring($OutDir.Length+1).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
ConvertTo-Json -InputObject $manifest -Depth 3 | Set-Content -LiteralPath (Join-Path $OutDir 'PAKET_MANIFEST.json') -Encoding utf8
Write-Output "Paket zusammengestellt: $OutDir; Runtime-Positivliste, keine Replay-/Testprogramme."
