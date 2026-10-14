// Only copy into the isolated smoke-test project, never into production Assets.
// Projectile, hitbox, explosion visual, geometry and retention-region code are production files.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
public class ZeldaCharacterData : MonoBehaviour
{
    public bool IsDead, IsGhostLike;
    public bool IsGhostForm=>IsGhostLike;
    public bool CanTakeAttackDamage=true;
    public int Damage;public ZeldaCharacterData LastOwner;
    public void TakeDamage(int power,ZeldaCharacterData owner){Damage+=power;LastOwner=owner;}
}
public class SkeletonZeldaCharacterData : ZeldaCharacterData {public static Sprite GetFistSprite()=>null;}
public class ZeldaCharacterAiBase : MonoBehaviour {public bool IsStunned;}
public class AutomatonCharacterAi : ZeldaCharacterAiBase {public bool HostilityActivated=true;}
public class ZeldaFourWayMover : MonoBehaviour
{
    public Vector2 FacingDirection=Vector2.up; public bool IsAttacking;
    public ZeldaCharacterData CharacterData=>GetComponent<ZeldaCharacterData>();
    public bool TryBeginItemAttack(float duration)=>!IsAttacking;
    public Vector3 GetOverheadWorldPosition(Vector2 offset)=>transform.position+(Vector3)offset;
}
public static class ZeldaRuntimeRegistry
{
    public static ZeldaFourWayMover Controlled;
    public static ZeldaFourWayMover GetControlledMover()=>Controlled;
    public static Scene GetGameplayScene(GameObject o)=>o.scene;
}
public static class CameraVisionObjectStreaming
{
    public static readonly List<GameObject> Suspended=new List<GameObject>();
    public static int Refreshes;
    public static void RefreshRetentionRegionsNow()
    {
        Refreshes++;
        foreach(var o in Suspended)
            if(o!=null && CameraVisionStreamingRegion.Intersects(o.scene,new Bounds(o.transform.position,Vector3.one*.2f))) o.SetActive(true);
    }
}
public static class DocumentReader {public static bool IsInputBlocked;}
public static class SignpostInteraction {public static bool BlocksInput;}
public static class CharacterDialogueWindow {public static bool BlocksInput;}
public class ClockworkPuppetRuntime : MonoBehaviour
{
    public static bool BlocksCharacterInput;
    public static readonly List<ClockworkPuppetRuntime> ActivePuppets = new List<ClockworkPuppetRuntime>();
    public bool IsBroken, IsUnderRemoteControl;
    public FortressBallista Mounted;
    public bool IsAttachedToBallista(FortressBallista ballista) => Mounted == ballista;
}
public static class ZeldaUiPalette {public static Color Primary=>Color.cyan;}
public static class ZeldaPossessionProgressBar {public static void ConfigureOverlayRenderer(Renderer r){}}
public static class ZeldaInteractionArbiter
{
    public static void OfferInteraction(Component o,ZeldaFourWayMover m,KeyCode k,Vector2 p,Action<bool> a){a(true);}
    public static void Submit(Component o,ZeldaFourWayMover m,KeyCode k,Vector2 p,Action a){a();}
}
public class DoorData : MonoBehaviour {public bool IsDestroyed;public int Damage;public void ReceiveAttack(int p,ZeldaCharacterData o,bool direct){Damage+=p;}}
public class CardboardBoxPickupItem : MonoBehaviour {public bool IsDestroyed;public void ReceiveAttack(int p,ZeldaCharacterData o){}}
public class BatteryPickupItem : MonoBehaviour {public void ReceiveAttack(int p,ZeldaCharacterData o){}}
public class BatterySocket : MonoBehaviour {public void ReceiveAttack(int p,ZeldaCharacterData o){}}
public class BombPickupItem : MonoBehaviour {public void DetonateFromAttack(ZeldaCharacterData o){}}
public class PlacedBomb : MonoBehaviour {public void DetonateImmediately(){}}
public class StabilityCrystalPickupItem : MonoBehaviour {public void BurstFromAttack(){}}
public class DollAttackShake : MonoBehaviour {public void TriggerAttackShake(){}}
