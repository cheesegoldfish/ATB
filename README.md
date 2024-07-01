Copied from Omniverse ATB with my custom ATB edits.

Personally I prefer ATB because it is very lightweight on the CPU.

Lots of functions implemented in other BotBases can also be achieved with Dalamud plugins (like Auto commendation in a duty, or need/greeding loot)
and I would rather have Dalamud do those things and get a good and fast lightweight botbase.

1. Updated for DT - VPR & PCT
1. Hotkey target switching narrowed to just the commons I use (None, Nearest Enemy, Best Clustered, Lowest Current HP)
1. Working SmartPull
1. Added Best Clustered target selection for selecting the best mob for AOE.
1. Doesn't target things in PVP that shouldn't generally be targeted automatically like Mammets.


## Installation

ATB is a precompiled routine, which is different than some others. In order to get started as a user, follow these steps.

1. Download the [latest version](https://github.com/cheeseoldfish/ATB/releases/latest/download/ATB.zip).
2. Right click the zip, select properties, and check the "Unblock" option. This is necessary because the zip file includes a dll.
3. Unzip into your `RebornBuddy\BotBases` folder.

## Magitek Usage

You probably also want a way to always use Magitek instead of having to pick the Routine every time you switch classes.

There are lots of options for this maintained by the community.

For convenience here is a ForceMagitek plugin that will automatically choose Magitek for you for every class. 

1. Place this [ForceMagitek.cs](https://raw.githubusercontent.com/cheesegoldfish/ATB/master/Plugins/ForceMagitek/ForceMagitek.cs) file in the `RebornBuddy\Plugins\ForceMagitek\` folder. 
2. Then make sure to enable the plugin in RebornBuddy under the Plugins tab.