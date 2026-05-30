using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace ClearSight.Windows;

// Shared drawing for the rounded, shadowed bars both overlays use, so the
// cooldown and party windows stay visually in step.
internal static class OverlayBars
{
    public static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.9f);
    private static readonly Vector4 Track = new(0f, 0f, 0f, 0.55f);
    private const float Rounding = 3f;

    public static void Draw(float width, float height, float fraction, Vector4 fill, string label)
        => Draw(width, height, fraction, 0f, fill, default, label);

    public static void Draw(float width, float height, float fraction, float shieldFraction, Vector4 fill, Vector4 shieldColor, string label)
    {
        var dl = ImGui.GetWindowDrawList();
        var p = ImGui.GetCursorScreenPos();
        var max = new Vector2(p.X + width, p.Y + height);

        dl.AddRectFilled(p, max, ImGui.GetColorU32(Track), Rounding);

        var fillWidth = width * Math.Clamp(fraction, 0f, 1f);
        if (fillWidth > 0)
            dl.AddRectFilled(p, new Vector2(p.X + fillWidth, max.Y), ImGui.GetColorU32(fill), Rounding);

        if (shieldFraction > 0)
        {
            var start = width * Math.Clamp(fraction, 0f, 1f);
            var end = width * Math.Clamp(fraction + shieldFraction, 0f, 1f);
            if (end > start)
                dl.AddRectFilled(new Vector2(p.X + start, p.Y), new Vector2(p.X + end, max.Y), ImGui.GetColorU32(shieldColor));
        }

        dl.AddRect(p, max, ImGui.GetColorU32(Shadow), Rounding);

        if (!string.IsNullOrEmpty(label))
        {
            var size = ImGui.CalcTextSize(label);
            var at = new Vector2(p.X + (width - size.X) * 0.5f, p.Y + (height - size.Y) * 0.5f);
            dl.AddText(new Vector2(at.X + 1, at.Y + 1), ImGui.GetColorU32(Shadow), label);
            dl.AddText(at, 0xFFFFFFFFu, label);
        }

        ImGui.Dummy(new Vector2(width, height));
    }
}
