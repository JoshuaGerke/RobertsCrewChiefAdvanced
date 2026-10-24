# ============================================================================
# makeRelease.ps1
#
# Complete Crew Chief release automation
#
# Release input:
#   CrewChiefV4\updateSnippet.md
#
# Expected format is version number then a Markdown snippet with change details:
#
#   4.19.4.5
#
#   **Hotfix**
#   - **iRacing**
#     - Some change
#
# The version must be a four-part version number.
#
# Assumes website FTP to "tony@thecrewchief.org" using local file ftp-credential.xml 
# created with something like Powershell (as admin):
#   Get-Credential | Export-Clixml CrewChiefV4\ftp-credential.xml
#
# ============================================================================
#
# PROCESS
#
#   1. Validate release information
#   2. Update release files
#   3. Build Crew Chief
#   4. Build installer
#   5. Copy versioned MSI to CrewChiefV4.msi
#   6. Verify MSI files
#   7. FTP release files
#   8. Commit release to Git
#   9. Create annotated Git tag
#
# After commit/tag, a dialog offers to push the version tag to origin.
#
# ============================================================================


using namespace System.Management.Automation.Host
Add-Type -AssemblyName PresentationFramework

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ============================================================================
# CONFIGURATION
# ============================================================================

# makeRelease.ps1 lives in the repository root.
$Root = $PSScriptRoot


# ============================================================================
# SELECT RELEASE TYPE
# ============================================================================

function Select-ReleaseType
{
    $msgBoxInput = [System.Windows.MessageBox]::Show(
        'Build a Full release? (No: Beta Release)',
        'Crew Chief Release',
        'YesNoCancel',
        'Question'
    )

    switch ($msgBoxInput)
    {
        'Yes'
        {
            return "Release"
        }

        'No'
        {
            return "Beta"
        }

        'Cancel'
        {
            [System.Windows.MessageBox]::Show(
                'Release cancelled.',
                'Crew Chief Release',
                'OK',
                'Information'
            ) | Out-Null
            return $null
        }
    }
}

# ============================================================================
# FILES
# ============================================================================

$SnippetFile =
    Join-Path $Root "updateSnippet.md"

$AssemblyInfoFile =
    Join-Path $Root "CrewChiefV4\Properties\AssemblyInfo.cs"

$ProductFile =
    Join-Path $Root "CrewChiefV4_installer\Product.wxs"

$InstallerDirectory =
    Join-Path $Root "CrewChiefV4_installer\Installs"

$ChangeLogFile =
    Join-Path $Root "change_log_for_auto_updated.html"

$PrimaryXml =
    Join-Path $Root "auto_update_data_files\primary\auto_update_data.xml"

$SecondaryXml =
    Join-Path $Root "auto_update_data_files\secondary\auto_update_data.xml"

$GitlabXml =
    Join-Path $Root "auto_update_data_files\gitlab\auto_update_data.xml"

# ============================================================================
# FTP
# ============================================================================

$FtpServer = "198.54.116.134"
$FtpPort = 21
$FtpUser = "tony@thecrewchief.org"

$CredentialFile =
    Join-Path $Root "ftp-credential.xml"

# ============================================================================
# BACKUP
# ============================================================================

# Set after the version is read.  Backups are kept outside the Git repository
# so they cannot make a later clean-working-tree check fail.
$BackupDirectory = $null
$TagPushed = $false


# ============================================================================
# USER-VISIBLE EXIT / STATUS DIALOGS
# ============================================================================

function Show-Error
{
    param(
        [string]$Message
    )

    [System.Windows.MessageBox]::Show(
        $Message,
        'Crew Chief Release - Error',
        'OK',
        'Error'
    ) | Out-Null
}

function Show-Cancelled
{
    param(
        [string]$Message = 'The release operation was cancelled.'
    )

    [System.Windows.MessageBox]::Show(
        $Message,
        'Crew Chief Release',
        'OK',
        'Information'
    ) | Out-Null
}

function Show-Complete
{
    param(
        [string]$Message
    )

    [System.Windows.MessageBox]::Show(
        $Message,
        'Crew Chief Release',
        'OK',
        'Information'
    ) | Out-Null
}

function Check-CleanGitWorkingTree
{
    if (-not (Test-Path (Join-Path $Root '.git')))
    {
        throw 'makeRelease.ps1 must be located in the CrewChiefV4 Git repository root.'
    }

    Write-Host ''
    Write-Host 'Checking Git working tree...' -ForegroundColor Yellow

    Push-Location $Root
    try
    {
        $status = @(git status --porcelain -- . ":!updateSnippet.md" 2>&1)

        if ($LASTEXITCODE -ne 0)
        {
            throw 'Unable to determine Git working tree status.'
        }

        if ($status.Count -gt 0)
        {
            $details = $status -join "`r`n"

            throw @"
The Git working tree is not clean.

A release cannot be started while there are
uncommitted or untracked changes.

Current Git status:

$details

Commit or otherwise deal with these changes,
then run makeRelease.ps1 again.
"@
        }

        Write-Host 'Git working tree is clean.' -ForegroundColor Green
    }
    finally
    {
        Pop-Location
    }
}

# ============================================================================
# GENERAL FUNCTIONS
# ============================================================================

