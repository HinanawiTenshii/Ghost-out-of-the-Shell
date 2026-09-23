# Static geometry/reference/particle-setting checks and prefab-based preview.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$path=Join-Path $root 'Assets/Prefabs/Decorations/喷泉.prefab'
$prefab=Get-Content -Raw $path
$blocks=[regex]::Split($prefab,'(?m)(?=^--- !u!)')
$ids=@([regex]::Matches($prefab,'(?m)^--- !u!\d+ &(\d+)')|ForEach-Object{$_.Groups[1].Value})
if(@($ids|Group-Object|Where-Object Count -gt 1).Count){throw 'Duplicate IDs'}
foreach($ref in [regex]::Matches($prefab,'\{fileID: (\d+)\}')){
    if($ref.Groups[1].Value -ne '0' -and $ids -notcontains $ref.Groups[1].Value){throw 'Broken local reference'}
}
if(!(Get-Content -Raw ($path+'.meta')).Contains('guid: b49620ef6c5dded4caa68f9e2ff9795f')){throw 'Fountain GUID changed'}
$rt=$blocks|Where-Object{$_ -match '^--- !u!4 &6553058051394660024\b'}
if(!$rt.Contains('m_LocalScale: {x: 8, y: 8, z: 8}') -or !$rt.Contains('m_LocalPosition: {x: -15.865288, y: 5.3425097, z: -1.9148613}')){throw 'Existing placement/scale changed'}
$box=$blocks|Where-Object{$_ -match '^--- !u!61 &6553058051394660026\b'}
if(!$box -or !$box.Contains('m_Size: {x: 1, y: 1}') -or !$box.Contains('m_IsTrigger: 0') -or !$box.Contains('m_Enabled: 1')){throw 'Solid square collider mismatch'}
if($prefab -match 'CircleCollider2D:'){throw 'Old circular collider remains'}
$objects=@{};$transforms=@{};$renderers=@()
foreach($b in $blocks){
    if($b -notmatch '^--- !u!(\d+) &(\d+)'){continue};$type=$Matches[1];$id=$Matches[2]
    $go=[regex]::Match($b,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
    if($type -eq '1'){$objects[$id]=[regex]::Match($b,'m_Name: (.*)').Groups[1].Value.Trim()}
    if($type -eq '4' -and !$b.Contains('stripped')){
        if($id -eq '6553058051394660024'){$transforms[$go]=@{pos=@(0,0);size=@(1,1)};continue}
        if(!$b.Contains('m_Father: {fileID: 6553058051394660024}')){throw 'Unexpected geometry parent'}
        $pos=[regex]::Match($b,'m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
        $size=[regex]::Match($b,'m_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
        if([double]$pos.Groups[3].Value -ne 0 -or $b -notmatch 'm_LocalRotation: \{x: -?0, y: -?0, z: -?0, w: 1\}'){throw 'Fountain must be flat top-down rectangles'}
        $transforms[$go]=@{pos=@([double]$pos.Groups[1].Value,[double]$pos.Groups[2].Value);size=@([double]$size.Groups[1].Value,[double]$size.Groups[2].Value)}
    }
    if($type -eq '212'){
        if(!$b.Contains('guid: 311925a002f4447b3a28927169b83ea6')){throw 'Non-Square fountain geometry'}
        $c=[regex]::Match($b,'m_Color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)')
        $renderers+=@{go=$go;order=[int][regex]::Match($b,'m_SortingOrder: (-?\d+)').Groups[1].Value;color=@([float]$c.Groups[1].Value,[float]$c.Groups[2].Value,[float]$c.Groups[3].Value,[float]$c.Groups[4].Value)}
    }
}
if($renderers.Count -ne 11){throw 'Unexpected geometry count'}
$bed=Get-Content -Raw "$root/Assets/Prefabs/Decorations/Plants/GeometricFlowerbed.prefab"
foreach($color in @('m_Color: {r: 0.66, g: 0.68, b: 0.7, a: 1}','m_Color: {r: 0.43, g: 0.46, b: 0.49, a: 1}')){
    # Fountain and flowerbed share the same neutral gray stone palette.
    if(!$bed.Contains($color) -or !$prefab.Contains($color)){throw 'Stone palette differs from flowerbed'}
}
$emitter=$blocks|Where-Object{$_ -match '^--- !u!1001 &6553058052248252519\b'}
if(!$emitter.Contains('m_SourcePrefab: {fileID: 100100000, guid: e6b9c85d88584ae18576d9e51bc4c08a, type: 3}')){throw 'Existing water emitter disconnected'}
function Setting($key){
    $m=[regex]::Match($emitter,'propertyPath: '+[regex]::Escape($key)+'\s+value: ([^\r\n]+)')
    if(!$m.Success){throw "Missing fountain-local override $key"};return [double]$m.Groups[1].Value
}
if((Setting 'particlesPerSecond') -le 0 -or (Setting 'maximumParticles') -gt 64){throw 'Emission disabled or excessive'}
if((Setting 'particleColor.a') -gt .7 -or (Setting 'fadeOutStart') -gt .55){throw 'Water particles too prominent'}
foreach($axis in 'x','y','z'){if((Setting "m_LocalScale.$axis") -ne 1){throw 'Emitter local scale mismatch'}}
foreach($axis in 'x','y'){if((Setting "m_LocalPosition.$axis") -ne 0){throw 'Spray not centered'}}
# World-space lifetime/speed with the emitter's Local scaling mode (unit scale).
# Use the full parent scale as a conservative emission-shape radius bound.
$reach=(Setting 'emissionRadius')*8+(Setting 'outwardSpeed')*1.25*(Setting 'particleLifetime')*1.2+(Setting 'particleSize')*1.25*.5
if($reach -ge .878*8/2){throw 'Particle travel estimate extends outside water'}
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(740,740);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::FromArgb(18,28,62))
foreach($r in ($renderers|Sort-Object order)){
    $t=$transforms[$r.go]
    foreach($axis in 0..1){if([Math]::Abs($t.pos[$axis])+$t.size[$axis]/2 -gt .500001){throw 'Detail outside outer rim'}}
    $c=$r.color;$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb([int](255*$c[3]),[int](255*$c[0]),[int](255*$c[1]),[int](255*$c[2])))
    $g.FillRectangle($brush,[float](370+($t.pos[0]-$t.size[0]/2)*640),[float](370-($t.pos[1]+$t.size[1]/2)*640),[float]($t.size[0]*640),[float]($t.size[1]*640));$brush.Dispose()
}
$preview=Join-Path $root 'Docs/GeometricFountain-preview.png'
$bmp.Save($preview,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
Write-Output "Preview (static geometry, no particle simulation): $preview"
Write-Output 'PASS: original asset GUID/placement/scale preserved; 11 Square sprites; matching flowerbed stone palette; fitted solid box collider; existing emitter retained with fountain-only overrides.'
