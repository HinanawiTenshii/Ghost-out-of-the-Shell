# Offline disk-rotation smoke test. Extract actual production methods; only
# Unity serialization/logging and unrelated scene types are lightweight stubs.
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$core = Get-Content -Raw -LiteralPath "$taskRoot/Assets/Scripts/SceneManagement/GameSaveSystem.cs"
$profiles = Get-Content -Raw -LiteralPath "$taskRoot/Assets/Scripts/SceneManagement/GameSaveSystem.Profiles.cs"
$history = Get-Content -Raw -LiteralPath "$taskRoot/Assets/Scripts/SceneManagement/GameSaveSystem.AutoHistory.cs"
$smoke = Get-Content -Raw -LiteralPath "$taskRoot/Tools/AutoSaveHistorySmoke.cs"
function Extract-Block([string]$source, [string]$signature) {
    $block = [regex]::Match($source, '(?ms)^    ' + [regex]::Escape($signature) + '.*?^    \}').Value
    if (!$block) { throw "Production block not found: $signature" }
    return $block
}
function Extract-Expression([string]$source, [string]$signature) {
    $expression = [regex]::Match($source, '(?ms)^    ' + [regex]::Escape($signature) + '[^;]+;').Value
    if (!$expression) { throw "Production expression not found: $signature" }
    return $expression
}
$blocks = @(
    (Extract-Block $core '[Serializable] public sealed class SaveData'),
    (Extract-Block $core 'private static SaveData ReadData('),
    (Extract-Block $core 'private static string Hash('),
    (Extract-Block $profiles 'private static string SlotName('),
    (Extract-Block $profiles 'private static void WriteData('),
    (Extract-Block $profiles 'private static T ReadChecked<T>('),
    (Extract-Block $profiles 'private static void DeleteSaveFile('),
    (Extract-Expression $profiles 'public static bool IsManualSlot('),
    (Extract-Expression $profiles 'public static bool IsAutomaticSlot('),
    (Extract-Expression $profiles 'private static bool IsProfileId('),
    (Extract-Expression $profiles 'private static bool ValidId('),
    (Extract-Expression $profiles 'private static bool BelongsTo(')
)
$fixture = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace UnityEngine {
    public static class Debug {
        public static void Log(object value) { Console.WriteLine(value); }
        public static void LogWarning(object value) { }
    }
    public static class JsonUtility {
        private static readonly System.Text.Json.JsonSerializerOptions Options =
            new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        public static string ToJson(object value) => System.Text.Json.JsonSerializer.Serialize(value, value.GetType(), Options);
        public static T FromJson<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json, Options);
    }
}
public static class SceneTravelStateManager { public class DiskPlayer { } }
public static class QuestJournalManager { public class SaveState { } }
public class MapPointOfInterestRecord { }
public static class SaveGameCatalog { public class SceneTemplate { public string templateKey; } }
public static class SaveSceneTemplateStore { public static bool Validate(string key) { return true; } }
public sealed partial class GameSaveSystem {
    public class Envelope { public string payload, checksum; }
    public class ProfileData { public string id, campaignId; }
    public class LevelOutcome { }
    public class LevelVisit { public List<SaveGameCatalog.SceneTemplate> initialScenes; }
    private ProfileData activeProfile = null;
    private static string ProfileFolder(string id) { throw new Exception("Live profile paths are forbidden in offline tests"); }
    // PRODUCTION_BLOCKS
}
'@
$fixture = $fixture.Replace('// PRODUCTION_BLOCKS', ($blocks -join "`n"))
$fixture += "`n" + [regex]::Replace($history, '(?m)^using [^;]+;\r?\n', '')
$fixture += "`n" + [regex]::Replace($smoke, '(?m)^using [^;]+;\r?\n', '')
Add-Type -TypeDefinition $fixture
Push-Location -LiteralPath $taskRoot
try { [AutoSaveHistorySmoke]::RunChecks() }
finally { Pop-Location }
