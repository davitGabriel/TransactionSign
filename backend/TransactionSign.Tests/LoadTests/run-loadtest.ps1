<#
.SYNOPSIS
    Runs k6 load test with database validation.

.PARAMETER BaseUrl
    API base URL (default: http://localhost:5000)

.PARAMETER Quick
    Run quick 5-minute test instead of full 2-hour test

.PARAMETER SkipValidation
    Skip post-test database validation

.EXAMPLE
    .\run-loadtest.ps1 -Quick
    .\run-loadtest.ps1 -BaseUrl "http://localhost:5001"
#>
param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$SqlServer = "(localdb)\mssqllocaldb",
    [string]$Database = "TransactionSignDb",
    [switch]$Quick,
    [switch]$SkipValidation
)

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot

# Set UTF-8 encoding for proper k6 output (progress bars, checkmarks, etc.)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# Create results directory
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$resultsDir = Join-Path $ScriptDir "results\$timestamp"
New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null

$k6Script = Join-Path $ScriptDir "k6-workflow.js"
$sqlScript = Join-Path $ScriptDir "validate-db.sql"

Write-Host "========================================"
Write-Host "Transaction Signing Load Test"
Write-Host "========================================"
Write-Host "Mode:    $(if ($Quick) { 'Quick (5min)' } else { 'Full (2h)' })"
Write-Host "API:     $BaseUrl"
Write-Host "Results: $resultsDir"
Write-Host "========================================"
Write-Host ""

# Run k6
$env:BASE_URL = $BaseUrl
$env:QUICK = if ($Quick) { "1" } else { "0" }

$k6Log = Join-Path $resultsDir "k6-output.log"
$k6Json = Join-Path $resultsDir "k6-summary.json"

Write-Host "Running k6 load test..."
k6 run --summary-export $k6Json $k6Script 2>&1 | Tee-Object -FilePath $k6Log

# Display k6 summary
if (Test-Path $k6Json) {
    Write-Host ""
    Write-Host "========================================"
    Write-Host "LOAD TEST RESULTS"
    Write-Host "========================================"

    $summary = Get-Content $k6Json | ConvertFrom-Json

    $httpReqs = $summary.metrics.http_reqs.values.count
    $iterations = $summary.metrics.iterations.values.count
    $successSigns = if ($summary.metrics.successful_signs) { $summary.metrics.successful_signs.values.count } else { "N/A" }
    $failures = if ($summary.metrics.workflow_failures) { $summary.metrics.workflow_failures.values.count } else { 0 }

    Write-Host "  HTTP Requests:      $httpReqs"
    Write-Host "  Iterations:         $iterations"
    Write-Host "  Successful Signs:   $successSigns"
    Write-Host "  Workflow Failures:  $failures"
    Write-Host "========================================"
}

Write-Host ""
Write-Host "========================================"
Write-Host "Results saved to: $resultsDir"
Write-Host "========================================"