function Fail
{
    param(
        [string]$Message
    )

    Write-Host ""
    Write-Host "===================================" -ForegroundColor Red
    Write-Host "RELEASE ABORTED" -ForegroundColor Red
    Write-Host "===================================" -ForegroundColor Red
    Write-Host ""
    Write-Host $Message -ForegroundColor Red
    Write-Host ""

    throw $Message
}

function Confirm-Step
{
    param(
        [string]$Message
    )

    Write-Host ""
    $answer = Read-Host "$Message (Y/N, Enter=Yes)"

    # Empty input (just Enter) defaults to Yes.
    return ($answer -eq "" -or $answer -match '^[Yy]$')
}

function Backup-File
{
    param(
        [string]$File
    )

    if (-not (Test-Path $File))
    {
        throw "Cannot backup missing file: $File"
    }

    if (-not (Test-Path $BackupDirectory))
    {
        New-Item `
            -ItemType Directory `
            -Path $BackupDirectory `
            -Force |
            Out-Null
    }

    $relative =
        $File.Substring($Root.Length).TrimStart('\')

    $backup =
        Join-Path $BackupDirectory $relative

    $backupParent =
        Split-Path $backup -Parent

    if (-not (Test-Path $backupParent))
    {
        New-Item `
            -ItemType Directory `
            -Path $backupParent `
            -Force |
            Out-Null
    }

    Copy-Item `
        -Path $File `
        -Destination $backup `
        -Force

    Write-Host "  $relative" -ForegroundColor DarkGray
}

# ============================================================================
# READ RELEASE INFORMATION
# ============================================================================

function Read-ReleaseInformation
{
    if (-not (Test-Path $SnippetFile))
    {
        throw "Release notes not found: $SnippetFile"
    }

    $snippet =
        Get-Content -Raw -Encoding UTF8 $SnippetFile

    if ([string]::IsNullOrWhiteSpace($snippet))
    {
        throw "$SnippetFile is empty."
    }

    $versionMatch =
        [regex]::Match(
            $snippet,
            '(?m)^\s*(\d+\.\d+\.\d+\.\d+)\s*$'
        )

    if (-not $versionMatch.Success)
    {
        throw `
            "Could not find a four-part version number in $SnippetFile."
    }

    $script:Version =
        $versionMatch.Groups[1].Value

    $script:Markdown =
        $snippet.Substring(
            $versionMatch.Index + $versionMatch.Length
        ).Trim()

    if ([string]::IsNullOrWhiteSpace($Markdown))
    {
        throw "No release notes found after version $($Version)."
    }

    $script:ReleaseDate =
        Get-Date -Format "dd MMMM yyyy"

    $script:BackupDirectory =
        Join-Path $env:TEMP "CrewChiefV4-release-backup-$Version"

    Write-Host ""
    Write-Host "Release version:"
    Write-Host "  $Version" -ForegroundColor Green

    Write-Host ""
    Write-Host "Release date:"
    Write-Host "  $ReleaseDate"
}

# ============================================================================
# VALIDATE ASSEMBLY VERSION
# ============================================================================

function Get-CurrentAssemblyVersion
{
    $assembly =
        Get-Content -Raw -Encoding UTF8 $AssemblyInfoFile

    # The ^ is intentional.
    #
    # This matches:
    #
    # [assembly: AssemblyVersion("4.19.4.4")]
    #
    # but NOT:
    #
    # // [assembly: AssemblyVersion("1.0.*")]
    #

    $pattern =
        '(?m)^(\s*\[assembly:\s*AssemblyVersion\s*\(\s*")([^"]+)("\s*\)\s*\])'

    $matches =
        [regex]::Matches(
            $assembly,
            $pattern
        )

    if ($matches.Count -ne 1)
    {
        throw `
            "Expected exactly one active AssemblyVersion in $AssemblyInfoFile, found $($matches.Count)"
    }

    return $matches[0].Groups[2].Value
}

function Validate-Version
{
    $currentVersion =
        Get-CurrentAssemblyVersion

    $versionObject = $null

    if (-not [version]::TryParse(
        $Version,
        [ref]$versionObject
    ))
    {
        throw "Release version '$Version' is not a valid .NET version."
    }

    $currentVersionObject = $null

    if (-not [version]::TryParse(
        $currentVersion,
        [ref]$currentVersionObject
    ))
    {
        throw `
            "Current AssemblyVersion '$currentVersion' is not a valid .NET version."
    }

    if ($versionObject -le $currentVersionObject)
    {
        throw `
            "Release version $Version must be greater than current AssemblyVersion $currentVersion."
    }

    Write-Host ""
    Write-Host "Current AssemblyVersion:"
    Write-Host "  $currentVersion"

    Write-Host ""
    Write-Host "New AssemblyVersion:"
    Write-Host "  $Version" -ForegroundColor Green
}

# ============================================================================
# UPDATE ASSEMBLY VERSION
# ============================================================================

function Update-AssemblyVersion
{
    Write-Host ""
    Write-Host "Updating AssemblyVersion..." -ForegroundColor Yellow

    $assembly =
        Get-Content -Raw -Encoding UTF8 $AssemblyInfoFile

    $pattern =
        '(?m)^(\s*\[assembly:\s*AssemblyVersion\s*\(\s*")([^"]+)("\s*\)\s*\])'

    $matches =
        [regex]::Matches(
            $assembly,
            $pattern
        )

    if ($matches.Count -ne 1)
    {
        throw `
            "Expected exactly one active AssemblyVersion in $AssemblyInfoFile, found $($matches.Count)"
    }

    $assembly =
        [regex]::Replace(
            $assembly,
            $pattern,
            "`${1}$Version`${3}",
            1
        )

    Set-Content `
        -Path $AssemblyInfoFile `
        -Value $assembly `
        -Encoding UTF8

    $newVersion =
        Get-CurrentAssemblyVersion

    if ($newVersion -ne $Version)
    {
        throw `
            "AssemblyVersion update failed. Found '$newVersion', expected '$Version'."
    }

    Write-Host "  AssemblyVersion = $Version" `
        -ForegroundColor Green
}

# ============================================================================
# UPDATE PRODUCT CODE
# ============================================================================

function Update-ProductCode
{
    Write-Host ""
    Write-Host "Generating new ProductCode..." -ForegroundColor Yellow

    $ProductCode =
        [guid]::NewGuid().ToString().ToUpper()

    $product =
        Get-Content -Raw -Encoding UTF8 $ProductFile

    $pattern =
        'ProductCode\s*=\s*\{[^}]+\}'

    $matches =
        [regex]::Matches(
            $product,
            $pattern
        )

    if ($matches.Count -ne 1)
    {
        throw `
            "Expected exactly one ProductCode in $ProductFile, found $($matches.Count)"
    }

    $product =
        [regex]::Replace(
            $product,
            $pattern,
            "ProductCode = {$ProductCode}",
            1
        )

    Set-Content `
        -Path $ProductFile `
        -Value $product `
        -Encoding UTF8

    $script:ProductCode = $ProductCode

    Write-Host "  ProductCode = {$ProductCode}" `
        -ForegroundColor Green
}

