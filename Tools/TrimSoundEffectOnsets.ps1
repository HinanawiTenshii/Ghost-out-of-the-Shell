# One-time asset migration: keep Unity GUIDs and original clips outside Assets.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$backup = Join-Path $root ('.codex-temp/Audio/OnsetTrim-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$clips = @(
    @{Path='Assets/SoundEffect/cncl01.mp3'; Start=0.0681383},
    @{Path='Assets/SoundEffect/cncl02.mp3'; Start=0.0499524},
    @{Path='Assets/SoundEffect/cncl04.mp3'; Start=0.0415170},
    @{Path='Assets/SoundEffect/cncl05.mp3'; Start=0.0227188},
    @{Path='Assets/SoundEffect/cncl06.mp3'; Start=0.0520612},
    @{Path='Assets/SoundEffect/cncl07.mp3'; Start=0.0613356},
    @{Path='Assets/SoundEffect/Hit.mp3'; Start=0.0809728},
    @{Path='Assets/SoundEffect/MGS 驚嘆號音效.mp3'; Start=0.121472},
    @{Path='Assets/Resources/Audio/NpcAwarenessAlert.wav'; Start=0.0128549}
)
foreach ($clip in $clips) {
    $source = [IO.Path]::GetFullPath((Join-Path $root $clip.Path))
    $destination = [IO.Path]::ChangeExtension($source, '.wav')
    if (!$source.StartsWith($root + [IO.Path]::DirectorySeparatorChar) -or !(Test-Path -LiteralPath $source)) { throw "Invalid/missing source $source" }
    if ($source -ne $destination -and ((Test-Path -LiteralPath $destination) -or (Test-Path -LiteralPath "$destination.meta"))) { throw "Destination already exists: $destination" }
}
New-Item -ItemType Directory -Path $backup | Out-Null
foreach ($clip in $clips) {
    $source = [IO.Path]::GetFullPath((Join-Path $root $clip.Path))
    $destination = [IO.Path]::ChangeExtension($source, '.wav')
    $original = Join-Path $backup ([IO.Path]::GetFileName($source))
    $pending = Join-Path $backup ([IO.Path]::GetFileNameWithoutExtension($source) + '-trimmed.wav')
    $guid = (Select-String -LiteralPath "$source.meta" -Pattern '^guid:').Line
    Copy-Item -LiteralPath "$source.meta" -Destination "$original.meta"
    $start = $clip.Start.ToString('0.0000000', [Globalization.CultureInfo]::InvariantCulture)
    # Preserve internal pauses/tails and channel count. A sub-millisecond fade avoids a cut click.
    & ffmpeg -hide_banner -loglevel error -n -i $source -af "atrim=start=$start,asetpts=PTS-STARTPTS,afade=t=in:d=0.0005" -c:a pcm_s16le $pending
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $pending)) { throw "Conversion failed: $source" }
    Move-Item -LiteralPath $source -Destination $original
    if ($source -ne $destination) { Move-Item -LiteralPath "$source.meta" -Destination "$destination.meta" }
    Move-Item -LiteralPath $pending -Destination $destination
    if ((Select-String -LiteralPath "$destination.meta" -Pattern '^guid:').Line -ne $guid) { throw 'Audio GUID changed' }
    Write-Output "$($clip.Path): trimmed $start seconds, GUID retained"
}
Write-Output "Original clips and import settings retained in $backup"
