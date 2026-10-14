using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class MechanismRequirementsSmoke
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static IEnumerator test;static int frame=-1,checks;
    static MechanismRequirementsSmoke(){EditorApplication.update+=Tick;}
    public static void Run(){UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);SessionState.SetBool("MechanismRequirementsSmoke",true);EditorApplication.EnterPlaymode();}
    static void Tick()
    {
        if(!EditorApplication.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;
        try{if(SessionState.GetBool("MechanismRequirementsSmoke",false)){SessionState.SetBool("MechanismRequirementsSmoke",false);test=Verify();}
            if(test==null||test.MoveNext())return;test=null;Debug.Log("MECHANISM_REQUIREMENTS_PASS checks="+checks);EditorApplication.Exit(0);
        }catch(Exception e){test=null;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    static FieldInfo Field(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,F);if(f!=null)return f;}throw new Exception(n);}
    static void Set(object o,string n,object v)=>Field(o,n).SetValue(o,v);
    static T Get<T>(object o,string n)=>(T)Field(o,n).GetValue(o);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
    static void ClearMessages(ZeldaHealthHeartsUI hud){foreach(var t in hud.GetComponentsInChildren<Text>(true))if(t.text.StartsWith("需要")||t.text.Contains("能力值不足"))t.text="";}
    static bool HasMessage(ZeldaHealthHeartsUI hud,string message){foreach(var t in hud.GetComponentsInChildren<Text>(true))if(t.gameObject.activeInHierarchy&&t.text.Contains(message))return true;return false;}
    static IEnumerator Verify()
    {
        while(DocumentReader.IsInputBlocked)yield return null;
        var hud=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ZeldaHealthHeartsUI.prefab")).GetComponent<ZeldaHealthHeartsUI>();
        var actorRoot=new GameObject("Operator");var actor=actorRoot.AddComponent<ZeldaCharacterData>();var mover=actorRoot.AddComponent<ZeldaFourWayMover>();ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var camera=new GameObject("Main camera",typeof(Camera));camera.tag="MainCamera";camera.transform.position=new Vector3(0,0,-10);camera.GetComponent<Camera>().orthographic=true;
        var gate=new GameObject("Water gate").AddComponent<WaterwayGate>();gate.transform.position=new Vector3(30,0);
        var water=new GameObject("Controller").AddComponent<WaterwayGateController>();water.BindGate(gate);
        Set(water,"displayFont",hud.PermissionLabelFont);
        var pillar=new GameObject("Panel").AddComponent<ElectricGatePillar>();
        var cannon=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/Fortress/Fortress-Cannon.prefab")).GetComponent<FortressCannon>();
        cannon.transform.position=new Vector3(20,20);
        var cauldron=new GameObject("Cauldron").AddComponent<AlchemyCauldron>();
        Component[] mechanisms={water,pillar,cannon,cauldron};int[] requirements={3,5,5,4};
        for(int i=0;i<mechanisms.Length;i++)
        {
            var source=mechanisms[i];var window=source.GetComponentInChildren<ZeldaRequirementWindow>(true);
            Check(window,"window attached to existing runtime object "+source.GetType().Name);
            Check((int)Call(window,"GetRequirementValue")==requirements[i],"correct requirement "+requirements[i]);
            var icons=Get<Image[]>(window,"icons");int enabled=0;foreach(var icon in icons)if(icon.enabled)enabled++;
            Check(enabled==requirements[i],"wrench icon count");
            Check(Get<Text>(window,"requirementLabel").text=="需求：" && Get<RectTransform>(window,"panelRect").sizeDelta==new Vector2(270,84),"same lever layout");
            var duplicate=ZeldaRequirementWindow.AttachSkillRequirement(source,requirements[i]);Check(duplicate==window,"no duplicate window");
            Call(window,"SetVisible",true,true);Check(Get<CanvasGroup>(window,"canvasGroup").alpha==1,"window can display");
            source.gameObject.SetActive(false);Check(!Get<GameObject>(window,"overlayObject").activeSelf,"unload hides separate overlay");source.gameObject.SetActive(true);
        }
        var pillarWindow=pillar.GetComponentInChildren<ZeldaRequirementWindow>();Set(pillar,"hasPanel",false);
        Call(pillarWindow,"SetVisible",true,true);Call(pillarWindow,"LateUpdate");Check(!Get<GameObject>(pillarWindow,"overlayObject").activeSelf,"pillar without panel hides requirements");Set(pillar,"hasPanel",true);
        Set(actor,"skillValue",0);
        Call(water,"SetPromptVisible",true);Check(Get<TextMesh>(water,"promptText").text=="能力值不足","water overhead insufficient");
        Call(pillar,"ShowPrompt",true);Check(Get<TextMesh>(pillar,"prompt").text=="能力值不足","panel overhead insufficient");
        Call(cauldron,"ShowPrompt",true);Check(Get<TextMesh>(cauldron,"prompt").text=="能力值不足","cauldron overhead insufficient");
        water.transform.position=new Vector3(30,30);pillar.transform.position=new Vector3(35,35);cauldron.transform.position=new Vector3(40,40);
        actorRoot.transform.position=new Vector3(20,18);mover.RestoreSavedMovement(Vector2.up,mover.CurrentStamina);Physics2D.SyncTransforms();
        Call(cannon,"ShowPrompt",true);Check(Get<TextMesh>(cannon,"promptText").text=="能力值不足","cannon overhead insufficient");
        Set(actor,"skillValue",5);Call(cannon,"ShowPrompt",true);Check(Get<TextMesh>(cannon,"promptText").text=="按[E]发射加农炮","cannon prompt no longer contains numeric requirement");
        var inventoryRoot=new GameObject("Inventory");inventoryRoot.SetActive(false);var inv=inventoryRoot.AddComponent<PersistentInventory>();Set(inv,"saveToPlayerPrefs",false);inventoryRoot.SetActive(true);inv.enabled=false;inv.ClearAll();
        var door=new GameObject("Locked door").AddComponent<DoorHingeInteraction>();Set(door,"requiredUnlockItemInstanceId","correct-key");Set(door,"requiredUnlockItemName","测试钥匙");door.SetLocked(true);
        Set(actor,"skillValue",1);ClearMessages(hud);Call(door,"HandlePlayerInteraction");
        Check(HasMessage(hud,"需要测试钥匙")&&!HasMessage(hud,"能力值不足"),"missing key takes priority even below skill requirement");
        var key=Resources.Load<GameObject>("PickupItems/KeyPickupItem").GetComponent<PickupItemBase>();inv.TryAddUniqueItem(key.ItemId,"wrong-key",1,1);
        ClearMessages(hud);Call(door,"HandlePlayerInteraction");Check(HasMessage(hud,"需要测试钥匙")&&!HasMessage(hud,"能力值不足"),"wrong key still shows matching-key requirement only");
        inv.TryAddUniqueItem(key.ItemId,"correct-key",1,1);ClearMessages(hud);Call(door,"HandlePlayerInteraction");
        Check(HasMessage(hud,"能力值不足")&&!HasMessage(hud,"需要测试钥匙")&&door.IsLocked&&inv.ContainsItemInstance("correct-key"),"matching key low skill: skill popup without key loss");
        Set(actor,"skillValue",2);Call(door,"HandlePlayerInteraction");Check(!door.IsLocked&&!inv.ContainsItemInstance("correct-key"),"matching key at required skill unlocks");
        door.SetLocked(true);Set(door,"passwordLockEnabled",true);Set(actor,"skillValue",1);ClearMessages(hud);Call(door,"HandlePlayerInteraction");
        Check(HasMessage(hud,"能力值不足")&&!DoorPasswordPanel.IsOpen,"password lock keeps skill restriction without requiring physical key");
        yield return null;
    }
}
