using namespace System.Management.Automation.Host
Add-Type -AssemblyName PresentationFramework

function ReleaseOrCandidate
{
    return "V4"

    $msgBoxInput =  [System.Windows.MessageBox]::Show('Release version? (No: Release Candidate)','Crew Chief Build Menu','YesNoCancel','Question')
    switch  ($msgBoxInput) {
        'Yes'
        {
            $variant = "V4"
        }
        'No'
        {
            $variant = "RC"
        }
        'Cancel'
        {
            exit
        }
    } 
    return $variant
}
function InstallerOrDogFood 
{
    $msgBoxInput =  [System.Windows.MessageBox]::Show('Build new installer? (No: Update your local dog food exe file)','Crew Chief Build Menu','YesNoCancel','Question')
    switch  ($msgBoxInput) {
        'Yes'
        {
            $response = "Package a new installer"
        }
        'No'
        {
            $response = "Update your local dog food exe file"
        }
        'Cancel'
        {
            exit
        }
    } 
    return $response
}
function UpdateGUID 
{
    $file = $PSScriptRoot + '..\..\..\CrewChiefV4_installer\Product.wxs'
    $content = Get-Content -Path $file
    $guid = [guid]::NewGuid().ToString().ToUpper()
    Write-Output $guid
    $replace = 'ProductCode = {' + $guid + '}'
    $newContent = $content -replace 'ProductCode *= *\{.*\}', $replace
    $newContent | Set-Content -Path $file

    $msgBoxInput =  [System.Windows.MessageBox]::Show('Change the version string in AssemblyVersion?','Version update','YesNo','Question')

    switch  ($msgBoxInput) {
        'Yes'
        {
            $file = $PSScriptRoot + '\..\Properties\AssemblyInfo.cs'
            Start-Process -Wait notepad $file
            
            $file = $PSScriptRoot + '\..\Properties\version.txt'
            #Start-Process -Wait notepad $file
            
            $file = $PSScriptRoot + '\..\..\auto_update_data_files\primary\auto_update_data.xml'
            Start-Process -Wait notepad $file
            
            $file = $PSScriptRoot + '\..\..\auto_update_data_files\secondary\auto_update_data.xml'
            Start-Process -Wait notepad $file
            
            $file = $PSScriptRoot + '\..\..\auto_update_data_files\gitlab\auto_update_data.xml'
            if (Test-Path -Path $file) {
                 Write-Output "Edit the https://gitlab.com/mr_belowski/CrewChiefV4/-/raw/main/CrewChiefV4_installer/Installs/CrewChiefV4- line too!"
                 Start-Process -Wait notepad $file
            }
        }
    }       
}

function BuildRelease 
{
    # Set the path environment variable to include MSBuild
    $env:Path += ";C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin"
    $env:Path += ";c:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin"
    $env:Path += ";c:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/amd64"
    # Change directory to CrewChiefV4
    $path = $PSScriptRoot + "\.."
    Set-Location -Path $path

    # Build CrewChiefV4.csproj with Release configuration
    Write-Output "Building..."
    $Errors = (msbuild CrewChiefV4.csproj /t:Rebuild /p:Configuration=Release) 
    
    Write-Output $Errors
    $ErrorsLine = $Errors | Select-String "Error\(s\)"
    if (!$ErrorsLine -contains " 0 Error")
    {
        [System.Windows.MessageBox]::Show($ErrorsLine,'Errors in build','OK','Error')
        exit
    }
}
function BuildInstaller
{
    # Insert the version info in the .exe
    $file = $PSScriptRoot + '\..\Properties\version.txt'
    if ($Variant -eq "V4") 
    {
    	$exe = $PSScriptRoot + '\..\bin\Release\CrewChiefV4.exe'
    }
    else
    {
    	$exe = $PSScriptRoot + '\..\bin\Release\CrewChiefRC.exe'
    }
    #Gets overwritten by Wix??? pyi-set_version.exe $file $exe


    # Change directory to CrewChiefV4_installer
    $path = $PSScriptRoot + "\..\..\CrewChiefV4_installer"
    Set-Location -Path $path

    # Build CrewChiefV4_installer.wixproj with Release configuration
    Write-Output "Building installer..."
    $Log = (msbuild CrewChiefV4_installer.wixproj /t:Rebuild /p:Configuration=Release)
    Set-Content -Path "WixLog.txt" -Value $Log
    Write-Output $Log
    $Errors = $Log | Select-String ": error "
 
    if ($Errors.Count -gt 0) {
        foreach ($error in $Errors) {
            if ($error -like "*error*") {
                [System.Windows.MessageBox]::Show($Errors,'Errors in building installer','OK','Error')
                exit
            }
        }
    }
}

function Success
{
    [System.Windows.MessageBox]::Show('Built successfully','Crew Chief Build Menu','OK','Information')
}
#######################################################################################

Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope CurrentUser
$Variant = ReleaseOrCandidate

$response = InstallerOrDogFood
if ($response -eq "Package a new installer")
{
    UpdateGUID
    BuildRelease
    BuildInstaller
    Success
}

if ($response -eq "Update your local dog food exe file")
{
    BuildRelease
    Success
    if ($Variant -eq "V4") 
    {
        $ScriptLocation = $PSScriptRoot + "\PatchLite.ps1"
    } 
    else 
    {
        $ScriptLocation = $PSScriptRoot + "\PatchLiteRC.ps1"
    }
    #$Cred = (Get-Credential)
    Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File $ScriptLocation" #-Credential $Cred
}

