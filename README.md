# ClearSight

A Dalamud plugin for FINAL FANTASY XIV that displays your action cooldowns as ticking-down bars on your HUD, so you can track recharge progress at a glance.

> Project status: early development.

## How To Use

### Prerequisites

ClearSight assumes all the following prerequisites are met:

* XIVLauncher, FINAL FANTASY XIV, and Dalamud have all been installed and the game has been run with Dalamud at least once.
* XIVLauncher is installed to its default directories and configurations.
  * If a custom path is required for Dalamud's dev directory, it must be set with the `DALAMUD_HOME` environment variable.
* A .NET 10 SDK has been installed and configured, or is otherwise available. (Required by `Dalamud.NET.Sdk/15.0.0`. Download from [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0).)

### Building

1. Open up `ClearSight.sln` in your C# editor of choice (likely [Visual Studio](https://visualstudio.microsoft.com) or [JetBrains Rider](https://www.jetbrains.com/rider/)).
2. Build the solution. By default, this will build a `Debug` build, but you can switch to `Release` in your IDE.
3. The resulting plugin can be found at `ClearSight/bin/x64/Debug/ClearSight.dll` (or `Release` if appropriate.)

### Activating in-game

1. Launch the game and use `/xlsettings` in chat or `xlsettings` in the Dalamud Console to open up the Dalamud settings.
    * In here, go to `Experimental`, and add the full path to the `ClearSight.dll` to the list of Dev Plugin Locations.
2. Next, use `/xlplugins` (chat) or `xlplugins` (console) to open up the Plugin Installer.
    * In here, go to `Dev Tools > Installed Dev Plugins`, and `ClearSight` should be visible. Enable it.
3. You should now be able to use `/clearsight` (chat) or `clearsight` (console) to toggle the window.

Note that you only need to add it to the Dev Plugin Locations once (Step 1); it is preserved afterwards. You can disable, enable, or load your plugin on startup through the Plugin Installer.

## Credits

This project was bootstrapped from goatcorp's [SamplePlugin](https://github.com/goatcorp/SamplePlugin) template, which provides the build configuration, project layout, and Dalamud integration scaffolding. Thanks to the goatcorp team and Dalamud contributors.

See also the [Dalamud Developer Docs](https://dalamud.dev) for plugin development reference and the [submission guide](https://dalamud.dev/plugin-publishing/submission) for publishing to the official repository.

## Contributing

All participation in this repository is governed by the [Dalamud Code of Conduct](https://dalamud.dev/code-of-conduct). If you use AI tooling at any point, review the [AI Usage Policy](https://dalamud.dev/plugin-publishing/ai-policy) and disclose your level of AI use. Entirely AI-generated submissions to the official Dalamud plugin repository will be rejected, and undisclosed AI use may result in a ban.
