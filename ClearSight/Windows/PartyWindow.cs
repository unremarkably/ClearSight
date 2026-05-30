using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace ClearSight.Windows;

public class PartyWindow : Window, IDisposable
{
    private static readonly Vector4 MyBarrier = new(0.45f, 0.85f, 1.0f, 1f);
    private static readonly Vector4 BarrierMissing = new(1f, 0.35f, 0.35f, 1f);
    private static readonly Vector4 ShieldTint = new(1f, 0.88f, 0.40f, 1f);
    private static readonly Vector4 ShieldOverlay = new(1f, 0.88f, 0.40f, 0.7f);
    private static readonly Vector4 CrownColor = new(1f, 0.82f, 0.30f, 1f);

    private static readonly Vector4 HpFull = new(0.36f, 0.76f, 0.37f, 1f);
    private static readonly Vector4 HpLow = new(0.83f, 0.58f, 0.25f, 1f);
    private static readonly Vector4 MpFill = new(0.35f, 0.51f, 0.77f, 1f);

    private static readonly Vector4 TankColor = new(0.43f, 0.61f, 0.91f, 1f);
    private static readonly Vector4 HealerColor = new(0.44f, 0.88f, 0.60f, 1f);
    private static readonly Vector4 DpsColor = new(0.93f, 0.51f, 0.51f, 1f);
    private static readonly Vector4 NeutralColor = new(0.82f, 0.82f, 0.82f, 1f);

    private readonly Plugin plugin;
    private readonly Configuration config;

    // Set while drawing if the cursor is over a member, so a right-click on empty
    // space can tell itself apart from a right-click on someone.
    private bool memberHovered;

    // Whether the local player can hand out leader/kick — refreshed each frame.
    private bool localIsLeader;

    // The panel's own rectangle, captured each frame so the native menu can be
    // opened just outside it (it would otherwise hide behind the panel).
    private Vector2 panelPos;
    private Vector2 panelSize;

