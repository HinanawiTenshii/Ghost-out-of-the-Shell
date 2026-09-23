$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$path="$root/Assets/Prefabs/Decorations/NoticeBoard.prefab"
$text=Get-Content -Raw $path
$checks=0
function Check($ok,$label){if(!$ok){throw $label};$script:checks++}
$ids=@([regex]::Matches($text,'(?m)^--- !u!\d+ &(\d+)')|ForEach-Object{$_.Groups[1].Value})
Check (@($ids|Group-Object|Where-Object Count -gt 1).Count -eq 0) 'Unique object IDs'
foreach($ref in [regex]::Matches($text,'\{fileID: (\d+)\}')){Check ($ref.Groups[1].Value -eq '0' -or $ids -contains $ref.Groups[1].Value) 'Valid local reference'}
Check (([regex]::Matches($text,'(?m)^SpriteRenderer:')).Count -eq 15) 'Only 15 simple rectangular sprites'
Check (([regex]::Matches($text,'(?m)^BoxCollider2D:')).Count -eq 1) 'One solid collider'
Check ($text.Contains('m_Size: {x: 2.6, y: 1.9}') -and $text.Contains('m_IsTrigger: 0')) 'Collider matches board footprint'
Check (!$text.Contains('MonoBehaviour:')) 'No added interaction or runtime generator'
Check ((Get-Content -Raw "$path.meta").Contains('guid: 9ba5c64a7f174bb98fb059b7e4ab600c')) 'Stable prefab GUID'
$shelf=Get-Content -Raw "$root/Assets/Prefabs/Decorations/Shelf.prefab"
foreach($color in @('0.38, g: 0.2, b: 0.11','0.67, g: 0.39, b: 0.2','0.29, g: 0.16, b: 0.1','0.73, g: 0.45, b: 0.24','0.62, g: 0.35, b: 0.18','0.97, g: 0.77, b: 0.28')){
    Check ($text.Contains($color) -and $shelf.Contains($color)) 'Shelf frame and metal palette preserved'
}
function Rectangles($prefab) {
    $transforms=@{};$renderers=@()
    foreach($b in [regex]::Split($prefab,'(?m)(?=^--- !u!)')){
        $go=[regex]::Match($b,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
        if($b -match '^--- !u!4 ' -and !$b.Contains('m_Father: {fileID: 0}')){
            $p=[regex]::Match($b,'m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
            $s=[regex]::Match($b,'m_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
            $transforms[$go]=@([double]$p.Groups[1].Value,[double]$p.Groups[2].Value,[double]$s.Groups[1].Value,[double]$s.Groups[2].Value)
        }
        if($b -match '^--- !u!212 '){
            Check ($b.Contains('guid: 311925a002f4447b3a28927169b83ea6')) 'Built-in Square sprite'
            Check ($b.Contains('fileID: 10754, guid: 0000000000000000f000000000000000')) 'Shared default sprite material'
            $c=[regex]::Match($b,'m_Color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)')
            $renderers+=@{go=$go;order=[int][regex]::Match($b,'m_SortingOrder: (-?\d+)').Groups[1].Value;color=@([double]$c.Groups[1].Value,[double]$c.Groups[2].Value,[double]$c.Groups[3].Value)}
        }
    }
    foreach($r in ($renderers|Sort-Object order)){$r.rect=$transforms[$r.go];$r}
}
$boardRects=@(Rectangles $text);$shelfRects=@(Rectangles $shelf)
foreach($r in $boardRects){
    Check ([Math]::Abs($r.rect[0])+$r.rect[2]/2 -le 1.300001 -and [Math]::Abs($r.rect[1])+$r.rect[3]/2 -le .950001) 'All geometry within collision footprint'
}
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(860,440);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(25,32,43));$font=[Drawing.Font]::new('Segoe UI',14)
$panels=@(@{rects=$shelfRects;center=@(-3.61597,-.5921262);anchor=@(220,195);zoom=65;name='Shelf reference'},@{rects=$boardRects;center=@(0,0);anchor=@(620,195);zoom=135;name='NoticeBoard'})
foreach($panel in $panels){
    foreach($r in $panel.rects){
        $t=$r.rect;$c=$r.color
        $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int](255*$c[0]),[int](255*$c[1]),[int](255*$c[2])))
        $x=$panel.anchor[0]+($t[0]-$panel.center[0]-$t[2]/2)*$panel.zoom
        $y=$panel.anchor[1]-($t[1]-$panel.center[1]+$t[3]/2)*$panel.zoom
        $g.FillRectangle($brush,[single]$x,[single]$y,[single]($t[2]*$panel.zoom),[single]($t[3]*$panel.zoom));$brush.Dispose()
    }
    $g.DrawString($panel.name,$font,[Drawing.Brushes]::White,$panel.anchor[0]-70,355)
}
$small=[Drawing.Font]::new('Segoe UI',10)
$g.DrawString('Actual prefab rectangles | fitted per panel, not equal world scale',$small,[Drawing.Brushes]::LightGray,218,405)
$out="$root/Docs/NoticeBoard-preview.png";$bmp.Save($out,[Drawing.Imaging.ImageFormat]::Png)
$small.Dispose();$font.Dispose();$g.Dispose();$bmp.Dispose()
Write-Output "PASS: $checks prefab references, geometry, palette and collider checks. Preview: $out"
