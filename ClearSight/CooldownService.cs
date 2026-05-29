using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace ClearSight;

// Dalamud doesn't hand us cooldown timers through a tidy managed API, so we
// reach into FFXIVClientStructs (which ships with Dalamud) and read the game's
// own ActionManager — and its hotbars — directly. All the unsafe pointer work
// lives here and nowhere else; when a game patch shifts these structs around,
// this is the one file that needs fixing. Everything above it only ever sees
// CooldownInfo.

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
/// Watches the player's hotbars and reads the live cooldowns for whatever's
/// slotted there. The tracked set rebuilds itself whenever the job changes, so
/// the overlay always reflects the kit you're actually playing.
/// </summary>
public sealed unsafe class CooldownService : IDisposable
{
    // The ActionCategory rows we care about telling apart. oGCDs are abilities;
    // weaponskills and spells are the GCD actions a player can opt into showing.
    private const uint Spell = 2;
    private const uint Weaponskill = 3;
    private const uint Ability = 4;

    private readonly IPlayerState playerState;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;

    // Kept as an ordered list so the bars line up in a stable hotbar order
    // rather than jumping around frame to frame.
    private readonly List<uint> trackedActions = new();

    // What the current tracked set was built for, so we know when it's stale.
    private uint builtForJob = uint.MaxValue;
    private bool builtIncludingGcd;

    // Job swaps rebuild instantly, but re-slotting a skill doesn't announce
    // itself, so we also refresh on a slow cadence to quietly pick those up.
    private const int FramesBetweenRefreshes = 120;
    private int framesSinceRefresh;

    public CooldownService(IPlayerState playerState, IDataManager dataManager, IPluginLog log)
    {
        this.playerState = playerState;
        this.dataManager = dataManager;
        this.log = log;
    }

    /// <summary>
    /// Makes sure the tracked set matches the current job and GCD preference,
    /// rescanning the hotbars only when one of those has actually changed.
    /// Cheap to call every frame.
    /// </summary>
    public void EnsureTracked(bool includeGcd)
    {
        var job = playerState.IsLoaded ? playerState.ClassJob.RowId : 0u;

        var stale = job != builtForJob || includeGcd != builtIncludingGcd;
        if (!stale && ++framesSinceRefresh < FramesBetweenRefreshes)
            return;

        RebuildFromHotbars(includeGcd);
        builtForJob = job;
        builtIncludingGcd = includeGcd;
        framesSinceRefresh = 0;
    }

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
        var result = new List<CooldownInfo>(trackedActions.Count);
        foreach (var id in trackedActions)
        {
            var info = GetCooldown(id);
            if (info.HasValue)
                result.Add(info.Value);
        }
        return result;
    }

    // Walks every hotbar slot the player has, keeps the ones holding an action
    // worth a bar, and rebuilds the tracked list in the order they're slotted.
    private void RebuildFromHotbars(bool includeGcd)
    {
        trackedActions.Clear();

        var hotbars = RaptureHotbarModule.Instance();
        if (hotbars == null)
            return;

        var actions = dataManager.GetExcelSheet<LuminaAction>();
        var seen = new HashSet<uint>();

        // Standard hotbars are 0-9 and cross hotbars 10-17; each holds up to 16
        // slots. Scanning all of them means we cover keyboard and controller
        // layouts alike, and the dedupe keeps shared actions from doubling up.
        for (uint bar = 0; bar < 18; bar++)
        {
            for (uint slot = 0; slot < 16; slot++)
            {
                var entry = hotbars->GetSlotById(bar, slot);
                if (entry == null || entry->CommandType != RaptureHotbarModule.HotbarSlotType.Action)
                    continue;

                var id = entry->CommandId;
                if (id == 0 || !seen.Add(id))
                    continue;

                if (WorthShowing(actions, id, includeGcd))
                    trackedActions.Add(id);
            }
        }
    }

    private static bool WorthShowing(Lumina.Excel.ExcelSheet<LuminaAction> actions, uint actionId, bool includeGcd)
    {
        if (!actions.TryGetRow(actionId, out var action) || !action.IsPlayerAction)
            return false;

        var category = action.ActionCategory.RowId;

        // oGCDs always earn a bar; the shared-GCD weaponskills and spells only
        // show up when the player has opted into the clutter.
        return category == Ability
            || (includeGcd && (category == Weaponskill || category == Spell));
    }

    public void Dispose()
    {
        // We don't own any of the game's memory, so there's nothing to free yet.
        // This is just a tidy home for cleanup once we add hooks or events.
        trackedActions.Clear();
    }
}
