# Puts a deliberate rounding bug into the repricing engine for the talk: each modifier rounds as it is applied,
# instead of rounding once per line after all the multipliers. Every existing test still passes.
# Run with -Reset to discard all uncommitted changes to the engine and restore the correct code.
param([switch]$Reset)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

$engine = 'src/Billing.Domain/Pricing/RepricingEngine.cs'

if ($Reset) {
    git restore -- $engine
    if ($LASTEXITCODE -ne 0) { throw "git restore failed for $engine." }
    Write-Host "Restored $engine."
    return
}

$path = Join-Path (Get-Location) $engine
$source = [IO.File]::ReadAllText($path)

foreach ($factor in 'BilateralFactor', 'ProfessionalComponentFactor') {
    $correct = "amount *= $factor;"
    if (-not $source.Contains($correct)) {
        throw "Could not find '$correct' in $engine. Is the bug already applied? Use -Reset to restore the engine."
    }
    $source = $source.Replace($correct, "amount = Math.Round(amount * $factor, 2, MidpointRounding.AwayFromZero);")
}

[IO.File]::WriteAllText($path, $source, [Text.UTF8Encoding]::new($false))
Write-Host "Rounding bug applied to $engine. Run 'git diff' to see it, or use -Reset to restore the engine."
