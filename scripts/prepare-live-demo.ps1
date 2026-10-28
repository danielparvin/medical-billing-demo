# Run right before the talk. Makes (or re-checks) a clone of demo-start that gives Claude a clean context:
# no saved memories, no CLAUDE.md, and no way to reach the finished solution (demo-finished, cp-5-mpd-complete).
# Run it again on the existing clone to re-check it. Exits 1 if anything needs attention.
param(
    [string]$Destination = (Join-Path $env:USERPROFILE 'source\repos\medical-billing-demo-live'),
    [string]$Branch = 'demo-start',
    # Keep the origin remote in the clone. By default it is removed: demo-start's README names demo-finished,
    # so Claude could otherwise run 'git fetch origin demo-finished'.
    [switch]$KeepRemote,
    # Rename Claude's data for this clone's path (memories, past sessions) to a timestamped backup.
    [switch]$ResetClaudeData,
    # Set autoMemoryEnabled=false in ~/.claude/settings.json so the demo session saves no memories.
    [switch]$DisableAutoMemory
)
$ErrorActionPreference = 'Stop'

$problems = [System.Collections.Generic.List[string]]::new()
function Pass([string]$message) { Write-Host "  OK    $message" -ForegroundColor Green }
function Fail([string]$message) { Write-Host "  FIX   $message" -ForegroundColor Red; $problems.Add($message) }
function Note([string]$message) { Write-Host "  NOTE  $message" -ForegroundColor Yellow }

$claudeHome = Join-Path $env:USERPROFILE '.claude'
$Destination = [System.IO.Path]::GetFullPath($Destination)

# --- Clone ---------------------------------------------------------------------------------------------------
if (Test-Path $Destination) {
    Write-Host "Checking existing clone at $Destination"
} else {
    $sourceUrl = git -C $PSScriptRoot remote get-url origin
    Write-Host "Cloning $Branch from $sourceUrl into $Destination"
    git clone --quiet --single-branch --no-tags --branch $Branch $sourceUrl $Destination
    if ($LASTEXITCODE -ne 0) { throw "git clone failed." }
    if (-not $KeepRemote) { git -C $Destination remote remove origin }
}

# --- Git: only the starting point is reachable ---------------------------------------------------------------
Write-Host "`nGit"
$current = git -C $Destination branch --show-current
if ($current -eq $Branch) { Pass "On branch $Branch" } else { Fail "On branch '$current', expected $Branch" }

$otherRefs = @(git -C $Destination for-each-ref --format='%(refname)' |
    Where-Object { $_ -ne "refs/heads/$Branch" -and $_ -ne "refs/remotes/origin/$Branch" -and $_ -ne 'refs/remotes/origin/HEAD' })
if ($otherRefs.Count -eq 0) { Pass 'No other branches or tags' } else { Fail "Other refs present: $($otherRefs -join ', ')" }

$remotes = @(git -C $Destination remote)
if ($remotes.Count -eq 0) { Pass 'No remotes, so the finished branch cannot be fetched' }
elseif ($KeepRemote) { Note "Remote kept ($($remotes -join ', ')); 'git fetch origin demo-finished' would still work" }
else { Fail "Remotes present ($($remotes -join ', ')); remove with: git -C `"$Destination`" remote remove origin" }

$status = git -C $Destination status --porcelain
if (-not $status) { Pass 'Working tree clean' } else { Fail 'Working tree has changes left over from a rehearsal (git status)' }

$mentions = @(git -C $Destination grep -n -i -e 'demo-finished' -e 'cp-5-mpd-complete' HEAD -- . 2>$null)
foreach ($line in $mentions) { Note "Mentions the finished solution: $($line -replace '^HEAD:', '')" }

# --- Instructions Claude loads automatically -----------------------------------------------------------------
Write-Host "`nCLAUDE.md and project config"
$instructionFiles = @(
    (Join-Path $claudeHome 'CLAUDE.md'),
    (Join-Path $Destination 'CLAUDE.md'),
    (Join-Path $Destination 'CLAUDE.local.md'),
    (Join-Path $Destination '.claude')
)
# Claude also loads CLAUDE.md from every parent folder of the working directory.
$parent = Split-Path $Destination -Parent
while ($parent) {
    $instructionFiles += (Join-Path $parent 'CLAUDE.md'), (Join-Path $parent 'CLAUDE.local.md')
    $parent = Split-Path $parent -Parent
}
$found = @($instructionFiles | Where-Object { Test-Path $_ })
if ($found.Count -eq 0) { Pass 'No CLAUDE.md files or .claude folder' }
foreach ($path in $found) { Fail "Loaded into Claude's context: $path" }

# --- Claude's saved data for this path -----------------------------------------------------------------------
Write-Host "`nClaude memory and sessions"
# Claude Code keys per-project data by the working directory with every non-alphanumeric character replaced by '-'.
$projectKey = $Destination -replace '[^A-Za-z0-9]', '-'
$projectDir = Join-Path $claudeHome "projects\$projectKey"
if ((Test-Path $projectDir) -and $ResetClaudeData) {
    $backup = "$projectDir.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Rename-Item $projectDir $backup
    Note "Moved earlier Claude data to $backup"
}
$memoryFiles = @(if (Test-Path "$projectDir\memory") { Get-ChildItem "$projectDir\memory" -File -Recurse })
if ($memoryFiles.Count -eq 0) { Pass 'No saved memories for this folder' }
else { Fail "$($memoryFiles.Count) saved memory file(s) in $projectDir\memory (rerun with -ResetClaudeData)" }
$sessions = @(if (Test-Path $projectDir) { Get-ChildItem $projectDir -Filter '*.jsonl' -File })
if ($sessions.Count -eq 0) { Pass 'No earlier sessions in this folder' }
else { Note "$($sessions.Count) earlier session(s) here: start a new session, never resume (or rerun with -ResetClaudeData)" }

$settingsPath = Join-Path $claudeHome 'settings.json'
$settings = if (Test-Path $settingsPath) { Get-Content $settingsPath -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
if ($DisableAutoMemory -and $settings.autoMemoryEnabled -ne $false) {
    $settings | Add-Member -NotePropertyName autoMemoryEnabled -NotePropertyValue $false -Force
    $settings | ConvertTo-Json -Depth 20 | Set-Content $settingsPath -Encoding utf8
    Note "Set autoMemoryEnabled=false in $settingsPath (remove it after the talk to turn memory back on)"
}
if ($settings.autoMemoryEnabled -eq $false -or $env:CLAUDE_CODE_DISABLE_AUTO_MEMORY -eq '1') { Pass 'Auto memory is off' }
else { Note 'Auto memory is on, so the demo session may save memories (rerun with -DisableAutoMemory)' }

# --- Summary -------------------------------------------------------------------------------------------------
Write-Host ''
if ($problems.Count -eq 0) {
    Write-Host "Ready. Open a NEW Claude session in $Destination." -ForegroundColor Green
} else {
    Write-Host "$($problems.Count) item(s) to fix before the talk." -ForegroundColor Red
    exit 1
}