# ============================================================================
# MARKDOWN -> CHANGELOG HTML
# ============================================================================

function Convert-MarkdownToChangelogHtml
{
    param(
        [string]$Markdown
    )

    $lines =
        $Markdown -split '\r?\n'

    $html =
        New-Object System.Collections.Generic.List[string]

    $inList = $false

    foreach ($line in $lines)
    {
        if ([string]::IsNullOrWhiteSpace($line))
        {
            if ($inList)
            {
                $html.Add("    </ul>")
                $inList = $false
            }

            continue
        }

	$line = $line.Replace("&", "&amp;")
	$line = $line.Replace("<", "&lt;")
	$line = $line.Replace(">", "&gt;")

        # **text** -> <b>text</b>

        $line =
            [regex]::Replace(
                $line,
                '\*\*(.+?)\*\*',
                '<b>$1</b>'
            )

        # Headings

        if ($line -match '^\s*#{1,3}\s+(.+)$')
        {
            if ($inList)
            {
                $html.Add("    </ul>")
                $inList = $false
            }

            $html.Add(
                "    <b>$($Matches[1])</b><br>"
            )

            continue
        }

        # List items

        if ($line -match '^\s*[-*]\s+(.+)$')
        {
            if (-not $inList)
            {
                $html.Add("    <ul>")
                $inList = $true
            }

            $html.Add(
                "      <li>$($Matches[1])</li>"
            )

            continue
        }

        if ($inList)
        {
            $html.Add("    </ul>")
            $inList = $false
        }

        $html.Add("    $line<br>")
    }

    if ($inList)
    {
        $html.Add("    </ul>")
    }

    return ($html -join "`r`n")
}

# ============================================================================
# UPDATE CHANGELOG
# ============================================================================

function Update-ChangeLog
{
    Write-Host ""
    Write-Host "Updating changelog..." -ForegroundColor Yellow

    $changeLog =
        Get-Content -Raw -Encoding UTF8 $ChangeLogFile

    if ($changeLog -notmatch '(?i)<body\b[^>]*>')
    {
        throw "Could not find <body> in $ChangeLogFile."
    }

    if (
        $changeLog -match `
        "(?i)<h2>\s*Version\s+$([regex]::Escape($Version))\s*</h2>"
    )
    {
        throw `
            "Version $Version already exists in the changelog."
    }

    $changeHtml =
        Convert-MarkdownToChangelogHtml $Markdown

    $entry = @"
<h2>Version $Version</h2>
$changeHtml
"@

    $newChangeLog =
        [regex]::Replace(
            $changeLog,
            '(?i)(<body\b[^>]*>)',
            "`$1`r`n$entry",
            1
        )

    Set-Content `
        -Path $ChangeLogFile `
        -Value $newChangeLog `
        -Encoding UTF8
}

# ============================================================================
# UPDATE AUTO UPDATE XML
# ============================================================================

function Update-AutoUpdateXml
{
    param(
        [string]$File,
        [bool]$UpdateInstallerUrl
    )

    $xml =
        Get-Content -Raw -Encoding UTF8 $File

    if (
        [regex]::Matches(
            $xml,
            '<version>\s*[^<]+\s*</version>'
        ).Count -ne 1
    )
    {
        throw "Expected exactly one <version> in $File."
    }

    if (
        [regex]::Matches(
            $xml,
            '<title>\s*[^<]+\s*</title>'
        ).Count -ne 1
    )
    {
        throw "Expected exactly one <title> in $File."
    }

    $xml =
        [regex]::Replace(
            $xml,
            '<version>\s*[^<]+\s*</version>',
            "<version>$Version</version>",
            1
        )

    $title =
        "Crew Chief $Version updated $ReleaseDate"

    $xml =
        [regex]::Replace(
            $xml,
            '<title>\s*[^<]+\s*</title>',
            "<title>$title</title>",
            1
        )

    if ($UpdateInstallerUrl)
    {
        $url =
            "https://gitlab.com/mr_belowski/CrewChiefV4/-/raw/main/" +
            "CrewChiefV4_installer/Installs/" +
            "CrewChiefV4-$Version.msi"

        if (
            [regex]::Matches(
                $xml,
                '<url>\s*[^<]+\s*</url>'
            ).Count -ne 1
        )
        {
            throw "Expected exactly one <url> in $File."
        }

        $xml =
            [regex]::Replace(
                $xml,
                '<url>\s*[^<]+\s*</url>',
                "<url>$url</url>",
                1
            )
    }

    Set-Content `
        -Path $File `
        -Value $xml `
        -Encoding UTF8
}

