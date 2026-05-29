using System;
using System.Numerics;
using Dalamud.Configuration;

namespace ClearSight;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    // Master switch for the on-screen bars.
    public bool ShowOverlay { get; set; } = true;

    // When locked, the overlay stops accepting clicks and can't be dragged,
    // so it sits quietly on the HUD without getting in the way.
    public bool Locked { get; set; } = false;

    // Size of an individual cooldown bar.
    public Vector2 BarSize { get; set; } = new(180, 22);

    // Vertical gap between stacked bars.
    public float BarSpacing { get; set; } = 4f;

    // Once an action is off cooldown there's nothing to count down, so by
    // default we hide its bar to keep the overlay focused on what's recharging.
    public bool HideReadyActions { get; set; } = true;

    // Weaponskills and spells all share the global cooldown, so by default we
    // only bother with oGCDs. Flip this on if you want bars for those too.
    public bool IncludeGcdActions { get; set; } = false;

    // The party panel — the heart of the Sage workflow: who's hurt, who's
    // shielded, and which of your barriers are still ticking on them.
    public bool ShowParty { get; set; } = true;
    public bool PartyLocked { get; set; } = false;

    // A temporary aid while we nail down exactly which statuses to watch: lists
    // every buff/debuff on each member with its raw ID, so we can confirm the
    // real barrier IDs against the live game.
    public bool DebugStatuses { get; set; } = false;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
