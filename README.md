﻿# Deadly Acceleration

## Introduction 

Space Engineers is a little more forgiving than reality in regards to `F = ma`, this mod fixes that.

Have you ever been in an intense dogfight and done a 180 hairpin at 100m/s with your 99 gyros? 
Well, unfortunately for you, your engineer should be a smear in the cockpit after such a stunt. 

This mod makes you take damage if you accelerate too fast. Over a certain (configurable) amount of acceleration, you will start taking damage. 
The damage is scaled depending on how far past the safe point you are. Being in a cockpit will reduce the damage you take, with 
different cockpits offering more or less protection for your poor engineer against the forces of nature (pun intended).

## Features 

- Death 
- Both linear and angular acceleration is used, spinning too fast with gyros can also cause you to take damage 
- Highly customisable 
- Warnings when exceeding limit (in case you didn't notice the red borders and sound effects)
- Changes how you fight and build ships (no more 180 hairpins at 100m/s)
- Fighter cockpit is now actually useful (it has a _much_ higher cushion effect allowing you to survive longer at high g)
- Survival only (more an unplanned feature since you cannot take damage in creative)
- Damage is inversely scaled by time for small time values (configurable, see: TimeScaling). Allowing brief exposure to high acceleration with little or no damage
- "Juice" for increasing resilience to high g (100% stolen from The Expanse). It can support any `Component` item through the API, see 
    [Deadly Acceleration - Basic Juice Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2464816132) for a mod that uses this. 
    See the [API Guide](https://github.com/0x00002a/DeadlyAccel/blob/main/API%20Guide.md) for a basic rundown of how to use the API

### A note on speed limits and balance

This mod is usable with a 100m/s speed limit, but only really in fights, since a 5g acceleration will get you to 100m/s in ~2 seconds. Be aware, however, when 
using modded thrusters or very fast ships since going too far over the safe point can kill you before you can react. This mod is currently only tested by me and therefore 
probably not very balanced. I suggest tuning the config to match your personal tastes.

### Juice and toxicity mechanic 

Overall juice is pretty self explanatory. Make it, slap it in your cockpit, and blast through a turn with no damage. Some things to note however:

- Juice only works if it is in the inventory of the _cockpit_ you are in, it will not work if it is in your personal inventory. This is an intentional design decision 
- Toxicity will decay over time naturally. However, if you use multiple juices without using a medbay, it will use the ***lowest*** decay rate of the juices used 
	(e.g. you use one with `0.1` decay rate and one with `0.001`, your decay rate is now `0.001`)
- Using a medbay (or survival kit) will cause the decay rate to increase dramatically (should decay from 100 in a few seconds tops). Additionally resetting toxicity to 0 
	using a medbay will also reset your toxic decay rate


### Multiplayer

This mod undergoes testing in single-player, hosted multiplayer, and dedicated server environments. I am the only tester though so there may be bugs that only occure with 
multiple players. Please report them.


### Planned features

This is a whishlist of what I _might_ do with this mod, no promises:

- Block damage. Ships can be torn apart by acceleration too (well, force), it just requires a lot more of it

### Support for modded cockpits

This mod is partially compatible with modded cockpits. While by default modded cockpits will have no cushioning applied, 
they can be added to the config same as vanilla.


## Configuration 

I've run out of space here so configuration instructions can now be viewed by typing `/da help config`. 
This works in multiplayer or single-player. Note however that currently only admin players can do this in multiplayer. 

### Breaking changes 

When new options are added to the config file, the old version is backed-up (renamed to "DeadlyAccel.cfg.backup.x" where x is a unique identifier) and the default config is 
loaded. Any modifications to the old config file must be copied over to the new file.

## Bug reports

Report any problems with the mod [here](https://github.com/0x00002a/DeadlyAccel/issues/new/choose). Please include a copy of
your log files and steps to  reproduce it. You can find your log files at `%appdata%\SpaceEngineers\Storage\2422178213.sbm_DeadlyAccel`.

## Reuse/License

I've run out of space in the description, look at the license in the repo but tl;dr I will ask for your mod to be yeeted if you reuse without following the license and its GPLv3.

### Source

The full source code for this mod can be found here: https://github.com/0x00002a/DeadlyAccel
