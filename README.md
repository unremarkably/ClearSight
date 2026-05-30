# ClearSight

A Dalamud plugin for FINAL FANTASY XIV with two parts: big, readable on-screen cooldown bars so you're not squinting at tiny hotbar timers, and a party panel built for healers (Sage especially) that shows everyone's HP and MP, who's shielded, and whether *your* barriers are still up on them — at a glance.

> Heads up: this is still early in development, so expect rough edges.

## Installing

ClearSight isn't on the official Dalamud plugin list, and I have no plans to put it there. Instead you add my personal repository to Dalamud and grab it from the plugin installer. It's quick:

1. You need XIVLauncher with Dalamud already set up — if you can use other plugins, you're good to go.
2. In game, type `/xlsettings` in chat to open the Dalamud settings.
3. Go to the **Experimental** tab and find **Custom Plugin Repositories**.
4. Paste my repository link into the empty box, hit the **+** button, then **Save and Close**:

   ```
   https://raw.githubusercontent.com/unremarkably/ClearSight/master/repo.json
   ```

5. Open the plugin installer with `/xlplugins`, search for **Clear Sight**, and click **Install**.

Once the repository is added it sticks around, so you'll get updates automatically and won't have to do this again.

## Using it

- `/clearsight` — toggle the cooldown bars.
- `/clearsight party` — toggle the party panel.
- `/clearsight config` — open settings.

Both overlays are draggable; right-click either one to lock it in place once you've got it where you want it. On the party panel, left-click a member to target them and right-click for the usual menu (target, examine, promote, and so on).

## Credits

This started life from goatcorp's [SamplePlugin](https://github.com/goatcorp/SamplePlugin) template, which handled all the boring setup so I could focus on the actual plugin. Thanks to the goatcorp team and everyone who works on Dalamud — none of this exists without them.
