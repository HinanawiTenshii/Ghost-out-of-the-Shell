# Sources/CC0 evidence: Docs/ElectricGateAudio.md. Rebuild only the three electric-gate clips.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$original = Join-Path $root 'Docs/AudioOriginals/ElectricGate'
$output = Join-Path $root 'Assets/Resources/Audio/ElectricGate'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$hashes = @{
    'shieldloop.wav'='BB5FDBA1FDBBD44A00143AF7DA8B86BF9581C3F2A8E22445C8601F51539DC801'
    'snaploop.wav'='350F26E58C47D2F061B0AA6681C245357258CB3084BCB0C48D34DDC8FCA28CDE'
    'shieldhit.wav'='6213E372A3AAAFA0A4B33CC04E2F62CA97D20F3D91CF3506464D8CC9DC899625'
}
foreach($file in $hashes.Keys) { if ((Get-FileHash -LiteralPath "$original/$file").Hash -ne $hashes[$file]) { throw "Source changed: $file" } }
# Crossfade identical copies, then keep exactly one cyclic period, starting in the overlap.
# End continues into the identical start rather than adding a silent gap each repetition.
$hum = '[0:a]highpass=f=65,lowpass=f=2800,volume=0.5,asplit=2[a][b];[a][b]acrossfade=d=0.04:c1=tri:c2=tri,atrim=start=0.982449:end=1.964898,asetpts=PTS-STARTPTS[out]'
& ffmpeg -y -v error -i "$original/shieldloop.wav" -filter_complex $hum -map '[out]' -map_metadata -1 -ac 1 -ar 44100 -c:a pcm_s16le "$output/CurrentLoop.wav"
if($LASTEXITCODE -ne 0){throw 'Hum conversion failed'}
$warning = '[0:a]highpass=f=170,lowpass=f=6500,acompressor=threshold=0.035:ratio=3:attack=2:release=60:makeup=2,alimiter=limit=0.8:level=0:latency=1,asplit=2[a][b];[a][b]acrossfade=d=0.04:c1=tri:c2=tri,atrim=start=4.897415:end=9.79483,asetpts=PTS-STARTPTS[out]'
& ffmpeg -y -v error -i "$original/snaploop.wav" -filter_complex $warning -map '[out]' -map_metadata -1 -ac 1 -ar 44100 -c:a pcm_s16le "$output/WarningLoop.wav"
if($LASTEXITCODE -ne 0){throw 'Warning conversion failed'}
# Remove measured ~52ms pre-roll; the discharge is sharp, with a short tail instead of a long explosion.
& ffmpeg -y -v error -i "$original/shieldhit.wav" -af 'atrim=start=0.052:end=0.492,asetpts=PTS-STARTPTS,highpass=f=100,lowpass=f=8000,volume=0.75,afade=t=in:d=0.0003,afade=t=out:st=0.26:d=0.18' -map_metadata -1 -ac 1 -ar 44100 -c:a pcm_s16le "$output/Discharge.wav"
if($LASTEXITCODE -ne 0){throw 'Discharge conversion failed'}
