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

    // Where the overlay sits on screen, remembered between sessions.
    public Vector2 OverlayPosition { get; set; } = new(200, 200);

    // Size of an individual cooldown bar.
    public Vector2 BarSize { get; set; } = new(180, 22);

    // Vertical gap between stacked bars.
    public float BarSpacing { get; set; } = 4f;

    // Once an action is off cooldown there's nothing to count down, so by
    // default we hide its bar to keep the overlay focused on what's recharging.
    public bool HideReadyActions { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
