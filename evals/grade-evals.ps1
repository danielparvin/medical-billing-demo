# Grades the eval copies that prepare-evals.ps1 made, after an agent has worked in each one.
# For each copy it reports what changed, whether the agent's own unit and component tests pass, and how many of the
# hidden grader's checks pass. The grader is copied in only for the run and removed afterwards.
#
#   ./evals/grade-evals.ps1 -OutDir C:\temp\evals
param([Parameter(Mandatory)][string]$OutDir)

$ErrorActionPreference = 'Stop'
$graders = Join-Path $PSScriptRoot 'graders'
$contradicting = 'Reprice_pays_every_line_in_full_and_sums_the_total_allowed_when_a_bill_has_several_priced_lines'
$testDir = 'tests/Billing.Domain.Tests'

# Runs dotnet test and returns "passed/total", or "build failed" when nothing ran.
function Invoke-Tests([string]$Project, [string]$Filter) {
    $arguments = @('test', $Project, '--nologo')
    if ($Filter) { $arguments += @('--filter', $Filter) }
    $output = (& dotnet @arguments | Out-String)
    $found = [regex]::Matches($output, 'Failed:\s+(\d+), Passed:\s+(\d+)')
    if ($found.Count -eq 0) { return 'build failed' }
    $failed = 0; $passed = 0
    foreach ($m in $found) { $failed += [int]$m.Groups[1].Value; $passed += [int]$m.Groups[2].Value }
    return "$passed/$($passed + $failed)"
}

function Get-ContradictingTestStatus([string]$Root) {
    $file = Join-Path $Root "$testDir/RepricingEngineTests.cs"
    $diff = @(git -C $Root diff -U0 -- "$testDir/RepricingEngineTests.cs")
    if ([IO.File]::ReadAllText($file).Contains($contradicting)) {
        if ($diff -match '^-.*(600\.00m|100\.00m, 200\.00m, 300\.00m)') { return 'kept, expectations changed' }
        return 'kept unchanged'
    }
    if ($diff -match '^\+.*450\.00m') { return 'renamed, now expects the discount' }
    return 'removed'
}

$rows = foreach ($dir in Get-ChildItem -Directory $OutDir | Sort-Object Name) {
    $root = $dir.FullName
    $eval = $dir.Name -replace '-\d+$', ''
    $grader = switch ($eval) {
        'rounding' { 'RoundOnceGrader' }
        'rounding-no-rule' { 'RoundOnceGrader' }
        'discount' { 'DiscountGrader' }
        default { $null }
    }
    if (-not $grader) { Write-Warning "Skipping $root : unknown eval '$eval'."; continue }
    Write-Host "Grading $($dir.Name)..."

    $changed = @(git -C $root status --porcelain)
    git -C $root diff --quiet -- src/Billing.Domain
    $engineChanged = $LASTEXITCODE -ne 0

    Push-Location $root
    try {
        $domain = Invoke-Tests $testDir $null
        $web = Invoke-Tests 'tests/Billing.Web.Tests' $null

        $graderFile = Join-Path $root "$testDir/$grader.cs"
        Copy-Item (Join-Path $graders "$grader.cs") $graderFile
        try { $hidden = Invoke-Tests $testDir "FullyQualifiedName~$grader" }
        finally { Remove-Item $graderFile }
    }
    finally { Pop-Location }

    [pscustomobject]@{
        Copy               = $dir.Name
        FilesChanged       = $changed.Count
        EngineChanged      = if ($engineChanged) { 'yes' } else { 'no' }
        ContradictingTest  = if ($eval -eq 'discount') { Get-ContradictingTestStatus $root } else { '' }
        DomainTests        = $domain
        WebTests           = $web
        HiddenGrader       = $hidden
    }
}

$rows | Format-Table -AutoSize
