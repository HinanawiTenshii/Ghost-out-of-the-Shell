# Executes real resume-selection methods with minimal data-only stubs. No disk
# saves, scene loading, Unity startup or persistentDataPath access occurs.
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$resumeSource = Get-Content -Raw -LiteralPath "$taskRoot/Assets/Scripts/SceneManagement/GameSaveSystem.Resume.cs"
$profileSource = Get-Content -Raw -LiteralPath "$taskRoot/Assets/Scripts/SceneManagement/GameSaveSystem.Profiles.cs"
$smokeSource = Get-Content -Raw -LiteralPath "$taskRoot/Tools/AutoSaveResumeSmoke.cs"
function Read-ProductionBlock([string]$source, [string]$signature) {
    $result = [regex]::Match($source, '(?ms)^    ' + [regex]::Escape($signature) + '.*?^    \}').Value
    if (!$result) { throw "Missing production method: $signature" }
    return $result
}
function Read-ProductionExpression([string]$source, [string]$signature) {
    $result = [regex]::Match($source, '(?ms)^    ' + [regex]::Escape($signature) + '[^;]+;').Value
    if (!$result) { throw "Missing production expression: $signature" }
    return $result
}
$production = @(
    (Read-ProductionBlock $profileSource '[Serializable] public sealed class ProfileData'),
    (Read-ProductionExpression $profileSource 'private static readonly int[] KnownSaveSlots'),
    (Read-ProductionExpression $profileSource 'private static bool ValidId('),
    (Read-ProductionExpression $profileSource 'private static bool BelongsTo('),
    (Read-ProductionBlock $resumeSource 'private static bool SetResumeReference('),
    (Read-ProductionBlock $resumeSource 'private static void ClearResumeReference('),
    (Read-ProductionBlock $resumeSource 'private static SaveData SelectContinueTarget('),
    (Read-ProductionExpression $resumeSource 'private static bool MatchesResumeSnapshot(')
)
$fixture = @'
using System;
using System.Collections.Generic;
using System.Reflection;
public sealed class GameSaveSystem {
    public class LevelVisit { public string checkpointId; }
    public class SaveData {
        public int version = 4;
        public string profileId, campaignId, world;
        public long savedUtcTicks;
        public List<LevelVisit> visitedLevels;
    }
    // PRODUCTION
}
'@
$fixture = $fixture.Replace('// PRODUCTION', ($production -join "`n"))
$fixture += "`n" + [regex]::Replace($smokeSource, '(?m)^using [^;]+;\r?\n', '')
Add-Type -TypeDefinition $fixture
Write-Output ("AUTOSAVE_RESUME_PASS checks=" + [AutoSaveResumeSmoke]::RunChecks())
