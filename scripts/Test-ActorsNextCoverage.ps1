param(
    [double] $LineThreshold = 0.90,
    [double] $BranchThreshold = 0.90,
    [switch] $SkipIntegration
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$runtimePackages = @(
    'Dapr.Actors.Next.Abstractions',
    'Dapr.Actors.Next.Core',
    'Dapr.Actors.Next.Testing',
    'Dapr.Actors.Next.StateMachine',
    'Dapr.Actors.Next.Streams',
    'Dapr.Actors.Next.Interpreted'
)

$sourceProjects = Get-ChildItem -Path (Join-Path $repoRoot 'src') -Directory -Filter 'Dapr.Actors.Next*' |
    ForEach-Object { Join-Path $_.FullName ($_.Name + '.csproj') } |
    Where-Object { Test-Path $_ }

$testProjects = @(
    'test/Dapr.Actors.Next.Abstractions.Test/Dapr.Actors.Next.Abstractions.Test.csproj',
    'test/Dapr.Actors.Next.Core.Test/Dapr.Actors.Next.Core.Test.csproj',
    'test/Dapr.Actors.Next.Testing.Test/Dapr.Actors.Next.Testing.Test.csproj',
    'test/Dapr.Actors.Next.StateMachine.Test/Dapr.Actors.Next.StateMachine.Test.csproj',
    'test/Dapr.Actors.Next.Streams.Test/Dapr.Actors.Next.Streams.Test.csproj',
    'test/Dapr.Actors.Next.Interpreted.Test/Dapr.Actors.Next.Interpreted.Test.csproj',
    'test/Dapr.Actors.Next.SourceGenerators.Test/Dapr.Actors.Next.SourceGenerators.Test.csproj',
    'test/Dapr.Actors.Next.Analyzers.Test/Dapr.Actors.Next.Analyzers.Test.csproj',
    'test/Dapr.Actors.Next.MetaConsumerSmoke.Test/Dapr.Actors.Next.MetaConsumerSmoke.Test.csproj'
)

if (-not $SkipIntegration) {
    $testProjects += 'test/Dapr.IntegrationTest.Actors.Next/Dapr.IntegrationTest.Actors.Next.csproj'
}

$testProjects = $testProjects |
    ForEach-Object { Join-Path $repoRoot $_ } |
    Where-Object { Test-Path $_ }

foreach ($project in $testProjects) {
    $testResults = Join-Path (Split-Path -Parent $project) 'TestResults'
    if (Test-Path $testResults) {
        $resolvedResults = (Resolve-Path $testResults).Path
        if (-not $resolvedResults.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to delete TestResults outside repository: $resolvedResults"
        }

        Remove-Item -LiteralPath $resolvedResults -Recurse -Force
    }
}

foreach ($project in $sourceProjects) {
    dotnet build $project -warnaserror --no-restore /m:1 /nr:false
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

foreach ($project in $testProjects) {
    if ($project.EndsWith('Dapr.Actors.Next.MetaConsumerSmoke.Test.csproj', [StringComparison]::OrdinalIgnoreCase)) {
        dotnet test $project --framework net8.0 /m:1 /nr:false
    }
    else {
        dotnet test $project --framework net8.0 --settings (Join-Path $repoRoot 'coverage.runsettings') --collect:"XPlat Code Coverage" /m:1 /nr:false
    }

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$reports = Get-ChildItem -Path (Join-Path $repoRoot 'test') -Recurse -Filter 'coverage.cobertura.xml'
if ($reports.Count -eq 0) {
    throw 'No Cobertura coverage reports were produced.'
}

$lineHits = @{}
$branchHits = @{}

function Normalize-CoverageFile([string] $packageName, [string] $fileName) {
    $normalized = $fileName.Replace('\', '/')
    $srcPrefix = 'src/'
    if ($normalized.StartsWith($srcPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        $normalized = $normalized.Substring($srcPrefix.Length)
    }

    $packagePrefix = "$packageName/"
    $packageIndex = $normalized.IndexOf($packagePrefix, [StringComparison]::OrdinalIgnoreCase)
    if ($packageIndex -ge 0) {
        $normalized = $normalized.Substring($packageIndex)
    }

    if (-not $normalized.StartsWith($packagePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        $normalized = "$packagePrefix$normalized"
    }

    return $normalized
}

foreach ($report in $reports) {
    [xml] $coverage = Get-Content $report.FullName
    foreach ($package in $coverage.coverage.packages.package) {
        $packageName = [string] $package.name
        if ($runtimePackages -notcontains $packageName) {
            continue
        }

        foreach ($class in $package.classes.class) {
            $file = Normalize-CoverageFile $packageName ([string] $class.filename)
            foreach ($line in $class.lines.line) {
                $lineKey = "$packageName|$file|$($line.number)"
                if (-not $lineHits.ContainsKey($lineKey)) {
                    $lineHits[$lineKey] = $false
                }

                if ([int] $line.hits -gt 0) {
                    $lineHits[$lineKey] = $true
                }

                if ([string] $line.branch -eq 'True') {
                    $conditionCoverage = [string] $line.'condition-coverage'
                    if ($conditionCoverage -match '\((\d+)/(\d+)\)') {
                        $covered = [int] $Matches[1]
                        $total = [int] $Matches[2]
                        if (-not $branchHits.ContainsKey($lineKey)) {
                            $branchHits[$lineKey] = @{ Covered = 0; Total = $total }
                        }

                        if ($covered -gt $branchHits[$lineKey].Covered) {
                            $branchHits[$lineKey].Covered = $covered
                        }

                        if ($total -gt $branchHits[$lineKey].Total) {
                            $branchHits[$lineKey].Total = $total
                        }
                    }
                }
            }
        }
    }
}

function Get-CoverageSummary([string[]] $packages) {
    $selectedLines = @{}
    $selectedBranches = @{}

    foreach ($key in $lineHits.Keys) {
        $packageName = $key.Substring(0, $key.IndexOf('|'))
        if ($packages -contains $packageName) {
            $selectedLines[$key] = $lineHits[$key]
        }
    }

    foreach ($key in $branchHits.Keys) {
        $packageName = $key.Substring(0, $key.IndexOf('|'))
        if ($packages -contains $packageName) {
            $selectedBranches[$key] = $branchHits[$key]
        }
    }

    $coveredLines = ($selectedLines.Values | Where-Object { $_ }).Count
    $totalLines = $selectedLines.Count
    $coveredBranches = 0
    $totalBranches = 0
    foreach ($entry in $selectedBranches.Values) {
        $coveredBranches += $entry.Covered
        $totalBranches += $entry.Total
    }

    [pscustomobject]@{
        CoveredLines = $coveredLines
        TotalLines = $totalLines
        LineRate = if ($totalLines -eq 0) { 1.0 } else { $coveredLines / $totalLines }
        CoveredBranches = $coveredBranches
        TotalBranches = $totalBranches
        BranchRate = if ($totalBranches -eq 0) { 1.0 } else { $coveredBranches / $totalBranches }
    }
}

foreach ($packageName in $runtimePackages) {
    $summary = Get-CoverageSummary @($packageName)
    Write-Host ("{0}: line={1:P2} ({2}/{3}) branch={4:P2} ({5}/{6})" -f $packageName, $summary.LineRate, $summary.CoveredLines, $summary.TotalLines, $summary.BranchRate, $summary.CoveredBranches, $summary.TotalBranches)
}

$solution = Get-CoverageSummary $runtimePackages
Write-Host ("Dapr.Actors.Next runtime solution: line={0:P2} ({1}/{2}) branch={3:P2} ({4}/{5})" -f $solution.LineRate, $solution.CoveredLines, $solution.TotalLines, $solution.BranchRate, $solution.CoveredBranches, $solution.TotalBranches)

if ($solution.LineRate -lt $LineThreshold -or $solution.BranchRate -lt $BranchThreshold) {
    throw ("Coverage threshold failed. Required line>={0:P2}, branch>={1:P2}; actual line={2:P2}, branch={3:P2}." -f $LineThreshold, $BranchThreshold, $solution.LineRate, $solution.BranchRate)
}