function Update-ReleaseData
{
    Write-Host ""
    Write-Host "Updating release data files..." `
        -ForegroundColor Yellow

    Update-AutoUpdateXml `
        $PrimaryXml `
        $false

    Update-AutoUpdateXml `
        $SecondaryXml `
        $true

    Update-AutoUpdateXml `
        $GitlabXml `
        $true
}

# ============================================================================
# MSBUILD
# ============================================================================

function Add-MSBuildToPath
{
    $paths = @(
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin",
        "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64"
    )

    foreach ($path in $paths)
    {
        if (Test-Path $path)
        {
            if ($env:Path -notlike "*$path*")
            {
                $env:Path += ";$path"
            }
        }
    }

    $msbuild =
        Get-Command msbuild.exe -ErrorAction SilentlyContinue

    if ($null -eq $msbuild)
    {
        throw "MSBuild.exe could not be found."
    }

    Write-Host ""
    Write-Host "MSBuild:"
    Write-Host "  $($msbuild.Source)" -ForegroundColor DarkGray
}

# ============================================================================
# BUILD APPLICATION
# ============================================================================

function BuildApplication
{
    Add-MSBuildToPath

    $directory =
        Join-Path $Root "CrewChiefV4"

    Push-Location $directory

    try
    {
        Write-Host ""
        Write-Host "===================================" -ForegroundColor Cyan
        Write-Host "BUILDING CREW CHIEF" -ForegroundColor Cyan
        Write-Host "===================================" -ForegroundColor Cyan

        & msbuild.exe `
            "CrewChiefV4.csproj" `
            "/t:Rebuild" `
            "/p:Configuration=Release"

        if ($LASTEXITCODE -ne 0)
        {
            throw `
                "CrewChiefV4 build failed with exit code $LASTEXITCODE"
        }

        Write-Host ""
        Write-Host "CrewChiefV4 build successful." `
            -ForegroundColor Green
    }
    finally
    {
        Pop-Location
    }
}

# ============================================================================
# BUILD INSTALLER
# ============================================================================

