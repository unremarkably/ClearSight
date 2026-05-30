using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace ClearSight.Windows;

public class MainWindow : Window, IDisposable
{
    // A calm blue that fills as the ability recharges — when the bar is full,
    // it's ready. The big centered timer is the whole point: no more squinting.
    private static readonly Vector4 CooldownFill = new(0.40f, 0.52f, 0.80f, 1f);

    private readonly Plugin plugin;
    private readonly Configuration config;

    public MainWindow(Plugin plugin)
        : base("ClearSight###ClearSightOverlay", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        this.config = plugin.Configuration;

        // It's a HUD element, not a popup — don't let the Escape key close it.
        RespectCloseHotkey = false;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Chrome-free and transparent like the party panel; drag to move when
        // unlocked, frozen in place when locked.
        Flags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground;
        if (config.Locked)
            Flags |= ImGuiWindowFlags.NoMove;
        else
            Flags &= ~ImGuiWindowFlags.NoMove;
    }

    public override void Draw()
    {
        plugin.Cooldowns.EnsureTracked(config.IncludeGcdActions);
        var cooldowns = plugin.Cooldowns.SnapshotTracked();

        var anyDrawn = false;
        foreach (var cd in cooldowns)
        {
            if (config.HideReadyActions && cd.IsReady)
                continue;

            DrawCooldown(cd);
            ImGui.Dummy(new Vector2(0, config.BarSpacing));
            anyDrawn = true;
        }

        if (!anyDrawn)
            ImGui.TextDisabled("Nothing on cooldown right now.");

        HandleMenu();
    }

    private void DrawCooldown(CooldownInfo cd)
    {
        var iconSize = config.BarSize.Y;

        ImGui.BeginGroup();

        var iconPos = ImGui.GetCursorScreenPos();
        var icon = Plugin.Textures.GetFromGameIcon(new GameIconLookup(IconOf(cd.ActionId))).GetWrapOrEmpty();
        ImGui.Image(icon.Handle, new Vector2(iconSize, iconSize));

        // Stacked actions show how many charges are banked in the icon corner.
        if (cd.MaxCharges > 1)
        {
            var dl = ImGui.GetWindowDrawList();
            var charges = cd.CurrentCharges.ToString();
            dl.AddText(new Vector2(iconPos.X + 2, iconPos.Y + 1), ImGui.GetColorU32(OverlayBars.Shadow), charges);
            dl.AddText(new Vector2(iconPos.X + 1, iconPos.Y), 0xFFFFFFFFu, charges);
        }

        ImGui.SameLine();

        var label = cd.Remaining > 0 ? FormatTime(cd.Remaining) : string.Empty;
        OverlayBars.Draw(config.BarSize.X, config.BarSize.Y, cd.Progress, CooldownFill, label);

        ImGui.EndGroup();

        // The icon already says which ability it is, so the name lives in a hover
        // tooltip rather than crowding the bar.
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(NameOf(cd.ActionId));
    }

    private static string FormatTime(float seconds)
        => seconds >= 10f ? $"{MathF.Ceiling(seconds):0}" : $"{seconds:0.0}";

    private void HandleMenu()
    {
        if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup("cooldownMenu");

        if (ImGui.BeginPopup("cooldownMenu"))
        {
            if (ImGui.MenuItem(config.Locked ? "Unlock bars" : "Lock bars"))
            {
                config.Locked = !config.Locked;
                config.Save();
            }
            ImGui.EndPopup();
        }
    }

    private static uint IconOf(uint actionId)
    {
        if (Plugin.DataManager.GetExcelSheet<LuminaAction>().TryGetRow(actionId, out var row))
            return row.Icon;
        return 0;
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
