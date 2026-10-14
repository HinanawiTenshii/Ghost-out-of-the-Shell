using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Run in the isolated validation project; never edits a scene or player save.
public static class PotionDescriptionSmoke
{
    static int checks;
    static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }

    public static void Run()
    {
        GameObject labelObject = null;
        try
        {
            var legacy = new System.Collections.Generic.Dictionary<AlchemyPotionKind, string>
            {
                { AlchemyPotionKind.Berserk, "以三份草药炼制的狂战药水。按R饮用，饮用后返还一个玻璃瓶。攻击力增加2点，攻击间隔缩短35%，普通近战可攻击前方多个目标；移动速度降低20%，持续22.5秒。同名临时效果不叠加，再次饮用刷新时长。" },
                { AlchemyPotionKind.Clamor, "以三份草药炼制的喧嚣药水。按R饮用，饮用后返还一个玻璃瓶。每3秒发出魔力噪声，吸引8单位内的普通角色前来调查，持续15秒。同名临时效果不叠加，再次饮用刷新时长。" },
                { AlchemyPotionKind.Death, "以三份草药炼制的剧毒药水。按R饮用，饮用后返还一个玻璃瓶。饮用后当前角色立即死亡。同名临时效果不叠加，再次饮用刷新时长。" },
                { AlchemyPotionKind.Echo, "以三份草药炼制的回响药水。按R饮用，饮用后返还一个玻璃瓶。记录位置和生命值，120秒内首次受到致命伤害时返回记录位置、恢复记录生命值并获得1秒伤害免疫。回溯时，当前场景中对玩家处于警告或敌对状态的角色会转为返回状态。主动切换附身不会触发，离开当前场景失效。同名临时效果不叠加，再次饮用刷新时长。" },
                { AlchemyPotionKind.Might, "以三份草药炼制的巨力药水。按R饮用，饮用后返还一个玻璃瓶。力量增加3点，普通近战可击退目标，持续30秒。同名临时效果不叠加，再次饮用刷新时长。" },
                { AlchemyPotionKind.Sight, "以三份草药炼制的灵视药水。按R饮用，饮用后返还一个玻璃瓶。视野范围增加25%，持续30秒。AI控制时拥有360度视野；玩家控制时可看见墙后角色和机关轮廓，但不能隔墙攻击、附身或交互。同名临时效果不叠加，再次饮用刷新时长。" },
                { AlchemyPotionKind.Spirit, "以三份草药炼制的灵能药水。按R饮用，饮用后返还一个玻璃瓶。恢复4点附身能量；满能量时，附身能量上限临时增加2点，持续30秒。同名临时效果不叠加，再次饮用刷新时长。" },
                { AlchemyPotionKind.Veil, "以三份草药炼制的幻隐药水。按R饮用，饮用后返还一个玻璃瓶。移动速度提高40%，不会被普通角色的视野发现，持续15秒。攻击或使用其他道具会解除；无法绕过碰撞和电门。同名临时效果不叠加，再次饮用刷新时长。" },
                { AlchemyPotionKind.Vitality, "以三份草药炼制的生命药水。按R饮用，饮用后返还一个玻璃瓶。恢复6点生命值；满生命值时，生命值上限临时增加3点，持续30秒。同名临时效果不叠加，再次饮用刷新时长。" },
                { AlchemyPotionKind.Weakness, "以三份草药炼制的虚弱药水。按R饮用，饮用后返还一个玻璃瓶。生命值上限降低3点（最低1点），最终攻击力固定为1，持续30秒。同名临时效果不叠加，再次饮用刷新时长。" },
            };
            labelObject = new GameObject("Description layout check", typeof(RectTransform), typeof(Text));
            var label = labelObject.GetComponent<Text>();
            label.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf");
            Check(label.font != null, "Project pixel font imports");
            label.fontSize = 21;
            label.lineSpacing = 1.15f;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.rectTransform.sizeDelta = new Vector2(510, 176);
            foreach (AlchemyPotionKind kind in Enum.GetValues(typeof(AlchemyPotionKind)))
            {
                var root = Resources.Load<GameObject>("PickupItems/Potion-" + kind);
                Check(root != null, kind + " prefab imports");
                var item = root.GetComponent<PotionPickupItem>();
                Check(item != null && item.Kind == kind, kind + " identity unchanged");
                string current = AlchemyRecipeCatalog.GetDescription(kind);
                Check(item.ItemDescription == current, kind + " prefab matches catalog");
                Check(current.EndsWith("按R饮用，饮用后返还一个玻璃瓶。"), kind + " bottle instruction");
                Check(!current.Contains("同名临时效果不叠加"), kind + " no misleading blanket refresh");
                Check(AlchemyRecipeCatalog.ResolveInventoryDescription(kind, legacy[kind], current) == current,
                    kind + " legacy saved default refreshed");
                Check(AlchemyRecipeCatalog.ResolveInventoryDescription(kind, null, current) == current,
                    kind + " missing saved description falls back");
                Check(AlchemyRecipeCatalog.ResolveInventoryDescription(kind, current, current) == current,
                    kind + " current saved description unchanged");
                string custom = "以三份草药炼制的" + AlchemyRecipeCatalog.GetName(kind) +
                    "。按R饮用，饮用后返还一个玻璃瓶。特别为城主调制。同名临时效果不叠加，再次饮用刷新时长。";
                Check(AlchemyRecipeCatalog.ResolveInventoryDescription(kind, custom, current) == custom,
                    kind + " authored description with similar wrapper preserved");
                Check(AlchemyRecipeCatalog.ResolveInventoryDescription(kind, legacy[kind], "") == legacy[kind],
                    kind + " missing default cannot erase old text");
                label.text = current;
                float height = label.preferredHeight;
                Debug.Log(kind + " description height=" + height + "/176");
                Check(height <= 176f, kind + " fits current description window");
            }
            string oldMight = "以三份草药炼制的巨力药水。按R饮用，饮用后返还一个玻璃瓶。" +
                "力量增加3点，免疫恐惧，普通近战可击退目标，持续30秒。同名临时效果不叠加，再次饮用刷新时长。";
            Check(AlchemyRecipeCatalog.ResolveInventoryDescription(AlchemyPotionKind.Might, oldMight, "new") == "new",
                "Alternate old Might catalog wording migrates");
            Check(AlchemyRecipeCatalog.GetDescription(AlchemyPotionKind.Death).Contains("立即死亡"), "Poison warning");
            Check(AlchemyRecipeCatalog.GetDescription(AlchemyPotionKind.Echo).Contains("120秒"), "Echo duration retained");
            Debug.Log("POTION_DESCRIPTION_PASS checks=" + checks);
            UnityEngine.Object.DestroyImmediate(labelObject);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            if (labelObject != null) UnityEngine.Object.DestroyImmediate(labelObject);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
