using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ClearSight.Windows;

public class PartyWindow : Window, IDisposable
{
    // Shields you cast glow; anyone else's barriers stay muted so the party's
    // protection is visible without stealing attention from your own work.
    private static readonly Vector4 MyBarrier = new(0.45f, 0.85f, 1.0f, 1f);
    private static readonly Vector4 OtherBarrier = new(0.6f, 0.6f, 0.6f, 1f);
    private static readonly Vector4 ShieldTint = new(0.95f, 0.85f, 0.35f, 1f);

    private readonly Plugin plugin;
    private readonly Configuration config;

    public PartyWindow(Plugin plugin)
        : base("ClearSight Party###ClearSightParty", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        this.config = plugin.Configuration;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(180, 80),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose() { }

    public override void OnClose()
    {
        config.ShowParty = false;
        config.Save();
    }

    public override void PreDraw()
    {
        if (config.PartyLocked)
            Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground;
        else
            Flags &= ~(ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground);
    }

    public override void Draw()
    {
        var party = plugin.Party.Snapshot();
        if (party.Count == 0)
        {
            ImGui.TextDisabled("Not in a party.");
            return;
        }

        var width = MathF.Max(ImGui.GetContentRegionAvail().X, 180f);

        for (var i = 0; i < party.Count; i++)
        {
            if (i > 0)
                ImGui.Spacing();
            DrawMember(party[i], width);
        }
    }

    private void DrawMember(PartyMemberInfo member, float width)
    {
        var hpText = $"{member.Name}  {member.HpFraction * 100f:0}%";
        ImGui.ProgressBar(member.HpFraction, new Vector2(width, 20f), hpText);

        if (member.ShieldPercent > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(ShieldTint, $"+{member.ShieldPercent}%");
        }

        foreach (var status in member.Statuses)
        {
            if (!status.IsTracked)
                continue;

            var color = status.Mine ? MyBarrier : OtherBarrier;
            var time = status.RemainingTime > 0 ? $"  {status.RemainingTime:0}s" : "";
            var tag = status.Mine ? "" : "  (other)";
            ImGui.TextColored(color, $"   {status.Name}{time}{tag}");
        }

        if (config.DebugStatuses)
            DrawDebugStatuses(member);
    }

    // The discovery aid: every status with its raw ID, so we can confirm which
    // ones are the barriers worth tracking against the real game.
    private void DrawDebugStatuses(PartyMemberInfo member)
    {
        using var indent = Dalamud.Interface.Utility.Raii.ImRaii.PushIndent();
        foreach (var status in member.Statuses)
        {
            var mark = status.Mine ? "*" : " ";
            ImGui.TextDisabled($"{mark} {status.StatusId,5}  {status.Name}  {status.RemainingTime:0}s");
        }
    }
}
