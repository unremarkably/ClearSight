using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ClearSight;

// Dalamud doesn't hand us cooldown timers through a tidy managed API, so we
// reach into FFXIVClientStructs (which ships with Dalamud) and read the game's
// own ActionManager directly. All the unsafe pointer work lives here and
// nowhere else — when a game patch shifts these structs around, this is the one
// file that needs fixing. Everything above it only ever sees CooldownInfo.

/// <summary>
/// A snapshot of one action's cooldown for a single frame — the only thing the
/// rest of the plugin sees, so nothing else has to touch raw game memory.
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
/// Reads action cooldowns from the game each frame. It keeps a set of action
/// IDs we currently care about — for now that's filled in by hand, and the plan
/// is to populate it automatically from the player's job.
/// </summary>
public sealed unsafe class CooldownService : IDisposable
{
    private readonly IClientState clientState;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;

    private readonly HashSet<uint> trackedActionIds = new();

    public CooldownService(IClientState clientState, IDataManager dataManager, IPluginLog log)
    {
        this.clientState = clientState;
        this.dataManager = dataManager;
        this.log = log;
    }

    public void Track(uint actionId)   => trackedActionIds.Add(actionId);
    public void Untrack(uint actionId) => trackedActionIds.Remove(actionId);
    public IReadOnlyCollection<uint> Tracked => trackedActionIds;

    /// <summary>
    /// The live cooldown for one action, or null when the game isn't ready to
    /// answer (the title screen, mid-load, that sort of thing).
    /// </summary>
    public CooldownInfo? GetCooldown(uint actionId)
    {
        var am = ActionManager.Instance();
        if (am == null)
            return null;

        // Cooldowns aren't tracked per-action but per "recast group": every oGCD
        // belongs to one, and the whole GCD rotation shares a single group. So we
        // ask the game which group this action falls into, then read that group's
        // timer. Player skills are always ActionType.Action.
        var recastGroup = am->GetRecastGroup((int)ActionType.Action, actionId);

        // The detail struct is where the timer actually lives: Total is the full
        // recast, Elapsed is how far we've progressed, and IsActive tells us
        // whether it's counting at all.
        var detail = am->GetRecastGroupDetail(recastGroup);

        float total = 0f, elapsed = 0f;
        if (detail != null)
        {
            total = detail->Total;
            elapsed = detail->IsActive ? detail->Elapsed : detail->Total;
        }

        float remaining = Math.Max(0f, total - elapsed);

        // Most modern oGCDs bank charges; the game reports current and max stacks
        // separately. Actions without charges just report 1 of 1.
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
    /// Every tracked action's cooldown for this frame. Call it once per draw and
    /// loop the result to paint the bars.
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
        // We don't own any of the game's memory, so there's nothing to free yet.
        // This is just a tidy home for cleanup once we add hooks or events.
        trackedActionIds.Clear();
    }
}