function BuildInstaller
{
    $directory =
        Join-Path $Root "CrewChiefV4_installer"

    Push-Location $directory

    try
    {
        Write-Host ""
        Write-Host "===================================" -ForegroundColor Cyan
        Write-Host "BUILDING INSTALLER" -ForegroundColor Cyan
        Write-Host "===================================" -ForegroundColor Cyan

        $logFile =
            Join-Path $directory "WixLog.txt"

        & msbuild.exe `
            "CrewChiefV4_installer.wixproj" `
            "/t:Rebuild" `
            "/p:Configuration=Release" `
            2>&1 |
            Tee-Object -FilePath $logFile

        if ($LASTEXITCODE -ne 0)
        {
            throw `
                "Installer build failed. See $logFile"
        }

        Write-Host ""
        Write-Host "Installer build successful." `
            -ForegroundColor Green
    }
    finally
    {
        Pop-Location
    }
}

# ============================================================================
# GET BUILT MSI
# ============================================================================

function Get-VersionedInstaller
{
    $file =
        Join-Path `
            $InstallerDirectory `
            "CrewChiefV4-$Version.msi"

    if (-not (Test-Path $file))
    {
        throw @"
Installer build completed but the expected MSI was not found:

$file
"@
    }

    $info =
        Get-Item $file

    if ($info.Length -eq 0)
    {
        throw "Generated MSI is zero bytes."
    }

    Write-Host ""
    Write-Host "Installer created:"
    Write-Host "  $file"
    Write-Host "  $([math]::Round($info.Length / 1MB, 2)) MB"

    return $file
}

# ============================================================================
# COPY MSI
# ============================================================================

function Copy-BetaInstaller
{
    param(
        [string]$Source
    )

    $destination =
        Join-Path `
            $InstallerDirectory `
            "CrewChiefV4_beta.msi"

    Write-Host ""
    Write-Host "Creating beta installer..." -ForegroundColor Yellow

    Copy-Item `
        -Path $Source `
        -Destination $destination `
        -Force

    if (-not (Test-Path $destination))
    {
        throw "Failed to create $destination."
    }

    $sourceHash =
        (Get-FileHash `
            -Path $Source `
            -Algorithm SHA256).Hash

    $destinationHash =
        (Get-FileHash `
            -Path $destination `
            -Algorithm SHA256).Hash

    if ($sourceHash -ne $destinationHash)
    {
        throw "Beta MSI does not match the versioned MSI."
    }

    Write-Host "  $destination" -ForegroundColor Green

    return $destination
}

function Copy-ReleaseInstaller
{
    param(
        [string]$Source
    )

    $destination =
        Join-Path `
            $InstallerDirectory `
            "CrewChiefV4.msi"

    Write-Host ""
    Write-Host "Creating current installer..." -ForegroundColor Yellow

    Copy-Item `
        -Path $Source `
        -Destination $destination `
        -Force

    if (-not (Test-Path $destination))
    {
        throw "Failed to create $destination."
    }

    $sourceHash =
        (Get-FileHash `
            -Path $Source `
            -Algorithm SHA256).Hash

    $destinationHash =
        (Get-FileHash `
            -Path $destination `
            -Algorithm SHA256).Hash

    if ($sourceHash -ne $destinationHash)
    {
        throw "Generic MSI does not match the versioned MSI."
    }

    Write-Host "  $destination" -ForegroundColor Green

    return $destination
}

# ============================================================================
# FTP
# ============================================================================

function New-FtpRequest
{
    param(
        [string]$RemoteFile,
        [string]$Method,
        [System.Management.Automation.PSCredential]$Credential
    )

    $uri =
        "ftp://$FtpServer`:$FtpPort/$RemoteFile"

    $request =
        [System.Net.FtpWebRequest]::Create($uri)

    $request.Method = $Method
    $request.Credentials = $Credential
    $request.UseBinary = $true
    $request.UsePassive = $true
    $request.KeepAlive = $false

    return $request
}

function Upload-FtpFile
{
    param(
        [string]$LocalFile,
        [string]$RemoteFile,
        [System.Management.Automation.PSCredential]$Credential
    )

    Write-Host ""
    Write-Host "Uploading $RemoteFile..." -ForegroundColor Yellow

    $request =
        New-FtpRequest `
            $RemoteFile `
            ([System.Net.WebRequestMethods+Ftp]::UploadFile) `
            $Credential

    $bytes =
        [System.IO.File]::ReadAllBytes($LocalFile)

    try
    {
        $request.ContentLength =
            $bytes.Length

        $stream =
            $request.GetRequestStream()

        try
        {
            $stream.Write(
                $bytes,
                0,
                $bytes.Length
            )
        }
        finally
        {
            $stream.Close()
        }

        $response =
            $request.GetResponse()

        $response.Close()
    }
    catch
    {
        throw `
            "FTP upload failed for $RemoteFile`: $($_.Exception.Message)"
    }

    # Verify remote size.

    $sizeRequest =
        New-FtpRequest `
            $RemoteFile `
            ([System.Net.WebRequestMethods+Ftp]::GetFileSize) `
            $Credential

    try
    {
        $sizeResponse =
            $sizeRequest.GetResponse()

        $remoteSize =
            $sizeResponse.ContentLength

        $sizeResponse.Close()
    }
    catch
    {
        throw `
            "Upload completed but remote size could not be verified for $RemoteFile`: $($_.Exception.Message)"
    }

    $localSize =
        (Get-Item $LocalFile).Length

    if ($remoteSize -ne $localSize)
    {
        throw @"
FTP upload verification FAILED.

File:
  $RemoteFile

Local size:
  $localSize bytes

Remote size:
  $remoteSize bytes
"@
    }

    Write-Host `
        "  Verified: $remoteSize bytes" `
        -ForegroundColor Green
}

