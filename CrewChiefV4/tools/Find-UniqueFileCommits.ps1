param(
    [string]$File,
    [switch]$Mine,
    [string]$Author
)

cls;

# Prompt for file path if missing
if ([string]::IsNullOrWhiteSpace($File)) {

    Write-Host ""
    Write-Host "List the unique commits for a file in all branches of origin/"
    Write-Host "Enter file path:"
    $File = Read-Host ">"

    if ([string]::IsNullOrWhiteSpace($File)) {
        Write-Host ""
        Write-Host "Usage:"
        Write-Host "  .\Find-UniqueFileCommits.ps1 `"path\to\file.ext`" [-Mine] [-Author `"Name`"]"
        Write-Host ""
        Write-Host "Examples:"
        Write-Host "  .\Find-UniqueFileCommits.ps1 `"src\main.cpp`""
        Write-Host "  .\Find-UniqueFileCommits.ps1 `"src\main.cpp`" -Mine"
        Write-Host "  .\Find-UniqueFileCommits.ps1 `"src\main.cpp`" -Author `"John Smith`""
        Write-Host ""
        exit 1
    }
}

# Runtime prompt for Mine/Author if neither specified
if (-not $Mine -and [string]::IsNullOrWhiteSpace($Author)) {

    Write-Host ""
    Write-Host "Filter options:"
    Write-Host "  Return = All authors"
    Write-Host "  M = Only my commits"
    Write-Host "  A = Specific author"
    Write-Host ""

    $choice = Read-Host "Select option"
    $Mine = $false

    switch ($choice.ToLower()) {

        "m" {
            $Mine = $true
        }

        "a" {
            Write-Host ""
            Write-Host "Enter author name (case ignored):"
            $Author = Read-Host ">"
        }
    }
}

# Silence progress/UI noise
$ProgressPreference = 'SilentlyContinue'

git fetch origin --prune *> $null

# Resolve current git user if -Mine used
if ($Mine) {

    $Author = git config user.name 2>$null

    if ([string]::IsNullOrWhiteSpace($Author)) {
        Write-Host ""
        Write-Host "Unable to determine git user.name"
        exit 1
    }
}

# Only origin/* branches
$branches = git branch -r |
    ForEach-Object { $_.Trim() } |
    Where-Object {
        $_ -like 'origin/*' -and
        $_ -notmatch 'HEAD ->'
    }

Write-Host ""
Write-Host "Unique commits for file: $File"

if ($Author) {
    Write-Host "Author filter: $Author"
}
else {
    $Author = ".*"
}

Write-Host ""

foreach ($branch in $branches) {

    # Build git log arguments
    $gitArgs = @(
        "log"
        $branch
        "--not"
    )

    $gitArgs += ($branches | Where-Object { $_ -ne $branch })

    $gitArgs += @(
        "--follow"
        "--format=%H"
    )

    if ($Author) {
        $gitArgs += "--regexp-ignore-case"
        $gitArgs += "--author=$Author"
    }

    $gitArgs += @(
        "--"
        $File
    )
    
    # Commits unique to this origin branch
    $commits = & git @gitArgs 2>$null

    if (-not $commits) {
        continue
    }

    Write-Host "================================================================================"
    Write-Host "BRANCH: $branch"
    Write-Host "================================================================================"

    foreach ($commit in $commits) {

        $short   = git rev-parse --short $commit 2>$null
        $date    = git log -1 --format="%ad" --date=short $commit 2>$null
        $author  = git log -1 --format="%an" $commit 2>$null
        $subject = git log -1 --format="%s" $commit 2>$null

        Write-Host "$short  $date  $author"
        Write-Host "  $subject"
        Write-Host ""
    }
}