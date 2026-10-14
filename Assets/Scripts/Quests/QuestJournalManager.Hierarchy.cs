using System;
using System.Collections.Generic;

public sealed partial class QuestJournalManager
{
    [Serializable] public sealed class ParentLink
    {
        public string entryId;
        public string parentEntryId;
    }

    public struct HierarchyRow
    {
        public Entry entry;
        public int depth;
        public bool hasChildren;
    }

    private readonly Dictionary<string, string> parentEntries = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<string> collapsedEntries = new HashSet<string>(StringComparer.Ordinal);

    public string GetParentEntryId(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId)) return string.Empty;
        entryId = entryId.Trim();
        if (parentEntries.TryGetValue(entryId, out string parent)) return parent;
        if (entryId == ArenaInjectionQuestId) return ArenaQuestId;
        // Stable IDs also gate item/scene events before the NPC's scene is loaded.
        return entryId == SandwormBaitQuestId || entryId == SandwormMortarQuestId
            ? Level2SandwormQuestId : string.Empty;
    }

    /// <summary>Configure by stable ID, never scene references. Empty leaves an existing binding intact.</summary>
    public bool ConfigureParent(string entryId, string parentEntryId)
    {
        if (string.IsNullOrWhiteSpace(entryId)) return false;
        if (string.IsNullOrWhiteSpace(parentEntryId)) return true;
        entryId = entryId.Trim(); parentEntryId = parentEntryId.Trim();
        if (GetParentEntryId(entryId) == parentEntryId) return true;
        var seen = new HashSet<string>(StringComparer.Ordinal) { entryId };
        string ancestor = parentEntryId;
        while (!string.IsNullOrEmpty(ancestor))
        {
            if (!seen.Add(ancestor)) return false;
            ancestor = GetParentEntryId(ancestor);
        }
        parentEntries[entryId] = parentEntryId;
        return true;
    }

    public bool CanTriggerEntry(string entryId, string parentEntryId = null)
    {
        if (!ConfigureParent(entryId, parentEntryId)) return false;
        string ancestor = GetParentEntryId(entryId);
        // Configuration rejects cycles; the bound also protects malformed old saves.
        int remaining = parentEntries.Count + 3;
        while (!string.IsNullOrEmpty(ancestor) && remaining-- > 0)
        {
            if (!ContainsEntry(ancestor)) return false;
            ancestor = GetParentEntryId(ancestor);
        }
        return string.IsNullOrEmpty(ancestor);
    }

    public bool CanApplyEventChanges(QuestJournalEventChange[] changes)
    {
        if (changes == null) return true;
        foreach (var change in changes)
            if (!CanTriggerEntry(change.entryId, change.parentEntryId)) return false;
        return true;
    }

    public bool IsEntryCollapsed(string entryId) => collapsedEntries.Contains(entryId);

    public void SetEntryCollapsed(string entryId, bool collapsed)
    {
        if (!ContainsEntry(entryId)) return;
        bool changed = collapsed ? collapsedEntries.Add(entryId) : collapsedEntries.Remove(entryId);
        // UI-only change: do not display a quest-update popup or change tracking.
        if (changed) JournalChanged?.Invoke();
    }

    /// <summary>Event-driven depth-first display, independent of acceptance order.</summary>
    public void GetVisibleHierarchy(List<HierarchyRow> rows)
    {
        rows.Clear();
        var children = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            string parent = GetParentEntryId(entry.Id);
            if (!children.TryGetValue(parent, out var siblings))
                children[parent] = siblings = new List<Entry>();
            siblings.Add(entry);
        }
        AppendHierarchyRows(string.Empty, 0, children, rows, new HashSet<string>());
    }

    private void AppendHierarchyRows(string parent, int depth, Dictionary<string, List<Entry>> children,
        List<HierarchyRow> rows, HashSet<string> visited)
    {
        if (!children.TryGetValue(parent, out var siblings)) return;
        foreach (var entry in siblings)
        {
            if (!visited.Add(entry.Id)) continue;
            rows.Add(new HierarchyRow { entry = entry, depth = depth, hasChildren = children.ContainsKey(entry.Id) });
            if (!IsEntryCollapsed(entry.Id)) AppendHierarchyRows(entry.Id, depth + 1, children, rows, visited);
        }
    }

    private void RestoreHierarchy(SaveState state)
    {
        parentEntries.Clear(); collapsedEntries.Clear();
        if (state.parents != null)
            foreach (var link in state.parents)
                if (link != null) ConfigureParent(link.entryId, link.parentEntryId);
        if (state.collapsed != null) collapsedEntries.UnionWith(state.collapsed);
    }
}