    public PartyWindow(Plugin plugin)
        : base("ClearSight Party###ClearSightParty", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        this.config = plugin.Configuration;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 90),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        // It's a HUD element, not a popup — don't let the Escape key close it.
        RespectCloseHotkey = false;
    }

    public void Dispose() { }

    public override void OnClose()
    {
        config.ShowParty = false;
        config.Save();
    }

    public override void PreDraw()
    {
        // The panel is always chrome-free and transparent — it lives on the HUD.
        // Unlocked, you drag it by its body; locked, it just sits there.
        Flags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground;
        if (config.PartyLocked)
            Flags |= ImGuiWindowFlags.NoMove;
        else
            Flags &= ~ImGuiWindowFlags.NoMove;
    }

    public override void Draw()
    {
        memberHovered = false;
        panelPos = ImGui.GetWindowPos();
        panelSize = ImGui.GetWindowSize();

        if (config.PartyHeaderVisible)
            DrawHeader();

        var party = plugin.Party.Snapshot();
        if (party.Count == 0)
        {
            ImGui.TextColored(NeutralColor, "Not in a party.");
            HandlePanelMenu();
            return;
        }

        localIsLeader = false;
        foreach (var m in party)
            if (m.IsSelf && m.IsLeader)
                localIsLeader = true;

        var width = MathF.Max(ImGui.GetContentRegionAvail().X, 200f);
        for (var i = 0; i < party.Count; i++)
        {
            if (i > 0)
                ImGui.Spacing();
            DrawMember(party[i], width);
        }

        HandlePanelMenu();
    }

    // ── Header ───────────────────────────────────────────────────────────────

    private void DrawHeader()
    {
        var dl = ImGui.GetWindowDrawList();
        var caretSize = 6f * ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var lineH = ImGui.GetTextLineHeight();

        // A little triangle that points down when expanded, right when collapsed.
        var cy = origin.Y + lineH * 0.5f;
        var cx = origin.X + caretSize * 0.5f;
        var caretColor = ImGui.GetColorU32(NeutralColor);
        if (config.PartyFiltersCollapsed)
            dl.AddTriangleFilled(new Vector2(cx - 3, cy - 4), new Vector2(cx + 3, cy), new Vector2(cx - 3, cy + 4), caretColor);
        else
            dl.AddTriangleFilled(new Vector2(cx - 4, cy - 3), new Vector2(cx + 4, cy - 3), new Vector2(cx, cy + 3), caretColor);

        ImGui.Dummy(new Vector2(caretSize + 4f, lineH));
        ImGui.SameLine();

        // The title doubles as the collapse toggle for the filter chips.
        ImGui.TextColored(NeutralColor, "ClearSight — Party");
        if (ImGui.IsItemClicked())
        {
            config.PartyFiltersCollapsed = !config.PartyFiltersCollapsed;
            config.Save();
        }

        if (!config.PartyFiltersCollapsed)
        {
            if (Chip("Buffs", config.ShowBuffs)) { config.ShowBuffs = !config.ShowBuffs; config.Save(); }
            ImGui.SameLine();
            if (Chip("Debuffs", config.ShowDebuffs)) { config.ShowDebuffs = !config.ShowDebuffs; config.Save(); }
            ImGui.SameLine();
            if (Chip("Hide permanent", config.HidePermanentStatuses)) { config.HidePermanentStatuses = !config.HidePermanentStatuses; config.Save(); }
            ImGui.SameLine();
            if (Chip("Numbers", config.ShowHpNumbers)) { config.ShowHpNumbers = !config.ShowHpNumbers; config.Save(); }
        }

        ImGui.Spacing();
    }

    private static bool Chip(string label, bool active)
    {
        var bg = active ? new Vector4(0.29f, 0.55f, 0.7f, 0.55f) : new Vector4(0.15f, 0.17f, 0.2f, 0.55f);
        ImGui.PushStyleColor(ImGuiCol.Button, bg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, bg + new Vector4(0.1f, 0.1f, 0.1f, 0.1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, bg);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 8f);
        var clicked = ImGui.SmallButton(label);
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(3);
        return clicked;
    }

    // ── Members ──────────────────────────────────────────────────────────────

    private void DrawMember(PartyMemberInfo member, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var iconSize = 22f * scale;

        ImGui.BeginGroup();

        // Row 1: barrier dot, job icon, name, shield %.
        var rowTop = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();

        var dotDiameter = 9f * scale;
        if (!member.IsSelf)
        {
            var center = new Vector2(rowTop.X + dotDiameter * 0.5f, rowTop.Y + iconSize * 0.5f);
            dl.AddCircleFilled(center, dotDiameter * 0.5f, ImGui.GetColorU32(member.HasMyBarrier ? MyBarrier : BarrierMissing));
        }
        ImGui.Dummy(new Vector2(dotDiameter + 3f, iconSize));
        ImGui.SameLine();

        var jobIcon = Plugin.Textures.GetFromGameIcon(new GameIconLookup(62100 + member.JobId)).GetWrapOrEmpty();
        ImGui.Image(jobIcon.Handle, new Vector2(iconSize, iconSize));
        ImGui.SameLine();

        // Center the name vertically against the job icon.
        var nameY = ImGui.GetCursorPosY() + (iconSize - ImGui.GetTextLineHeight()) * 0.5f;
        ImGui.SetCursorPosY(nameY);
        ImGui.TextColored(RoleColor(member.Role), member.Name);

        if (member.IsLeader)
        {
            ImGui.SameLine();
            ImGui.SetCursorPosY(nameY);
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.TextColored(CrownColor, FontAwesomeIcon.Crown.ToIconString());
            ImGui.PopFont();
        }

        if (member.ShieldPercent > 0)
        {
            ImGui.SameLine();
            ImGui.SetCursorPosY(nameY);
            ImGui.TextColored(ShieldTint, $"+{member.ShieldPercent}%");
        }

        // Row 2: HP bar with the overshield riding on top.
        OverlayBars.Draw(width, 18f * scale, member.HpFraction, member.ShieldPercent / 100f,
            member.HpFraction < 0.5f ? HpLow : HpFull, ShieldOverlay,
            BuildHpLabel(member, width));

        // Row 3: MP bar, taller when it carries a value so the text fits.
        var mpHasValue = config.ShowMpValue && member.MaxMp > 0;
        OverlayBars.Draw(width, (mpHasValue ? 13f : 6f) * scale, member.MpFraction, MpFill,
            mpHasValue ? BuildMpLabel(member, width) : "");

        DrawStatusIcons(member, width);

        if (config.DebugStatuses)
            DrawDebugStatuses(member);

        ImGui.EndGroup();
        HandleInteraction(member);
    }

    private void DrawStatusIcons(PartyMemberInfo member, float width)
    {
        var visible = VisibleStatuses(member);
        if (visible.Count == 0)
            return;

        var scale = ImGuiHelpers.GlobalScale;
        var size = 26f * scale;
        var dl = ImGui.GetWindowDrawList();

        // How many icons fit on a row, so extras wrap instead of spilling past the edge.
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)((width + spacing) / (size + spacing)));

        for (var i = 0; i < visible.Count; i++)
        {
            var status = visible[i];

            if (i > 0 && i % perRow != 0)
                ImGui.SameLine();

            // A status only counts as stacking when the sheet says so — otherwise
            // Param holds unrelated data we shouldn't read as a stack number. The
            // game also lays out one icon per stack, so we step the icon to match.
            var stacks = status.MaxStacks > 1 ? status.Stacks : (ushort)0;
            var iconId = status.IconId;
            if (stacks > 1)
                iconId += (uint)Math.Min(stacks - 1, status.MaxStacks - 1);

            var p = ImGui.GetCursorScreenPos();
            var tex = Plugin.Textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
            ImGui.Image(tex.Handle, new Vector2(size, size));

            if (status.IsTracked && status.Mine)
                dl.AddRect(p, new Vector2(p.X + size, p.Y + size), ImGui.GetColorU32(MyBarrier), 2f, ImDrawFlags.None, 2f);

            if (status.RemainingTime > 0)
            {
                var time = status.RemainingTime >= 10 ? $"{status.RemainingTime:0}" : $"{status.RemainingTime:0.0}";
                var ts = ImGui.CalcTextSize(time);
                var tp = new Vector2(p.X + (size - ts.X) * 0.5f, p.Y + size - ts.Y);
                dl.AddText(new Vector2(tp.X + 1, tp.Y + 1), ImGui.GetColorU32(OverlayBars.Shadow), time);
                dl.AddText(tp, 0xFFFFFFFFu, time);
            }

            if (stacks > 1)
            {
                var label = stacks.ToString();
                dl.AddText(new Vector2(p.X + 2, p.Y + 1), ImGui.GetColorU32(OverlayBars.Shadow), label);
                dl.AddText(new Vector2(p.X + 1, p.Y), 0xFFFFFFFFu, label);
            }
        }
    }

    private List<StatusInfo> VisibleStatuses(PartyMemberInfo member)
    {
        var result = new List<StatusInfo>();
        foreach (var s in member.Statuses)
        {
            if (s.IsTracked) { result.Add(s); continue; } // your barriers always show
            if (config.HidePermanentStatuses && s.IsPermanent) continue;
            if (s.IsDebuff && !config.ShowDebuffs) continue;
            if (!s.IsDebuff && !config.ShowBuffs) continue;
            result.Add(s);
        }

        // Your barriers lead, then whatever's expiring soonest.
        result.Sort((a, b) =>
        {
            var aMine = a.IsTracked && a.Mine;
            var bMine = b.IsTracked && b.Mine;
            if (aMine != bMine) return aMine ? -1 : 1;
            return a.RemainingTime.CompareTo(b.RemainingTime);
        });
        return result;
    }

    // ── Number formatting ──────────────────────────────────────────────────��─

    private string BuildHpLabel(PartyMemberInfo member, float width)
    {
        var pct = $"{member.HpFraction * 100f:0}%";
        if (!config.ShowHpNumbers)
            return pct;

        var full = $"{member.CurrentHp.ToString("N0", CultureInfo.InvariantCulture)} ({pct})";
        if (ImGui.CalcTextSize(full).X <= width - 10f)
            return full;

        var abbreviated = $"{Abbreviate(member.CurrentHp)} ({pct})";
        if (ImGui.CalcTextSize(abbreviated).X <= width - 10f)
            return abbreviated;

        return pct;
    }

    private string BuildMpLabel(PartyMemberInfo member, float width)
    {
        var full = member.CurrentMp.ToString("N0", CultureInfo.InvariantCulture);
        if (ImGui.CalcTextSize(full).X <= width - 10f)
            return full;
        return Abbreviate(member.CurrentMp);
    }

    private static string Abbreviate(uint value)
    {
        if (value >= 1000)
            return $"{value / 1000f:0.#}k";
        return value.ToString(CultureInfo.InvariantCulture);
    }

    // ── Interaction ────────────────────────────────────────────────────────��─

    private static void DrawDebugStatuses(PartyMemberInfo member)
    {
        foreach (var s in member.Statuses)
        {
            var mark = s.Mine ? "*" : " ";
            ImGui.TextDisabled($"{mark}{s.StatusId,6}  {s.Name}  {s.RemainingTime:0}s");
        }
    }

    private void HandleInteraction(PartyMemberInfo member)
    {
        var popupId = $"member##{member.EntityId}";

        // Hit-test the member's actual rectangle — more reliable than group hover
        // when the row is built from raw draw-list bars rather than real widgets.
        var hovered = ImGui.IsWindowHovered()
            && ImGui.IsMouseHoveringRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax());

        if (hovered)
        {
            memberHovered = true;
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                plugin.TargetMember(member.EntityId);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                ImGui.OpenPopup(popupId);
        }

        // Our own menu (it renders above the panel — the native one would open
        // behind it) wired to the real game functions.
        if (ImGui.BeginPopup(popupId))
        {
            ImGui.TextColored(RoleColor(member.Role), member.Name);
            ImGui.Separator();

            if (ImGui.MenuItem("Target"))
                plugin.TargetMember(member.EntityId);
            if (ImGui.MenuItem("Focus Target"))
                plugin.FocusMember(member.EntityId);
            if (ImGui.MenuItem("Examine"))
                plugin.ExamineMember(member.EntityId);

            // Promote/kick only when you're the leader and it's another real
            // player — the game still asks for confirmation.
            if (localIsLeader && !member.IsSelf && member.ContentId != 0)
            {
                ImGui.Separator();
                if (ImGui.MenuItem("Promote to leader"))
                    plugin.PromoteMember(member.Name, member.ContentId);
                if (ImGui.MenuItem("Kick from party"))
                    plugin.KickMember(member.Name, member.ContentId);
            }

            if (member.IsSelf)
            {
                ImGui.Separator();
                if (ImGui.MenuItem("Leave party"))
                    plugin.LeaveParty();
            }

            ImGui.Separator();
            if (ImGui.MenuItem("More options…"))
                OpenNativeMenuBesidePanel(member.EntityId);

            ImGui.Separator();
            DrawPanelMenuItems();
            ImGui.EndPopup();
        }
    }

    // Place the native menu just past the panel's edge so it isn't covered —
    // to the right normally, flipping left when there's no room there.
    private void OpenNativeMenuBesidePanel(uint entityId)
    {
        const float estimatedMenuWidth = 220f;
        var displayWidth = ImGui.GetIO().DisplaySize.X;

        var x = panelPos.X + panelSize.X + 4f;
        if (x + estimatedMenuWidth > displayWidth)
            x = panelPos.X - estimatedMenuWidth - 4f;
        if (x < 0)
            x = panelPos.X + panelSize.X + 4f;

        var y = ImGui.GetMousePos().Y;
        plugin.OpenNativeContextMenu(entityId, (int)x, (int)y);
    }

    // Right-clicking empty panel space (not a member) still gets you the menu —
    // important when the header is hidden and there's no other way back.
    private void HandlePanelMenu()
    {
        if (!memberHovered && ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup("panelMenu");

        if (ImGui.BeginPopup("panelMenu"))
        {
            DrawPanelMenuItems();
            ImGui.EndPopup();
        }
    }

    private void DrawPanelMenuItems()
    {
        if (ImGui.MenuItem(config.PartyHeaderVisible ? "Hide header" : "Show header"))
        {
            config.PartyHeaderVisible = !config.PartyHeaderVisible;
            config.Save();
        }
        if (ImGui.MenuItem(config.PartyLocked ? "Unlock panel" : "Lock panel"))
        {
            config.PartyLocked = !config.PartyLocked;
            config.Save();
        }
    }

    private static Vector4 RoleColor(byte role) => role switch
    {
        1 => TankColor,
        4 => HealerColor,
        2 or 3 => DpsColor,
        _ => NeutralColor,
    };
}