function Get-FtpCredential
{
    if (-not (Test-Path $CredentialFile))
    {
        throw @"
FTP credential file not found:

$CredentialFile

Create it once with:

Get-Credential -UserName "$FtpUser" |
    Export-Clixml "$CredentialFile"

The password is encrypted by Windows for the account which
created the file.
"@
    }

    $credential =
        Import-Clixml $CredentialFile

    if ($credential.UserName -ne $FtpUser)
    {
        throw `
            "FTP credential contains '$($credential.UserName)' instead of '$FtpUser'."
    }

    return $credential
}

function Upload-Beta
{
    param(
        [string]$BetaInstaller
    )

    $credential =
        Get-FtpCredential

    Write-Host ""
    Write-Host "===================================" -ForegroundColor Cyan
    Write-Host "BETA FTP UPLOAD" -ForegroundColor Cyan
    Write-Host "===================================" -ForegroundColor Cyan

    Write-Host ""
    Write-Host "Server:"
    Write-Host "  $FtpServer`:$FtpPort"

    Write-Host ""
    Write-Host "File:"
    Write-Host "  $BetaInstaller"
    Write-Host "    -> CrewChiefV4_beta.msi"

    if (-not (Confirm-Step "Upload the beta installer?"))
    {
        Write-Host ""
        Write-Host "FTP upload cancelled." -ForegroundColor Yellow
        Show-Cancelled 'FTP upload was cancelled.'
        return $false
    }

    Upload-FtpFile `
        $BetaInstaller `
        "CrewChiefV4_beta.msi" `
        $credential

    return $true
}

function Upload-Release
{
    param(
        [string]$VersionedInstaller,
        [string]$GenericInstaller
    )

    $credential =
        Get-FtpCredential

    Write-Host ""
    Write-Host "===================================" -ForegroundColor Cyan
    Write-Host "RELEASE FTP UPLOAD" -ForegroundColor Cyan
    Write-Host "===================================" -ForegroundColor Cyan

    if (-not (Confirm-Step "Upload the release files?"))
    {
        Write-Host ""
        Write-Host "FTP upload cancelled." -ForegroundColor Yellow
        Show-Cancelled 'FTP upload was cancelled.'
        return $false
    }

    Upload-FtpFile `
        $VersionedInstaller `
        "CrewChiefV4-$Version.msi" `
        $credential

    Upload-FtpFile `
        $GenericInstaller `
        "CrewChiefV4.msi" `
        $credential

    Upload-FtpFile `
        $ChangeLogFile `
        "change_log_for_auto_updated.html" `
        $credential

    Upload-FtpFile `
        $PrimaryXml `
        "auto_update_data.xml" `
        $credential

    return $true
}

# ============================================================================
# GIT
# ============================================================================

function Get-GitRoot
{
    Push-Location $Root

    try
    {
        $result =
            git rev-parse --show-toplevel 2>&1

        if ($LASTEXITCODE -ne 0)
        {
            throw "Could not determine Git repository root."
        }

        return $result.Trim()
    }
    finally
    {
        Pop-Location
    }
}

function Test-GitTagDoesNotExist
{
    Push-Location $Root

    try
    {
        $tag =
            git tag --list $Version

        if ($tag)
        {
            throw `
                "Git tag '$Version' already exists."
        }
    }
    finally
    {
        Pop-Location
    }
}

function Stage-GitFiles
{
    param(
        [string[]]$Files,
        [string[]]$ForcedFiles
    )

    Push-Location $Root

    try
    {
        Write-Host ""
        Write-Host "Staging Git files..." -ForegroundColor Yellow

        foreach ($file in $Files)
        {
            git add -- $file

            if ($LASTEXITCODE -ne 0)
            {
                throw "git add failed for $file"
            }
        }

        foreach ($file in $ForcedFiles)
        {
            git add -f -- $file

            if ($LASTEXITCODE -ne 0)
            {
                throw "git add -f failed for $file"
            }
        }

        Write-Host ""
        Write-Host "Staged changes:" -ForegroundColor Yellow
        Write-Host "-----------------------------------"

        git status --short

        Write-Host "-----------------------------------"

        Write-Host ""
        git diff --cached --stat
    }
    finally
    {
        Pop-Location
    }
}

function Offer-GitPush
{
    $msgBoxInput = [System.Windows.MessageBox]::Show(
        "Release $($Version) has been committed and tagged.`r`n`r`nPush tag $($Version) to origin now?",
        'Crew Chief Release',
        'YesNo',
        'Question'
    )

    if ($msgBoxInput -ne 'Yes')
    {
        $script:TagPushed = $false
        [System.Windows.MessageBox]::Show(
            "Release $($Version) was committed and tagged locally.`r`n`r`nThe tag has NOT been pushed.",
            'Crew Chief Release',
            'OK',
            'Information'
        ) | Out-Null
        return $false
    }

    Write-Host "Pushing release $($Version)..." -ForegroundColor Yellow

    Push-Location $Root
    try
    {
	# Upload LFS objects first.
	git lfs push origin main

	if ($LASTEXITCODE -ne 0)
	{
	    throw "git lfs push origin main failed."
	}

	# Push the release commit to main.
	git push origin main

	if ($LASTEXITCODE -ne 0)
	{
	    throw "git push origin main failed."
	}

	# Finally push the release tag.
	git push origin $Version

	if ($LASTEXITCODE -ne 0)
	{
	    throw "git push origin $($Version) failed."
	}

        Write-Host "Release $($Version) pushed successfully." -ForegroundColor Green
        $script:TagPushed = $true
        return $true
    }
    finally
    {
        Pop-Location
    }
}

