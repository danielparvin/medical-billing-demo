# Builds fresh, history-free copies of the repo for the agent evals described in evals/README.md.
# Each copy is a snapshot of -BaseCommit (default f52c5b8, before the talk demos existed) with one "Initial commit",
# no tags and no talk material in the README, so an agent can't find the answers in the repo's history.
#
#   ./evals/prepare-evals.ps1 -OutDir C:\temp\evals                 # all evals, 5 copies each
#   ./evals/prepare-evals.ps1 -OutDir C:\temp\evals -Eval discount -Runs 3
param(
    [Parameter(Mandatory)][string]$OutDir,
    [ValidateSet('all', 'rounding', 'rounding-no-rule', 'discount')][string]$Eval = 'all',
    [ValidateRange(1, 20)][int]$Runs = 5,
    [string]$BaseCommit = 'f52c5b8'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent

function Invoke-Git {
    git @args
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed." }
}

function Edit-File([string]$Path, [string]$Old, [string]$New) {
    $text = [IO.File]::ReadAllText($Path)
    $count = ([regex]::Matches($text, [regex]::Escape($Old))).Count
    if ($count -ne 1) { throw "Expected '$Old' once in $Path, found it $count times." }
    [IO.File]::WriteAllText($Path, $text.Replace($Old, $New), [Text.UTF8Encoding]::new($false))
}

# Removes the README's checkpoints, talk arc and live-coding script, which name the discount, its tags and the test it
# contradicts, and the capabilities index's pointer to that script.
function Remove-TalkMaterial([string]$Root) {
    $readme = Join-Path $Root 'README.md'
    $text = [IO.File]::ReadAllText($readme)
    $start = $text.IndexOf('## Checkpoints')
    $end = $text.IndexOf('## Known exceptions')
    if ($start -lt 0 -or $end -le $start) { throw "Could not find the talk material in $readme." }
    [IO.File]::WriteAllText($readme, $text.Remove($start, $end - $start), [Text.UTF8Encoding]::new($false))

    Edit-File (Join-Path $Root 'docs/capabilities/README.md') `
        'It exists only as the live-coding exercise in the [project README](../../README.md); on `main`, every line pays in full.' `
        'On `main`, every line pays in full.'
}

# Removes every written statement that the engine rounds once per line, after all the multipliers.
function Remove-RoundingRule([string]$Root) {
    $engine = Join-Path $Root 'src/Billing.Domain/Pricing/RepricingEngine.cs'
    Edit-File $engine `
        '/// Rounding rule: once per line, to 2 decimals, MidpointRounding.AwayFromZero, after all multipliers.' `
        '/// Rounding: to 2 decimals, MidpointRounding.AwayFromZero.'
    Edit-File $engine `
        "A priced line's amount is rounded once and then capped at its billed amount." `
        "A priced line's amount is rounded and then capped at its billed amount."
    Edit-File (Join-Path $Root 'docs/capabilities/bill-processing.md') `
        "shall round each line's amount once, to 2 decimals, after all factors. (untested)" `
        "shall round each line's amount to 2 decimals. (untested)"
}

$evals = if ($Eval -eq 'all') { 'rounding', 'rounding-no-rule', 'discount' } else { @($Eval) }
New-Item -ItemType Directory -Force $OutDir | Out-Null

foreach ($name in $evals) {
    foreach ($run in 1..$Runs) {
        $root = Join-Path $OutDir "$name-$run"
        if (Test-Path $root) { throw "$root already exists. Remove it or choose another -OutDir." }

        Invoke-Git clone --quiet $repo $root
        Invoke-Git -C $root checkout --quiet $BaseCommit
        Remove-Item -Recurse -Force (Join-Path $root '.git')

        Remove-TalkMaterial $root
        if ($name -eq 'rounding-no-rule') { Remove-RoundingRule $root }

        Invoke-Git -C $root init --quiet --initial-branch main
        Invoke-Git -C $root add --all
        Invoke-Git -C $root -c user.name=Developer -c user.email=dev@example.test commit --quiet -m 'Initial commit'
        Write-Host "Prepared $root"
    }
}

Write-Host ''
Write-Host "Next: in each folder, give a fresh agent session the prompt from evals/prompts/<eval>.md, then run"
Write-Host "./evals/grade-evals.ps1 -OutDir $OutDir"
