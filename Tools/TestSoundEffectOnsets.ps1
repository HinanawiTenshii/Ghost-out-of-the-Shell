$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$expected = @{
    'cncl01'='aa6c3c9180d76034583f02cbc82ec3bf'; 'cncl02'='aeb480a29c46b6f48af18e906299a5f7'
    'cncl04'='405dc3a6d7539434986f2fce6db746a6'; 'cncl05'='8d51395eabe593544b0a793eb51f77fd'
    'cncl06'='51c05f0391c761f4d8a1a72d71b43a97'; 'cncl07'='aec7b25afa8a16d418995af322ef6b9d'
    'Hit'='f870327fc4b287448a8fe336bcb9421b'; 'MGS 驚嘆號音效'='ac58db20dd4248d4fab0bb07169cff60'
    'glassbrokechanged'='93bf4cef59a02b9468783c28dc705be7'
    'NpcAwarenessAlert'='2c396c3f92de495ba5f316f5d5dd5d60'
    'CharacterDamage'='a17c3a49fbcf473083f4f1dd7fd81937'; 'CharacterDeath'='e2a1b1451b81462ea6c3993835d46e7a'
    'BombExplosion'='f364770eb83947be9186bf367196a4ca'
}
$files = @(Get-ChildItem "$root/Assets/SoundEffect","$root/Assets/Resources/Audio" -File -Filter '*.wav')
if ($files.Count -ne $expected.Count) { throw 'Unexpected short audio asset inventory' }
foreach ($file in $files) {
    $meta = Get-Content -Raw ($file.FullName + '.meta')
    if ($meta -notmatch "guid: $($expected[$file.BaseName])") { throw "GUID changed: $file" }
    foreach ($setting in 'loadType: 0','compressionFormat: 0','preloadAudioData: 1','loadInBackground: 0') {
        if (!$meta.Contains($setting)) { throw "Incorrect import setting $setting in $file" }
    }
    $log = (& ffmpeg -hide_banner -i $file.FullName -t 0.15 -af 'silencedetect=noise=-40dB:d=0.0005' -f null - 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "Decode failed: $file" }
    $starts = [regex]::Matches($log,'silence_start: ([0-9.]+)')
    if ($starts.Count -gt 0 -and [double]::Parse($starts[0].Groups[1].Value,[Globalization.CultureInfo]::InvariantCulture) -eq 0) {
        $end = [regex]::Match($log,'silence_end: ([0-9.]+)')
        if (!$end.Success -or [double]::Parse($end.Groups[1].Value,[Globalization.CultureInfo]::InvariantCulture) -gt 0.003) { throw "Leading silence exceeds 3ms: $file" }
    }
}
Write-Output "PASS: $($files.Count) short audio assets decode, start within 3ms at -40dB, preload as PCM, and retain their GUIDs."
