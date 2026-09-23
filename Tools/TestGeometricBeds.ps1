# Validate and preview actual prefab rectangles, with equal world scale across beds.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$specs=@(
    @{name='Normal';guid='13eff7937548385469ce8d1bb27b6d0f';width=.6708261;cx=.45352697;count=8;rootPos='m_LocalPosition: {x: -0.0044269916, y: -2.053828, z: -2.9776812}';px=170},
    @{name='Guard';guid='214cab8865d7b3f478f51dedd957dd37';width=.6708261;cx=.45352697;count=8;rootPos='m_LocalPosition: {x: -0.0044269916, y: -2.053828, z: -2.9776812}';px=440},
    @{name='Luxury';guid='35027d7b0468eb54693cbf12cc9acb5b';width=1.3586911;cx=.7975;count=10;rootPos='m_LocalPosition: {x: 19.2739, y: -2.9829245, z: -2.9776812}';px=820}
)
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(1080,540);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(18,28,62))
$font=[Drawing.Font]::new('Segoe UI',15)
$label=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(189,205,213))
foreach($spec in $specs){
    $path="$root/Assets/Prefabs/Decorations/Bed-$($spec.name).prefab"
    $text=Get-Content -Raw $path
    if(!(Get-Content -Raw ($path+'.meta')).Contains('guid: '+$spec.guid)){throw 'Prefab identity changed'}
    if($text -match 'MonoBehaviour:|Collider2D:|--- !u!1001 '){throw 'Unexpected script, collision, or nested prefab'}
    if($text -match '[^\n]--- !u!'){throw 'Malformed YAML document boundary'}
    $blocks=[regex]::Split($text,'(?m)(?=^--- !u!)')
    $ids=@([regex]::Matches($text,'(?m)^--- !u!\d+ &(\d+)')|ForEach-Object{$_.Groups[1].Value})
    if(@($ids|Group-Object|Where-Object Count -gt 1).Count){throw 'Duplicate object IDs'}
    foreach($ref in [regex]::Matches($text,'\{fileID: (\d+)\}')){
        if($ref.Groups[1].Value -ne '0' -and $ids -notcontains $ref.Groups[1].Value){throw 'Broken local reference'}
    }
    $objects=@{};$transforms=@{};$sprites=@();$rootTf=$null
    foreach($block in $blocks){
        if($block -notmatch '^--- !u!(\d+) &(\d+)'){continue};$type=$Matches[1];$id=$Matches[2]
        $go=[regex]::Match($block,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
        if($type -eq '1'){$objects[$id]=[regex]::Match($block,'m_Name: (.*)').Groups[1].Value.Trim()}
        if($type -eq '4'){
            if($block.Contains('m_Father: {fileID: 0}')){$rootTf=$block;continue}
            $p=[regex]::Match($block,'m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
            $s=[regex]::Match($block,'m_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
            if([Math]::Abs([double]$p.Groups[3].Value-2.9776812) -gt 1e-7){throw 'Original local depth changed'}
            if($block -notmatch 'm_LocalRotation: \{x: -?0, y: -?0, z: -?0, w: 1\}'){throw 'Expected axis-aligned rectangles'}
            $transforms[$go]=@{pos=@([double]$p.Groups[1].Value,[double]$p.Groups[2].Value);size=@([double]$s.Groups[1].Value,[double]$s.Groups[2].Value)}
        }
        if($type -eq '212'){
            if(!$block.Contains('guid: 311925a002f4447b3a28927169b83ea6')){throw 'Expected built-in Square sprite'}
            $c=[regex]::Match($block,'m_Color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)')
            if([double]$c.Groups[4].Value -ne 1){throw 'Unexpected transparency'}
            $order=[int][regex]::Match($block,'m_SortingOrder: (-?\d+)').Groups[1].Value
            if($order -gt -1 -or $order -lt -5){throw 'Bed must stay below character sorting'}
            $sprites+=@{go=$go;order=$order;color=@([double]$c.Groups[1].Value,[double]$c.Groups[2].Value,[double]$c.Groups[3].Value)}
        }
    }
    if(!$rootTf.Contains($spec.rootPos) -or !$rootTf.Contains('m_LocalScale: {x: 1, y: 1, z: 1}')){throw 'Root placement changed'}
    if($sprites.Count -ne $spec.count){throw 'Unexpected renderer count'}
    $pillows=@($objects.Values|Where-Object{$_ -like 'Bed-pillow*'})
    if($pillows.Count -ne $(if($spec.name -eq 'Luxury'){2}else{1})){throw 'Incorrect pillow count'}
    foreach($name in 'Bed-Basic','Bed-inner1','Bed-inner2','Bed-blanket','Bed-Headboard','Bed-Footboard','Bed-BlanketFold'){
        if($objects.Values -notcontains $name){throw "Missing recognizable bed part: $name"}
    }
    foreach($sprite in ($sprites|Sort-Object order)){
        $t=$transforms[$sprite.go];$dx=$t.pos[0]-$spec.cx;$dy=$t.pos[1]+.017971992
        if([Math]::Abs($dx)+$t.size[0]/2 -gt $spec.width/2+1e-7 -or [Math]::Abs($dy)+$t.size[1]/2 -gt 1.1950932/2+1e-7){throw 'New visual exceeds original bed footprint'}
        $c=$sprite.color;$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int](255*$c[0]),[int](255*$c[1]),[int](255*$c[2])))
        $g.FillRectangle($brush,[float]($spec.px+($dx-$t.size[0]/2)*280),[float](282-($dy+$t.size[1]/2)*280),[float]($t.size[0]*280),[float]($t.size[1]*280));$brush.Dispose()
    }
    $g.DrawString("Bed-$($spec.name)",$font,$label,[float]($spec.px-70),[float]54)
    Write-Output "PASS: Bed-$($spec.name), $($sprites.Count) Square sprites; preserved identity, placement, footprint and no-collider behavior."
}
$small=[Drawing.Font]::new('Segoe UI',11)
$g.DrawString('Top-down geometric beds | same world scale | static prefab preview',$small,$label,270,494)
$small.Dispose();$font.Dispose();$label.Dispose()
$out="$root/Docs/GeometricBeds-preview.png"
$bmp.Save($out,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
Write-Output "Preview: $out"
