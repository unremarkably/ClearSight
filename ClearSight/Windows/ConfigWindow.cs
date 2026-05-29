using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ClearSight.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration config;

    public ConfigWindow(Plugin plugin) : base("ClearSight Settings###ClearSightConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;
        this.plugin = plugin;
        config = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextDisabled("Cooldown bars");

        var showOverlay = config.ShowOverlay;
        if (ImGui.Checkbox("Show cooldown bars", ref showOverlay))
            plugin.SetOverlayVisible(showOverlay);

        var locked = config.Locked;
        if (ImGui.Checkbox("Lock position##bars", ref locked))
        {
            config.Locked = locked;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Stops the bars from being dragged so they stay put while you play.");

        var hideReady = config.HideReadyActions;
        if (ImGui.Checkbox("Hide actions that are ready", ref hideReady))
        {
            config.HideReadyActions = hideReady;
            config.Save();
        }

        var includeGcd = config.IncludeGcdActions;
        if (ImGui.Checkbox("Include weaponskills and spells", ref includeGcd))
        {
            config.IncludeGcdActions = includeGcd;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("These share the global cooldown. Off by default so the overlay only shows oGCDs.");

        var barSize = config.BarSize;
        if (ImGui.DragFloat2("Bar size", ref barSize, 1f, 20f, 600f))
        {
            config.BarSize = barSize;
            config.Save();
        }

        var spacing = config.BarSpacing;
        if (ImGui.DragFloat("Spacing", ref spacing, 0.5f, 0f, 40f))
        {
            config.BarSpacing = spacing;
            config.Save();
        }

        ImGui.Separator();
        ImGui.TextDisabled("Party panel");

        var showParty = config.ShowParty;
        if (ImGui.Checkbox("Show party panel", ref showParty))
            plugin.SetPartyVisible(showParty);

        var partyLocked = config.PartyLocked;
        if (ImGui.Checkbox("Lock position##party", ref partyLocked))
        {
            config.PartyLocked = partyLocked;
            config.Save();
        }

        var debug = config.DebugStatuses;
        if (ImGui.Checkbox("Show every status (debug)", ref debug))
        {
            config.DebugStatuses = debug;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Lists every buff/debuff with its raw ID, to help confirm which barriers to track.");
    }
}
