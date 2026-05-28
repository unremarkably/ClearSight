using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ClearSight;

// ============================================================================
//  CooldownService
// ----------------------------------------------------------------------------
//  This is the FFXIV-specific core of the plugin. Dalamud does NOT expose
//  recast (cooldown) timers through a clean managed API, so we drop into
//  "stage 2" of Dalamud's interaction model: FFXIVClientStructs, which ships
//  with Dalamud and lets us treat the game's own ActionManager as a library.
//
//  Everything unsafe/pointer-y is quarantined in THIS file. When a game patch
//  or API bump breaks cooldown reading, this is the (only) file you patch.
//  The config and render layers never touch ActionManager directly.
// ============================================================================

/// <summary>
/// A clean, managed snapshot of one action's cooldown state for a single frame.
/// The render layer consumes only this — never ActionManager pointers.
/// </summary>
public readonly struct CooldownInfo
{
    public uint ActionId { get; init; }

    /// <summary>Seconds remaining on the *current* charge's recast. 0 when ready.</summary>
    public float Remaining { get; init; }

    /// <summary>Full recast length in seconds (for computing fill fraction).</summary>
    public float Total { get; init; }

    /// <summary>Charges currently available (1 for non-charge actions).</summary>
    public ushort CurrentCharges { get; init; }

    /// <summary>Max charges this action can bank (1 for non-charge actions).</summary>
    public ushort MaxCharges { get; init; }

    public bool IsReady => CurrentCharges > 0 && Remaining <= 0f;

    /// <summary>0.0 = just used, 1.0 = fully recharged. Safe to feed a progress bar.</summary>
    public float Progress => Total <= 0f ? 1f : Math.Clamp(1f - (Remaining / Total), 0f, 1f);
}

/// <summary>
/// Reads action cooldowns from the game each frame and exposes them as managed
/// structs. Tracks an explicit user-chosen set of action IDs AND can be asked
/// for the current job's actions on demand (the "Both — flexible" model).
/// </summary>
public sealed unsafe class CooldownService : IDisposable
{
    private readonly IClientState clientState;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;

    // The explicit watch-list the user has configured (specific action IDs).
    private readonly HashSet<uint> trackedActionIds = new();

    public CooldownService(IClientState clientState, IDataManager dataManager, IPluginLog log)
    {
        this.clientState = clientState;
        this.dataManager = dataManager;
        this.log = log;
    }

    // ---- Watch-list management (used by the config layer) ------------------

    public void Track(uint actionId)   => trackedActionIds.Add(actionId);
    public void Untrack(uint actionId) => trackedActionIds.Remove(actionId);
    public IReadOnlyCollection<uint> Tracked => trackedActionIds;

    // ---- The core read ------------------------------------------------------

    /// <summary>
    /// Reads the live cooldown state for a single action.
    /// Returns null only if the game/ActionManager isn't available
    /// (e.g. at the title screen).
    /// </summary>
    public CooldownInfo? GetCooldown(uint actionId)
    {
        var am = ActionManager.Instance();
        if (am == null)
            return null;

        // Actions share "recast groups" — many oGCDs map to a group, and the
        // GCD shares one group across the whole rotation. We ask the game which
        // group this action belongs to, then read that group's timer.
        //
        // GetRecastGroup wants the action TYPE and the action ID. For player
        // abilities/spells/weaponskills this is ActionType.Action.
        var recastGroup = am->GetRecastGroup((int)ActionType.Action, actionId);

        // GetRecastGroupDetail returns a pointer to the live timer struct.
        // IsActive != 0 means the cooldown is currently ticking.
        // Total  = full recast length, Elapsed = how far through we are.
        var detail = am->GetRecastGroupDetail(recastGroup);

        float total = 0f, elapsed = 0f;
        if (detail != null)
        {
            total = detail->Total;
            elapsed = detail->IsActive ? detail->Elapsed : detail->Total;
        }

        float remaining = Math.Max(0f, total - elapsed);

        // Charge-based actions (most modern oGCDs) — the game exposes current
        // and max stacks separately. For non-charge actions both come back as 1.
        ushort maxCharges = ActionManager.GetMaxCharges(actionId, 0);
        ushort currentCharges = (ushort)am->GetCurrentCharges(actionId);

        return new CooldownInfo
        {
            ActionId = actionId,
            Remaining = remaining,
            Total = total,
            CurrentCharges = currentCharges,
            MaxCharges = maxCharges == 0 ? (ushort)1 : maxCharges,
        };
    }

    /// <summary>
    /// Snapshot of every action on the explicit watch-list. Call once per frame
    /// from your draw loop, iterate the result to draw bars.
    /// </summary>
    public List<CooldownInfo> SnapshotTracked()
    {
        var result = new List<CooldownInfo>(trackedActionIds.Count);
        foreach (var id in trackedActionIds)
        {
            var info = GetCooldown(id);
            if (info.HasValue)
                result.Add(info.Value);
        }
        return result;
    }

    public void Dispose()
    {
        // Nothing unmanaged is *owned* here (ActionManager is the game's, not
        // ours), so there's nothing to free. The method exists so the service
        // fits the same disposable lifecycle as the rest of the plugin and so
        // future additions (hooks, etc.) have a home to clean up in.
        trackedActionIds.Clear();
    }
}
