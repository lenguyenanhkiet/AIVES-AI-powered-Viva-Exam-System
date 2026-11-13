param([ValidateSet('seed', 'start')][string]$Action = 'seed')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

# Environment settings take precedence; load only the supported local .env names.
$mapping = @{
    AI_API_KEY='Ai__ApiKey'; AI_MODEL='Ai__Model'; AI_BASE_URL='Ai__BaseUrl'
    ELEVENLABS_API_KEY='ElevenLabs__ApiKey'; ELEVENLABS_VOICE_ID='ElevenLabs__VoiceId'
    ELEVENLABS_MODEL='ElevenLabs__Model'; ELEVENLABS_STT_MODEL='ElevenLabs__SttModel'
    ELEVENLABS_REALTIME_STT_MODEL='ElevenLabs__RealtimeSttModel'; JWT_KEY='Jwt__Key'
}
$envFile = Join-Path $repoRoot '.env'
if (Test-Path -LiteralPath $envFile) {
    foreach ($line in Get-Content -LiteralPath $envFile -Encoding UTF8) {
        if ($line -match '^\s*([A-Z_]+)\s*=(.*)$' -and $mapping.ContainsKey($Matches[1])) {
            $target = $mapping[$Matches[1]]
            if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($target, 'Process'))) {
                $value = $Matches[2].Trim().Trim('"').Trim("'")
                [Environment]::SetEnvironmentVariable($target, $value, 'Process')
            }
        }
    }
}
$env:ConnectionStrings__DefaultConnection = 'Server=localhost,1433;Database=AivesPanelDemo;Integrated Security=True;TrustServerCertificate=True'
$env:VivaAudio__RootDirectory = Join-Path $PSScriptRoot 'DemoSeed/.local/recordings'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5192'
$env:Logging__LogLevel__Default = 'Warning'
if (-not $env:DOTNET_ROOT -and (Test-Path -LiteralPath 'C:\Program Files\dotnet')) { $env:DOTNET_ROOT = 'C:\Program Files\dotnet' }
Push-Location $repoRoot
try {
    if ($Action -eq 'seed') {
        & dotnet run --project tools/DemoSeed -- --root $repoRoot
    } else {
        if ([string]::IsNullOrWhiteSpace($env:Jwt__Key)) { throw 'Set JWT_KEY in .env or Jwt__Key in the environment.' }
        & dotnet run --project AIpoweredVivaExamSystem.Presentation --no-launch-profile
    }
    if ($LASTEXITCODE -ne 0) { throw "Demo $Action failed (exit $LASTEXITCODE)." }
} finally { Pop-Location }
