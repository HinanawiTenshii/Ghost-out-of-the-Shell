using System.Collections.Generic;
using UnityEngine;

/// <summary>Coordinates streaming only; members keep independent patrol routes and AI.</summary>
[DefaultExecutionOrder(-1100)]
[DisallowMultipleComponent]
[AddComponentMenu("Characters/AI/Patrol Streaming Group")]
public sealed class PatrolStreamingGroup : MonoBehaviour
{
    [Header("巡逻加载同步组（不是巡逻队）")]
    [Tooltip("绑定同一小区域内需要保持巡逻节奏的普通巡逻角色。允许不同路线；不支持 SquadPatrolCharacterAi。请将本组件挂在独立的空对象上。")]
    [SerializeField] private ZeldaPatrolCharacterAi[] members = new ZeldaPatrolCharacterAi[0];

    internal sealed class Member
    {
        internal ZeldaPatrolCharacterAi ai;
        internal ZeldaCharacterData data;
        internal ZeldaFourWayMover mover;
        internal Collider2D body;
        internal bool Participates => ai != null && ai.HasIdlePatrol && ai.enabled && data != null && !data.IsDead &&
            mover != null && !mover.enabled;
        internal bool IsOrdinaryPatrol => Participates && ai.CurrentState == ZeldaAiState.Idle &&
            !ai.RequiresVisionStreamingRetention;
    }

    internal static readonly List<PatrolStreamingGroup> ActiveGroups = new List<PatrolStreamingGroup>();
    internal static int RegistryVersion { get; private set; }
    internal readonly List<Member> RuntimeMembers = new List<Member>();
    internal CameraVisionObjectStreaming Owner { get; private set; }
    public bool PatrolPaused { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() { ActiveGroups.Clear(); RegistryVersion++; }

    void OnEnable()
    {
        if (!ActiveGroups.Contains(this)) ActiveGroups.Add(this);
        RefreshMembers();
    }

    void OnDisable()
    {
        ActiveGroups.Remove(this);
        Unbind();
        Owner = null;
        PatrolPaused = false;
        RegistryVersion++;
    }

    /// <summary>Rebind after an intentional runtime roster change, never every frame.</summary>
    public void RefreshMembers()
    {
        Unbind();
        if (!isActiveAndEnabled) return;
        if (members == null) members = new ZeldaPatrolCharacterAi[0];
        foreach (var ai in members)
        {
            if (ai == null || !ai.HasIdlePatrol || ai is SquadPatrolCharacterAi) continue;
            if (ZeldaRuntimeRegistry.GetGameplayScene(ai.gameObject) !=
                ZeldaRuntimeRegistry.GetGameplayScene(gameObject)) continue;
            if (ai.StreamingGroup != null)
            {
                if (ai.StreamingGroup != this)
                    Debug.LogWarning("巡逻角色已绑定另一加载同步组，本组忽略重复绑定：" + ai.name, this);
                continue;
            }
            ai.StreamingGroup = this;
            RuntimeMembers.Add(new Member { ai = ai, data = ai.GetComponent<ZeldaCharacterData>(),
                mover = ai.GetComponent<ZeldaFourWayMover>(), body = ai.GetComponent<Collider2D>() });
        }
        RegistryVersion++;
    }

    void Unbind()
    {
        foreach (var member in RuntimeMembers)
            if (member.ai != null && member.ai.StreamingGroup == this) member.ai.StreamingGroup = null;
        RuntimeMembers.Clear();
    }

    internal bool Claim(CameraVisionObjectStreaming manager)
    {
        if (Owner != null && Owner != manager && Owner.isActiveAndEnabled && Owner.StreamingEnabled) return false;
        Owner = manager;
        return true;
    }

    internal void SetPaused(CameraVisionObjectStreaming manager, bool paused)
    {
        if (Owner != manager || PatrolPaused == paused) return;
        PatrolPaused = paused;
        if (paused)
            foreach (var member in RuntimeMembers)
                if (member.IsOrdinaryPatrol) member.ai.FreezeStreamingPatrol();
    }

    internal void Release(CameraVisionObjectStreaming manager)
    {
        if (Owner != manager) return;
        Owner = null;
        PatrolPaused = false;
    }

    void OnDrawGizmosSelected()
    {
        if (members == null) return;
        Color previousColor = Gizmos.color;
        Gizmos.color = new Color(.15f, .9f, 1f, .65f);
        foreach (var member in members)
        {
            if (member == null) continue;
            Gizmos.DrawLine(transform.position, member.transform.position);
            Gizmos.DrawWireSphere(member.transform.position, .25f);
        }
        Gizmos.color = previousColor;
    }
}
