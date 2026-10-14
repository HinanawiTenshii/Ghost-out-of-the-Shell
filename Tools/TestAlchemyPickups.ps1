$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$checks = 0
function Check($condition, $message) { if (!$condition) { throw $message }; $script:checks++ }
$names = @('RedHerbPowderPickupItem','GreenHerbPowderPickupItem','BlueHerbPowderPickupItem','GlassBottlePickupItem')
$ids = @('red_herb_powder','green_herb_powder','blue_herb_powder','glass_bottle')
$prefabGuids = @()
for ($i=0; $i -lt $names.Count; $i++) {
    $path = Join-Path $projectRoot "Assets/Resources/PickupItems/$($names[$i]).prefab"
    $asset = Get-Content -Raw -LiteralPath $path
    Check ($asset.Contains("m_Name: $($names[$i])") -and $asset.Contains("itemId: $($ids[$i])")) 'Resource identity matches'
    Check ($asset.Contains('persistentIdentity: {fileID: 0}') -and $asset.Contains('uniqueInstanceId: ""')) 'New copies do not share the old gunpowder quest identity'
    Check ($asset.Contains('m_IsTrigger: 1') -and $asset.Contains('maxStackSize: 3')) 'Alchemy supplies stack up to three per slot'
    if ($i -lt 3) {
        Check ($asset.Contains('animateParticles: 1') -and $asset.Contains('particlesPerSecond: 8')) 'Three powders share bounded magic emission defaults'
    }
    $prefabGuids += [regex]::Match((Get-Content -Raw -LiteralPath "$path.meta"),'guid: ([a-f0-9]{32})').Groups[1].Value
    $expectedScripts = if($i -eq 3){@('GlassBottlePickupItem','GlassBottlePickupItemVisual')}else{@('HerbPowderPickupItem','HerbPowderPickupItemVisual')}
    foreach($script in $expectedScripts) {
        $meta = Get-Content -Raw -LiteralPath (Join-Path $projectRoot "Assets/Scripts/Items/$script.cs.meta")
        $guid = [regex]::Match($meta,'guid: ([a-f0-9]{32})').Groups[1].Value
        Check ($guid.Length -eq 32 -and $asset.Contains("guid: $guid")) 'Prefab binds actual script GUID'
    }
    $all = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets/Resources/PickupItems') -Filter '*.prefab' -Recurse |
        Select-String -Pattern "^  itemId: $($ids[$i])$"
    Check (@($all).Count -eq 1) 'No duplicate inventory item ID'
}
Check (($prefabGuids | Select-Object -Unique).Count -eq 4) 'Distinct prefab GUIDs'
foreach($visualName in @('HerbPowderPickupItemVisual','GlassBottlePickupItemVisual')) {
    $source = Get-Content -Raw -LiteralPath (Join-Path $projectRoot "Assets/Scripts/Items/$visualName.cs")
    $rows = [regex]::Matches($source,'"([.DMHGLTBiJ]{10,})"')
    Check ($rows.Count -eq $(if($visualName -eq 'HerbPowderPickupItemVisual'){16}else{20})) 'Expected authored icon grid'
    foreach($row in $rows) {
        $pixels = $row.Groups[1].Value
        Check ($pixels.Length -eq 16) 'Pixel row width is consistent'
        if($visualName -eq 'GlassBottlePickupItemVisual') {
            for($x=0;$x -lt 8;$x++) { Check ($pixels[$x] -eq $pixels[15-$x]) 'Bottle outline, neck, glints and decoration are mirrored' }
        }
    }
}
$powder = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Assets/Scripts/Items/HerbPowderPickupItem.cs')
Check ($powder.Contains('CanUseFromInventory => false') -and $powder.Contains('ApplyUseEffect(ZeldaCharacterData user) => false')) 'Powder cannot accidentally be consumed'
$powderVisual = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Assets/Scripts/Items/HerbPowderPickupItemVisual.cs')
Check ($powderVisual.Contains('main.maxParticles = 32;') -and $powderVisual.Contains('main.useUnscaledTime = false;')) 'Motes have a particle budget and respect game pause'
Check ($powderVisual.Contains('velocity.space = ParticleSystemSimulationSpace.World;') -and $powderVisual.Contains('new ParticleSystem.MinMaxCurve(-spreadSpeed, spreadSpeed)')) 'World-up ascent and bilateral spreading'
Check ($powderVisual.Contains('new GradientColorKey(powderColor, 1f)') -and $powderVisual.Contains('new GradientAlphaKey(0f, 1f)')) 'Matching hue fades completely'
Check ($powderVisual.Contains('StopEmittingAndClear') -and $powderVisual.Contains('Destroy(particleMaterial)')) 'Unload/destruction clear transient particles and material'
$bottle = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Assets/Scripts/Items/GlassBottlePickupItem.cs')
Check ($bottle.Contains('override string InventoryState') -and $bottle.Contains('override void ApplyInventoryState')) 'Bottle instance tuning survives inventory serialization'
$flight = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Assets/Scripts/Items/ThrownGlassBottle.cs')
Check ($flight.Contains('AttackPower = 1;') -and $flight.Contains('Lifetime = 6f;')) 'Requested attack power and flight lifetime'
Check ($flight.Contains('Physics2D.CircleCast') -and $flight.Contains('Physics2D.OverlapCircle')) 'Swept and initial-overlap collision detection'
Check ($flight.IndexOf('finished = true;') -lt $flight.IndexOf('GlassBottleShatterVisual.Spawn')) 'One-shot guard before side effects'
Check ($flight.Contains('retention.Configure(Mathf.Max(.6f, investigationRadius + .5f))')) 'Wake distant listeners before investigation'
Write-Output "PASS: $checks alchemy item asset, icon and integration checks. Runtime/GPU tests: Tools/AlchemyPickupsSmoke.cs."
