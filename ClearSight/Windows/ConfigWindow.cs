using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ClearSight.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration config;

    public ConfigWindow(Plugin plugin) : base("ClearSight Settings###ClearSightConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;
        config = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var showOverlay = config.ShowOverlay;
        if (ImGui.Checkbox("Show cooldown bars", ref showOverlay))
        {
            config.ShowOverlay = showOverlay;
            config.Save();
        }

        var locked = config.Locked;
        if (ImGui.Checkbox("Lock position", ref locked))
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

        ImGui.Separator();

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
    }
}
