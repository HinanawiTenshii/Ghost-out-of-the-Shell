using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Compile-time edition policy. Standard builds keep the original rules.</summary>
public static class ExhibitionEdition
{
    public const string ProductName = "Ghost out of the Shell - Exhibition";
    public const string Caption = "核聚变特供版";
    public const string EntryScene = "Level2-Desert";
    public static bool IsEnabled
    {
        get
        {
#if GOS_EXHIBITION
            return true;
#else
            return false;
#endif
        }
    }

    public static float StaminaMultiplier => IsEnabled ? 2f : 1f;
    public static float BodyPossessionReduction => IsEnabled ? 2f : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LogEdition()
    {
        if (IsEnabled)
            Debug.Log("[Exhibition] " + Caption + "; tutorial destination=" + EntryScene +
                "; independent progress=" + Application.persistentDataPath + "/Saves");
    }

    public static string ResolveDestination(string source, string destination)
    {
        return IsEnabled && string.Equals(source, "TutorialLevel", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(destination?.Trim(), "Level0", StringComparison.OrdinalIgnoreCase)
            ? EntryScene : destination;
    }

    public static void PrepareLevelEntry(string scene)
    {
        if (!IsEnabled || GameSaveSystem.IsLoading || string.IsNullOrEmpty(scene) ||
            !scene.StartsWith("Level2-", StringComparison.OrdinalIgnoreCase)) return;
        PlayerGrowthAttributes.GetOrCreate().GrantExhibitionLoadout();
    }

    public static void AddMenuCaption(Transform parent, Font font)
    {
        if (!IsEnabled || parent == null || parent.Find("Exhibition Edition") != null) return;
        var go = new GameObject("Exhibition Edition", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(24f, 16f);
        rect.sizeDelta = new Vector2(250f, 24f);
        var text = go.GetComponent<Text>();
        text.font = font;
        text.text = Caption;
        text.fontSize = 14;
        text.color = new Color(.34f, .75f, .9f, .85f);
        text.alignment = TextAnchor.MiddleLeft;
        text.raycastTarget = false;
    }
}
