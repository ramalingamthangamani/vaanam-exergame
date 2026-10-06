# Generates English voice lines for every phrase in Localization.cs using the
# Windows speech engine (Microsoft Heera, Indian English), as WAV files in
# Assets/_Vaanam/Resources/Voice/en/.
#
# Tamil: Windows on this machine has no Tamil voice, so no Tamil WAVs are made.
# Record them (phone is fine) and drop them in Resources/Voice/ta/<key>.wav;
# the game picks them up automatically and falls back to English otherwise.
#
# Usage (PowerShell):  .\Tools\render-voice-lines.ps1
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$VoiceName = "Heera"
)
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Media.SpeechSynthesis.SpeechSynthesizer, Windows.Media, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType = WindowsRuntime]

$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
        $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($WinRtTask, $ResultType) {
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(-1) | Out-Null
    $netTask.Result
}

$locFile = Join-Path $ProjectRoot "Assets\_Vaanam\Scripts\Core\Localization.cs"
$outDir = Join-Path $ProjectRoot "Assets\_Vaanam\Resources\Voice\en"
New-Item -ItemType Directory -Force $outDir | Out-Null
New-Item -ItemType Directory -Force (Join-Path $ProjectRoot "Assets\_Vaanam\Resources\Voice\ta") | Out-Null

$phrases = @()
foreach ($line in (Get-Content $locFile -Encoding UTF8)) {
    if ($line -match '\{\s*"([a-z0-9_]+)",\s*\("([^"]*)",\s*"([^"]*)"\)\s*\}') {
        $phrases += [pscustomobject]@{ Key = $Matches[1]; En = $Matches[2] }
    }
}

$synth = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
$voice = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices | Where-Object { $_.DisplayName -match $VoiceName } | Select-Object -First 1
if ($voice) { $synth.Voice = $voice; Write-Host "Voice: $($voice.DisplayName)" } else { Write-Host "Voice '$VoiceName' not found, using default $($synth.Voice.DisplayName)" }

foreach ($p in $phrases) {
    $text = $p.En
    if ($p.Key -eq "title") { $text = "Vaanam" }
    # Slightly slower and warmer for small children.
    $ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-IN'><prosody rate='-12%' pitch='+5%'>$([System.Security.SecurityElement]::Escape($text))</prosody></speak>"
    $stream = Await ($synth.SynthesizeSsmlToStreamAsync($ssml)) ([Windows.Media.SpeechSynthesis.SpeechSynthesisStream])
    $size = [uint32]$stream.Size
    $reader = New-Object Windows.Storage.Streams.DataReader($stream.GetInputStreamAt(0))
    $null = Await ($reader.LoadAsync($size)) ([uint32])
    $bytes = [byte[]]::new($size)
    $reader.ReadBytes($bytes)
    [IO.File]::WriteAllBytes((Join-Path $outDir "$($p.Key).wav"), $bytes)
    Write-Host ("  {0,-14} {1}" -f $p.Key, $text)
}
Write-Host "Wrote $($phrases.Count) voice lines to $outDir"
