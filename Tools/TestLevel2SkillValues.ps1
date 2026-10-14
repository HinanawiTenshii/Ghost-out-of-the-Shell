$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$rules = [ordered]@{
    '^(AdvancedEngineer|Engineer)' = 6
    '^(Blacksmith)' = 5
    '^(ArabStrongman)' = 2
    '^(HeavyPaladin)' = 3
    '^(Strongman|Bodyguard)' = 1
    '^(Civilian|Merchant|Chef|Scribe|Dancer)' = 2
    '^(AdvancedArabSoldier|EliteArabSoldier|ArabSoldier|DesertScout)' = 3
    '^(Alchemist|AdvancedPaladin|Paladin|Wizard|Priest|EasternSwordsman)' = 4
    '^(Automaton)' = 1
    '^(Skeleton|Sandworm)' = 0
}
$expectedByGuid = @{}
$count = 0
Get-ChildItem "$root/Assets/Prefabs/Level2NPCS" -Recurse -Filter '*.prefab' | ForEach-Object {
    $expected = $null
    foreach ($rule in $rules.GetEnumerator()) { if ($_.BaseName -match $rule.Key) { $expected = $rule.Value; break } }
    if ($null -eq $expected) { throw "Unclassified Level2 character: $($_.Name)" }
    $text = [IO.File]::ReadAllText($_.FullName)
    $values = [regex]::Matches($text, '(?m)^  skillValue: (\d+)\r?$')
    if ($values.Count -ne 1 -or [int]$values[0].Groups[1].Value -ne $expected) { throw "Incorrect skill: $($_.Name), expected $expected" }
    $guid = [regex]::Match([IO.File]::ReadAllText($_.FullName + '.meta'), 'guid: ([a-f0-9]+)').Groups[1].Value
    $expectedByGuid[$guid] = $expected
    $count++
}
$overrideCount = 0
Get-ChildItem "$root/Assets/Scenes/Level2" -Filter '*.unity' | ForEach-Object {
    $text = [IO.File]::ReadAllText($_.FullName)
    foreach ($match in [regex]::Matches($text, 'guid: ([a-f0-9]+), type: 3\}\r?\n      propertyPath: skillValue\r?\n      value: (\d+)')) {
        $guid = $match.Groups[1].Value
        if (!$expectedByGuid.ContainsKey($guid)) { continue }
        if ([int]$match.Groups[2].Value -ne $expectedByGuid[$guid]) { throw "Stale scene skill override: $($_.Name), $guid" }
        $overrideCount++
    }
}
if ($count -lt 45) { throw "Missing Level2 prefabs: $count" }
"PASS: $count Level2 prefabs; Idle, Patrol and Troops agree; no conflicting Level2 scene overrides ($overrideCount matching overrides)."
