using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace ClearSight.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration config;

    public MainWindow(Plugin plugin)
        : base("ClearSight###ClearSightOverlay", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        this.config = plugin.Configuration;
    }

    public void Dispose() { }

    // Closing the window from its title-bar X is just another way of turning the
    // overlay off, so keep the saved setting honest about it.
    public override void OnClose()
    {
        config.ShowOverlay = false;
        config.Save();
    }

    public override void PreDraw()
    {
        // Locking the overlay should make it feel like part of the HUD: no title
        // bar to grab, no background panel, and no accidental dragging.
        if (config.Locked)
            Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground;
        else
            Flags &= ~(ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground);
    }

    public override void Draw()
    {
        plugin.Cooldowns.EnsureTracked(config.IncludeGcdActions);

        var cooldowns = plugin.Cooldowns.SnapshotTracked();
        var anythingDrawn = false;

        foreach (var cd in cooldowns)
        {
            if (config.HideReadyActions && cd.IsReady)
                continue;

            DrawBar(cd);
            ImGui.Dummy(new Vector2(0, config.BarSpacing));
            anythingDrawn = true;
        }

        if (!anythingDrawn)
            ImGui.TextDisabled("Nothing on cooldown right now.");
    }

    private void DrawBar(CooldownInfo cd)
    {
        var label = NameOf(cd.ActionId);

        // Off-charge actions read more naturally as "2.4s" than as a bare fraction,
        // and charge-based actions get a "x2" so you can see banked stacks at a glance.
        var caption = cd.CurrentCharges > 1
            ? $"{label}  x{cd.CurrentCharges}"
            : cd.Remaining > 0
                ? $"{label}  {cd.Remaining:0.0}s"
                : label;

        ImGui.ProgressBar(cd.Progress, config.BarSize, caption);
    }

    private static string NameOf(uint actionId)
    {
        if (Plugin.DataManager.GetExcelSheet<LuminaAction>().TryGetRow(actionId, out var row))
        {
            var name = row.Name.ToString();
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        return $"#{actionId}";
    }
}