function Commit-AndTag
{
    Push-Location $Root

    try
    {
        if (-not (Confirm-Step "Commit and tag release $($Version)?"))
        {
            Write-Host ""
            Write-Host "Git commit cancelled." -ForegroundColor Yellow
            Write-Host "Changes remain staged."
            Show-Cancelled 'Git commit was cancelled. Changes remain staged.'
            return $false
        }

        Write-Host ""
        Write-Host "Creating commit..." -ForegroundColor Yellow

        git commit -m "Release $Version"

        if ($LASTEXITCODE -ne 0)
        {
            throw "git commit failed."
        }

        Write-Host ""
        Write-Host "Creating annotated tag $($Version)..." `
            -ForegroundColor Yellow

        # Use a message file rather than -m. PowerShell can split multiline
        # Markdown into multiple native-command arguments.
        $tagMessageFile =
            Join-Path $env:TEMP "CrewChiefV4-tag-$($Version).txt"

        try
        {
            [System.IO.File]::WriteAllText(
                $tagMessageFile,
                $Markdown,
                (New-Object System.Text.UTF8Encoding($false))
            )

            git tag -a $Version -F $tagMessageFile

            if ($LASTEXITCODE -ne 0)
            {
                throw "git tag creation failed."
            }
        }
        finally
        {
            if (Test-Path $tagMessageFile)
            {
                Remove-Item $tagMessageFile -Force
            }
        }

        Write-Host ""
        Write-Host "Commit:" -ForegroundColor Green
        git log -1 --oneline

        Write-Host ""
        Write-Host "Tag:" -ForegroundColor Green
        git tag -n99 --list $Version

        Offer-GitPush | Out-Null

        return $true
    }
    finally
    {
        Pop-Location
    }
}

# ============================================================================
# BETA GIT
# ============================================================================

function Commit-Beta
{
    $files = @(
        "CrewChiefV4\Properties\AssemblyInfo.cs"
    )

    $forcedFiles = @(
        "CrewChiefV4_installer\Installs\CrewChiefV4-$Version.msi",
        "CrewChiefV4_installer\Installs\CrewChiefV4_beta.msi"
    )

    Stage-GitFiles `
        -Files $files `
        -ForcedFiles $forcedFiles

    return (Commit-AndTag)
}

# ============================================================================
# RELEASE GIT
# ============================================================================

function Commit-Release
{
    $files = @(
        "change_log_for_auto_updated.html",
        "CrewChiefV4\Properties\AssemblyInfo.cs",
        "CrewChiefV4_installer\Product.wxs",
        "auto_update_data_files\primary\auto_update_data.xml",
        "auto_update_data_files\secondary\auto_update_data.xml",
        "auto_update_data_files\gitlab\auto_update_data.xml"
    )

    $forcedFiles = @(
        "CrewChiefV4_installer\Installs\CrewChiefV4-$Version.msi",
        "CrewChiefV4_installer\Installs\CrewChiefV4.msi"
    )

    Stage-GitFiles `
        -Files $files `
        -ForcedFiles $forcedFiles

    return (Commit-AndTag)
}

# ============================================================================
# BETA WORKFLOW
# ============================================================================

function Build-Beta
{
    Write-Host ""
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host "             CREW CHIEF BETA" -ForegroundColor Cyan
    Write-Host "===============================================" -ForegroundColor Cyan

    Write-Host ""
    Write-Host "Beta release:"
    Write-Host "  $Version" -ForegroundColor Green

    # ------------------------------------------------------------------------
    # Back up AssemblyInfo and Product.wxs
    # ------------------------------------------------------------------------

    Backup-File $AssemblyInfoFile
    Backup-File $ProductFile

    # ------------------------------------------------------------------------
    # Update AssemblyVersion
    # ------------------------------------------------------------------------

    Update-AssemblyVersion

    # ------------------------------------------------------------------------
    # Generate new ProductCode, same as normal buildRelease.ps1
    # ------------------------------------------------------------------------

    Update-ProductCode

    # ------------------------------------------------------------------------
    # Confirm build
    # ------------------------------------------------------------------------

    if (-not (Confirm-Step "Build beta $($Version)?"))
    {
        Write-Host ""
        Write-Host "Beta build cancelled." -ForegroundColor Yellow
        Show-Cancelled "Beta build $($Version) was cancelled."
        return
    }

    # ------------------------------------------------------------------------
    # Build
    # ------------------------------------------------------------------------

    BuildApplication
    BuildInstaller

    # ------------------------------------------------------------------------
    # Locate generated MSI
    # ------------------------------------------------------------------------

    $versionedInstaller =
        Get-VersionedInstaller

    # ------------------------------------------------------------------------
    # Create CrewChiefV4_beta.msi
    # ------------------------------------------------------------------------

    $betaInstaller =
        Copy-BetaInstaller $versionedInstaller

    # ------------------------------------------------------------------------
    # FTP
    # ------------------------------------------------------------------------

    $ftpCompleted =
        Upload-Beta $betaInstaller

    if (-not $ftpCompleted)
    {
        Write-Host ""
        Write-Host "Beta MSI built but was not uploaded." `
            -ForegroundColor Yellow

        return
    }

    # ------------------------------------------------------------------------
    # Git
    # ------------------------------------------------------------------------

    Test-GitTagDoesNotExist

    $gitCompleted =
        Commit-Beta

    if (-not $gitCompleted)
    {
        return
    }

    # ------------------------------------------------------------------------
    # Complete
    # ------------------------------------------------------------------------

    Write-Host ""
    Write-Host "===============================================" `
        -ForegroundColor Green
    Write-Host "             BETA BUILD COMPLETE" `
        -ForegroundColor Green
    Write-Host "===============================================" `
        -ForegroundColor Green

    Write-Host ""
    Write-Host "Version:"
    Write-Host "  $Version"

    Write-Host ""
    Write-Host "Beta installer:"
    Write-Host "  $betaInstaller"

    Write-Host ""
    Write-Host "FTP:"
    Write-Host "  Uploaded and verified."

    Write-Host ""
    Write-Host "Git tag:"
    Write-Host "  $Version"

    Write-Host ""
    if ($TagPushed)
    {
        Write-Host "Git tag pushed to origin." -ForegroundColor Green
        Show-Complete "Beta $($Version) completed successfully.`r`n`r`nThe MSI was uploaded, committed, tagged and the tag was pushed to origin."
    }
    else
    {
        Write-Host "Git tag was not pushed." -ForegroundColor Yellow
        Show-Complete "Beta $($Version) completed successfully.`r`n`r`nThe MSI was uploaded and committed/tagged locally.`r`n`r`nThe tag was NOT pushed to origin."
    }
}

# ============================================================================
# RELEASE WORKFLOW
# ============================================================================

function Build-Release
{
    Write-Host ""
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host "             CREW CHIEF RELEASE" -ForegroundColor Cyan
    Write-Host "===============================================" -ForegroundColor Cyan

    # ------------------------------------------------------------------------
    # Back up all files which will be changed.
    # ------------------------------------------------------------------------

    Backup-File $ChangeLogFile
    Backup-File $AssemblyInfoFile
    Backup-File $ProductFile
    Backup-File $PrimaryXml
    Backup-File $SecondaryXml
    Backup-File $GitlabXml

    # ------------------------------------------------------------------------
    # Update release files.
    # ------------------------------------------------------------------------

    Update-ChangeLog
    Update-AssemblyVersion
    Update-ProductCode
    Update-ReleaseData

    # ------------------------------------------------------------------------
    # Confirm build.
    # ------------------------------------------------------------------------

    if (-not (Confirm-Step "Build release $($Version)?"))
    {
        Write-Host ""
        Write-Host "Release build cancelled." -ForegroundColor Yellow
        Show-Cancelled "Release build $($Version) was cancelled."
        return
    }

    # ------------------------------------------------------------------------
    # Build.
    # ------------------------------------------------------------------------

    BuildApplication
    BuildInstaller

    # ------------------------------------------------------------------------
    # Locate versioned MSI.
    # ------------------------------------------------------------------------

    $versionedInstaller =
        Get-VersionedInstaller

    # ------------------------------------------------------------------------
    # Create generic MSI.
    # ------------------------------------------------------------------------

    $genericInstaller =
        Copy-ReleaseInstaller $versionedInstaller

    # ------------------------------------------------------------------------
    # FTP.
    # ------------------------------------------------------------------------

    $ftpCompleted =
        Upload-Release `
            $versionedInstaller `
            $genericInstaller

    if (-not $ftpCompleted)
    {
        Write-Host ""
        Write-Host "Release built but FTP upload was cancelled." `
            -ForegroundColor Yellow

        return
    }

    # ------------------------------------------------------------------------
    # Git.
    # ------------------------------------------------------------------------

    Test-GitTagDoesNotExist

    $gitCompleted =
        Commit-Release

    if (-not $gitCompleted)
    {
        return
    }

    # ------------------------------------------------------------------------
    # Complete.
    # ------------------------------------------------------------------------

    Write-Host ""
    Write-Host "===============================================" `
        -ForegroundColor Green
    Write-Host "             RELEASE COMPLETE" `
        -ForegroundColor Green
    Write-Host "===============================================" `
        -ForegroundColor Green

    Write-Host ""
    Write-Host "Version:"
    Write-Host "  $Version"

    Write-Host ""
    Write-Host "Installer:"
    Write-Host "  $versionedInstaller"

    Write-Host ""
    Write-Host "FTP:"
    Write-Host "  Uploaded and verified."

    Write-Host ""
    Write-Host "Git tag:"
    Write-Host "  $Version"

    Write-Host ""
    if ($TagPushed)
    {
        Write-Host "Git tag pushed to origin." -ForegroundColor Green
        Show-Complete "Release $($Version) completed successfully.`r`n`r`nThe installers and release files were uploaded, committed, tagged and the tag was pushed to origin."
    }
    else
    {
        Write-Host "Git tag was not pushed." -ForegroundColor Yellow
        Show-Complete "Release $($Version) completed successfully.`r`n`r`nThe release was uploaded and committed/tagged locally.`r`n`r`nThe tag was NOT pushed to origin."
    }
}

# ============================================================================
# MAIN
# ============================================================================

try
{
    # This is deliberately the first release-specific operation.  Nothing is
    # read or modified until the repository has been confirmed clean.
    Check-CleanGitWorkingTree

    $ReleaseType = Select-ReleaseType

    if ($null -eq $ReleaseType)
    {
        return
    }

    Read-ReleaseInformation
    Validate-Version
    Test-GitTagDoesNotExist

    Write-Host ''
    Write-Host 'Release type:'
    Write-Host "  $ReleaseType" -ForegroundColor Green

    switch ($ReleaseType)
    {
        'Beta'
        {
            Build-Beta
        }

        'Release'
        {
            Build-Release
        }

        default
        {
            throw "Unknown ReleaseType '$ReleaseType'. Use 'Release' or 'Beta'."
        }
    }
}
catch
{
    $message = $_.Exception.Message

    Write-Host ''
    Write-Host '===================================' -ForegroundColor Red
    Write-Host 'RELEASE ABORTED' -ForegroundColor Red
    Write-Host '===================================' -ForegroundColor Red
    Write-Host ''
    Write-Host $message -ForegroundColor Red
    Write-Host ''

    Show-Error $message
}
