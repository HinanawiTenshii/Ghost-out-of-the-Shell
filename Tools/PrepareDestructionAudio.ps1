# CC0 recordings and hashes: Docs/DestructionAudio.md.
# Rebuilds only the two destruction clips; keeps their existing Unity GUIDs.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$audio = Join-Path $root '.codex-temp/Audio/DestructionV2'
$sources = @{
    'wood_impact/impactwood24.mp3.flac' = '259BC566A701602D2CA57CDB5963175A54E62FEB83BB22CE04C33021E74E7A1A'
    'wood_impact/crack07.mp3.flac' = '9EFA670DAAEDF79E101FD3463EA6F585CCC1079DC7E23785947E68886B276A44'
    '244238_3156517-hq.mp3' = 'F70DB0F3705B1C5344D2BF841CFB2E65287D7ECCA2C55D4692FA45E11D0EB456'
}
foreach ($source in $sources.Keys) {
    $path = Join-Path $audio $source
    if (!(Test-Path -LiteralPath $path)) { throw "Missing source: $path" }
    if ((Get-FileHash -LiteralPath $path).Hash -ne $sources[$source]) { throw "Source hash mismatch: $source" }
}

# Sharp wood splitting over a longer impact/debris recording. Keep natural pitch.
# Limiter latency is compensated; the single combined clip costs no extra runtime voice.
$woodFilter = '[0:a]atrim=start=0.086:end=1.42,asetpts=PTS-STARTPTS,highpass=f=55,lowpass=f=10500,volume=0.72[impact];[1:a]atrim=start=0.013:end=0.73,asetpts=PTS-STARTPTS,highpass=f=100,lowpass=f=10500,volume=0.42,afade=t=out:st=0.56:d=0.157[crack];[impact][crack]amix=inputs=2:duration=longest:normalize=0,alimiter=limit=0.85:level=0:latency=1,afade=t=in:d=0.0005,afade=t=out:st=1.10:d=0.234[out]'
& ffmpeg -y -v error -i "$audio/wood_impact/impactwood24.mp3.flac" -i "$audio/wood_impact/crack07.mp3.flac" -filter_complex $woodFilter -map '[out]' -map_metadata -1 -ac 1 -ar 44100 -c:a pcm_s16le "$root/Assets/Resources/Audio/DoorBreak.wav"
if ($LASTEXITCODE -ne 0) { throw 'Door audio preparation failed' }

# Public HQ preview of the CC0 recording; original WAV requires site login.
# Keep the actual smash and falling shards, not just the initial tick.
& ffmpeg -y -v error -i "$audio/244238_3156517-hq.mp3" -af 'atrim=start=0.187:end=1.44,asetpts=PTS-STARTPTS,highpass=f=90,lowpass=f=11000,volume=0.95,alimiter=limit=0.85:level=0:latency=1,afade=t=in:d=0.0005,afade=t=out:st=1.04:d=0.213' -map_metadata -1 -ac 1 -ar 44100 -c:a pcm_s16le "$root/Assets/Resources/Audio/GlassBreak.wav"
if ($LASTEXITCODE -ne 0) { throw 'Glass audio preparation failed' }
