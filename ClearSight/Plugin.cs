using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ClearSight.Windows;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace ClearSight;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static ITargetManager Targets { get; private set; } = null!;
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/clearsight";

    public Configuration Configuration { get; init; }
    public CooldownService Cooldowns { get; init; }
    public PartyTracker Party { get; init; }

    public readonly WindowSystem WindowSystem = new("ClearSight");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private PartyWindow PartyWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Cooldowns = new CooldownService(PlayerState, DataManager, Log);
        Party = new PartyTracker(PartyList, Objects, DataManager);

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        PartyWindow = new PartyWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(PartyWindow);

        // Both overlays just reflect their saved setting, so they come back
        // exactly as you left them last time.
        MainWindow.IsOpen = Configuration.ShowOverlay;
        PartyWindow.IsOpen = Configuration.ShowParty;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the cooldown overlay. \"party\" toggles the party panel, \"config\" opens settings."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
    }

    public void Dispose()
    {
        Cooldowns.Dispose();

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();
        PartyWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "config":
                ToggleConfigUi();
                break;
            case "party":
                SetPartyVisible(!Configuration.ShowParty);
                break;
            default:
                ToggleMainUi();
                break;
        }
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => SetOverlayVisible(!Configuration.ShowOverlay);

    // One path each for showing/hiding the overlays so the setting, the saved
    // config, and the actual window never drift apart.
    public void SetOverlayVisible(bool visible)
    {
        Configuration.ShowOverlay = visible;
        Configuration.Save();
        MainWindow.IsOpen = visible;
    }

    public void SetPartyVisible(bool visible)
    {
        Configuration.ShowParty = visible;
        Configuration.Save();
        PartyWindow.IsOpen = visible;
    }

    // The party panel's interactions, the same ones the native frames give you.
    // Each resolves the member fresh from their entity id, so a stale snapshot
    // can never point us at the wrong character.
    public void TargetMember(uint entityId)
    {
        var obj = Objects.SearchByEntityId(entityId);
        if (obj != null)
            Targets.Target = obj;
    }

    public void FocusMember(uint entityId)
    {
        var obj = Objects.SearchByEntityId(entityId);
        if (obj != null)
            Targets.FocusTarget = obj;
    }

    public unsafe void ExamineMember(uint entityId)
    {
        AgentInspect.Instance()->ExamineCharacter(entityId, false);
    }
}
