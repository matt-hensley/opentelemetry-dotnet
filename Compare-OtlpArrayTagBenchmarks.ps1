[CmdletBinding()]
param(
    [switch]$ShortRun,
    [int]$RepeatCount = 0,
    [string]$OutputPath
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = $scriptRoot
$genericRunner = 'C:\Users\mhensley\.agents\skills\benchmarkdotnet-worktree-compare\scripts\Compare-Benchmarks.ps1'
$runnerDirectory = Join-Path $repoRoot '.benchmark-runner\level1'

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot 'BenchmarkDotNet.Artifacts\results\ProtobufOtlpArrayTagSerializationBenchmarks-Comparison.md'
}

$harnessFiles = @(
    'test\Benchmarks\Benchmarks.csproj',
    'test\Benchmarks\Program.cs',
    'test\Benchmarks\Helper\ActivityHelper.cs',
    'test\Benchmarks\Exporter\ProtobufOtlpArrayTagSerializationBenchmarks.cs',
    'test\Benchmarks\Exporter\ProtobufOtlpArrayTagSerializationShortRunBenchmarks.cs'
)

$benchmarkFilter = if ($ShortRun) { '*ProtobufOtlpArrayTagSerializationShortRunBenchmarks*' } else { '*ProtobufOtlpArrayTagSerializationBenchmarks*' }
$stepDefinitions = @(
    [pscustomobject]@{ Name = 'Baseline'; Root = 'Baseline'; DisplayName = 'main'; Filter = $benchmarkFilter },
    [pscustomobject]@{ Name = 'Feature'; Root = 'Feature'; DisplayName = 'feature'; Filter = $benchmarkFilter }
)

$scenarioDefinitions = @(
    [pscustomobject]@{ Step = 'Baseline'; Scenario = 'main / sequential array tags'; Method = 'SerializeSequentialArrayTags' },
    [pscustomobject]@{ Step = 'Feature'; Scenario = 'feature / sequential array tags'; Method = 'SerializeSequentialArrayTags' }
)
$deltaComparisons = @()

if (-not (Test-Path $runnerDirectory)) {
    New-Item -ItemType Directory -Path $runnerDirectory -Force | Out-Null
}

$localRunner = Join-Path $runnerDirectory 'Compare-Benchmarks.ps1'
Copy-Item -LiteralPath $genericRunner -Destination $localRunner -Force

try {
& $localRunner `
    -Title 'OTLP Sequential Array-Tag Serialization Comparison' `
    -MainRef 'main' `
    -FeaturePath $repoRoot `
    -Framework 'net10.0' `
    -Configuration 'Release' `
    -BenchmarkProjectRelativePath 'test\Benchmarks\Benchmarks.csproj' `
    -BenchmarkClassName 'ProtobufOtlpArrayTagSerializationBenchmarks' `
    -ShortRunBenchmarkClassName 'ProtobufOtlpArrayTagSerializationShortRunBenchmarks' `
    -ReportNamespacePrefix 'Benchmarks.Exporter' `
    -HarnessFiles $harnessFiles `
    -StepDefinitions $stepDefinitions `
    -ScenarioDefinitions $scenarioDefinitions `
    -DeltaComparisons $deltaComparisons `
    -RunOrderOdd @('Baseline', 'Feature') `
    -RunOrderEven @('Feature', 'Baseline') `
    -RepeatCount $RepeatCount `
    -OutputPath $OutputPath `
    -Notes @(
        'The benchmark writes one activity containing 16 sequential int[] attributes (16 values each) through ProtobufOtlpTraceSerializer.WriteTraceData.',
        'The comparison isolates the feature change that retains a 2 KiB array scratch buffer per serializer thread; unusually large arrays are not part of this scenario.',
        'ArrayPool rent/return calls are internal to the serializer and are not directly counted; allocation and timing medians are reported by BenchmarkDotNet.'
    ) `
    -ShortRun:$ShortRun
}
finally {
    if (Test-Path $runnerDirectory) {
        Remove-Item -LiteralPath $runnerDirectory -Recurse -Force
    }
}
