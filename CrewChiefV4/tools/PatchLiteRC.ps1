# Copy of Patch.ps1, just don't patch sounds.  Copy because I can't figure out how to pass switch to elevated script.

# Patches CC install directory with newest Release Candidate files.  Not complete, can be grown and improved to cleanup stuff, settings, sounds etc.
# For some unknown reason does not work from git powershell prompt, so either run from Explorer, ISE or elevated PS prompt.
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {   
    $arguments = "& '" + $myinvocation.mycommand.definition + "'"
    Start-Process powershell -Verb RunAs -ArgumentList $arguments
    break
}

function GetScriptDirectory {
  $invocation = (Get-Variable MyInvocation -Scope 1).Value
  Split-Path $invocation.MyCommand.Path
}

function OverwriteFile($from, $to) {

    Write-Host "Overwriting "  $to " with " $from
    Copy-Item $from -Destination $to -Force
    
    echo ""
}

function MirrorDirectory($from, $to) {

    Write-Host "Mirroring " $from " with " $to
    
    $cmdArgs = @("$from","$to",'/MIR')
    robocopy @cmdArgs

    echo ""
}

$toolsPath = GetScriptDirectory
$releaseBinPath = $toolsPath + "\..\bin\Release\"
$rootPath = $toolsPath + "\..\"
$ccLayoutMainPath = ${env:ProgramFiles(x86)} + "\Britton IT Ltd\CrewChiefRC\"

echo "Patching RC CC install"
echo ""

OverwriteFile $releaseBinPath\"CrewChiefV4.exe" $releaseBinPath\"CrewChiefRC.exe"
OverwriteFile $releaseBinPath\"CrewChiefV4.pdb" $releaseBinPath\"CrewChiefRC.pdb"
OverwriteFile $releaseBinPath\"CrewChiefV4.exe.config" $releaseBinPath\"CrewChiefRC.exe.config"
OverwriteFile $releaseBinPath\"CrewChiefRC.exe" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"CrewChiefRC.pdb" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"CrewChiefRC.exe.config" $ccLayoutMainPath
OverwriteFile $rootPath\"ui_text\en.txt" $ccLayoutMainPath\ui_text\
OverwriteFile $rootPath\"carClassData.json" $ccLayoutMainPath
OverwriteFile $rootPath\"trackLandmarksData.json" $ccLayoutMainPath
OverwriteFile $rootPath\"controllerConfigurationData.json" $ccLayoutMainPath
OverwriteFile $rootPath\"saved_command_macros.json" $ccLayoutMainPath
OverwriteFile $rootPath\"speech_recognition_config.txt" $ccLayoutMainPath
OverwriteFile $rootPath\"chart_subscriptions.json" $ccLayoutMainPath
OverwriteFile $rootPath\"sounds_config.txt" $ccLayoutMainPath
OverwriteFile $rootPath\"iracing_formation.json" $ccLayoutMainPath

# Update binary dependencies.
OverwriteFile $releaseBinPath\"AutoUpdater.NET.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"MathNet.Numerics.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"NAudio.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"NAudio.Core.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"NAudio.Asio.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"NAudio.Midi.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"NAudio.Wasapi.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"NAudio.WinForms.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"NAudio.WinMM.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"Newtonsoft.Json.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"SharpDX.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"SharpDX.DirectInput.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"SharpDX.Direct2D1.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"SharpDX.Direct3D11.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"SharpDX.DXGI.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"SharpDX.Mathematics.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"websocket-sharp.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"GameOverlay.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"openvr_api.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"WindowsInput.dll" $ccLayoutMainPath
OverwriteFile $releaseBinPath\"MQTTnet.dll" $ccLayoutMainPath

echo "Press Enter to finish..."

Read-Host