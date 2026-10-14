# S9: kurt / Gunshots / Black Powder.wav (CC0). Keep the previous S1 source as history.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$original=Join-Path $root 'Docs/AudioOriginals/Handgun/Black Powder.wav'
if(!(Test-Path -LiteralPath $original)){
    throw 'Missing source backup. Download https://opengameart.org/sites/default/files/Black%20Powder.wav into Docs/AudioOriginals/Handgun/ first.'
}
if((Get-FileHash -LiteralPath $original).Hash -ne '495EADF1C1E41A1264FF75F08A58DE9004B3F9792391A174D70FC5C796C41744'){throw 'Unexpected Black Powder source'}
$output=Join-Path $root 'Assets/Resources/Audio/HandgunFire.wav'
# First powder report starts at ~147.75ms; preserve its attack and omit subsequent shots.
# No synthetic pitch glide, bit crushing, extra echo, or bomb sample. Shape only the recorded report.
& ffmpeg -y -v error -i $original -af 'atrim=start=0.1475:duration=0.32,asetpts=PTS-STARTPTS,pan=mono|c0=0.5*c0+0.5*c1,aresample=44100,highpass=f=70,lowpass=f=6500,equalizer=f=180:t=q:w=0.8:g=3,afade=t=in:d=0.00015,afade=t=out:st=0.08:d=0.24:curve=exp,volume=0.85' -map_metadata -1 -ac 1 -ar 44100 -c:a pcm_s16le $output
if($LASTEXITCODE -ne 0){throw 'Handgun conversion failed'}
Get-FileHash -LiteralPath $original,$output
& ffprobe -v error -show_entries format=duration,size -of json $output
