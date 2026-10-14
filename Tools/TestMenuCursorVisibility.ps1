# Run the actual production cursor-policy method with lightweight menu-state stubs.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw "$root/Assets/Scripts/Rendering/CRTScreenEffect.cs"
$method = [regex]::Match($source, '(?s)private static void UpdateCursorVisibility\(\)\s*\{.*?\r?\n    \}').Value
if (!$method) { throw 'Production cursor policy method not found' }
$fixture = @'
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace UnityEngine {
    public enum CursorLockMode { None, Locked, Confined }
    public static class Cursor { public static bool visible; public static CursorLockMode lockState; }
}
namespace UnityEngine.SceneManagement {
    public struct Scene { public string name; }
    public static class SceneManager {
        public static string sceneName = "Level1-Garden";
        public static Scene GetActiveScene() { return new Scene { name = sceneName }; }
    }
}
public static class PauseMenuController { public static bool IsPaused; }
public static class TabJournalMenuController { public static bool IsOpen; }
public static class SaveSlotPanel { public static bool IsOpen; }
public static class DoorPasswordPanel { public static bool IsOpen; }
public static class ElectricGatePanel { public static bool IsOpen; }
public static class CharacterDialogueWindow { public static bool BlocksInput; }
public static class CursorPolicyFixture {
    // PRODUCTION_METHOD
    public static int Run() {
        int checks = 0;
        // All combinations: title, pause, journal, save, password, electric gate.
        for (int state = 0; state < 128; state++) {
            SceneManager.sceneName = (state & 1) != 0 ? "Title Screen" : "Level2-Floor-1";
            PauseMenuController.IsPaused = (state & 2) != 0;
            TabJournalMenuController.IsOpen = (state & 4) != 0;
            SaveSlotPanel.IsOpen = (state & 8) != 0;
            DoorPasswordPanel.IsOpen = (state & 16) != 0;
            ElectricGatePanel.IsOpen = (state & 32) != 0;
            CharacterDialogueWindow.BlocksInput = (state & 64) != 0;
            for (int frame = 0; frame < 3; frame++) {
                // Simulate another system requesting an opposing state each frame.
                Cursor.visible = state == 0;
                Cursor.lockState = frame == 0 ? CursorLockMode.Locked : CursorLockMode.Confined;
                UpdateCursorVisibility();
                if (Cursor.visible != (state != 0)) throw new Exception("Visibility mismatch: " + state);
                if (Cursor.lockState != CursorLockMode.None) throw new Exception("Pointer remains locked: " + state);
                checks += 2;
            }
        }
        PauseMenuController.IsPaused = TabJournalMenuController.IsOpen = SaveSlotPanel.IsOpen = false;
        DoorPasswordPanel.IsOpen = ElectricGatePanel.IsOpen = false;
        CharacterDialogueWindow.BlocksInput = false;
        SceneManager.sceneName = "Level1-Garden";
        Cursor.visible = true;
        UpdateCursorVisibility();
        if (Cursor.visible) throw new Exception("Closing all menus must restore hidden gameplay pointer");
        checks++;
        SceneManager.sceneName = "tItLeScReEn";
        UpdateCursorVisibility();
        if (!Cursor.visible) throw new Exception("Title cursor regression");
        return checks + 1;
    }
}
'@
Add-Type -TypeDefinition $fixture.Replace('// PRODUCTION_METHOD', $method)
$checks = [CursorPolicyFixture]::Run()
Write-Output "PASS: $checks production cursor-policy assertions (all menu combinations, repeated frames, unlocked pointer and close recovery)."
