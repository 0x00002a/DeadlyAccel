# Deadly Acceleration

## Introduction 

Space Engineers is a little more forgiving than reality in regards to `F = ma`, this mod fixes that.

Have you ever been in an intense dogfight and done a 180 hairpin at 100m/s with your 99 gyros? 
Well, unforuntely for you, your engineer should be a smear in the cockpit after such a stunt. 

This mod makes you take damage if you accelerate too fast. Over a certain (configurable) amount of acceleration, you will start taking damage. 
The damage is scaled depending on how far past the safe point you are. Being in a cockpit will reduce the damage you take, with 
different cockpits offering more or less protection for your poor engineer against the forces of nature (pun intended).

## Features 

- Death 
- Both linear and angular acceleration is used, spinning too fast with gyros can also cause you to take damage 
- Highly customisable 
- Warnings when exceeding limit (in case you didn't notice the red borders and sound effects)
- Changes how you fight and build ships (no more 180 hairpins at 100m/s)
- Fighter cockpit is now actually useful (it has a _much_ heigher cushion effect allowing you to survive longer at high g)
- Survival only (more an unplanned feature since you cannot take damage in creative)
- "Juice" for increasing resiliance to high g (100% stolen from The Expanse). It can support any `Component` item through the API, see 
    [Deadly Acceleration - Basic Juice Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2464816132) for a mod that uses this. 
	See the [API Guide](https://github.com/0x00002a/DeadlyAccel/API%20Guide.md) for a basic rundown of how to use the API

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

This mod is partially compatable with modded cockpits. While by default modded cockpits will have no cushioning applied, 
they can be added to the config same as vanilla.


## Configuration 

Configuration can be done either through in-game commands (recommended) or through the on-disk XML file. For instructions on how to edit it in-game 
type (in chat) `/da help config` with the mod loaded. Note that in multiplayer only `/da config reload` is avaliable due to sync issues.

- `CushioningBlocks`: List of values for cushioning factors. 
						Final damage per tick is multiplied by the 1 - cushioning value 
						for the cockpit before being applied (e.g. fighter cockpit has a value of 0.9 and therefore reduces all damage by 90%).
                        Note that this property cannot be edited via in-game commands, you must edit the config file on disk directly.
- `IgnoreJetpack`: Whether to ignore force applied due to the jetpack (Warning: Setting this to false may mean you are killed by your jetpack dampers)
- `SafeMaximum`: Acceleration greater than this value will cause the character to take damage. Damage is polynomially (x^k) proportional to how much
					the current acceleration is over this value. The default is 5g because I found that to be the most balanced and is reasonable I think 
					for someone in a futuristic space suit (pilots can apparently survive up to 7g "safely" with proper support but I found this too hard to reach in vanilla)
- `DamageScaleBase`: Exponent for damage scaling (k). Higher values means damage will increase faster further past the safe point 
- `IgnoredGridNames`: List of grid names that are ignored when checking if the pilot should take damage
- `IgnoreRespawnShips`: Whether to ignore respawn ships when applying damage. It defaults to `true` since Vanilla drop-pods are otherwise death traps due to their parachutes
- `VersionNumber`: Ignore this one, its for internal use by the mod

### Breaking changes 

When new options are added to the config file, the old version is backed-up (renamed to "DeadlyAccel.cfg.backup.x" where x is a unique identifer) and the default config is 
loaded. Any modifications to the old config file must be copied over to the new file.

## Bug reports

Report any problems with the mod [here](https://github.com/0x00002a/DeadlyAccel/issues/new/choose). Please include a copy of
your log files and steps to  reproduce it. You can find your log files at `%appdata%\SpaceEngineers\Storage\2422178213.sbm_DeadlyAccel`.

## Reuse/License

All of _my_ code in this repository/mod is licensed under the GNU GPLv3. Some parts of this code are not my own work and I cannot and do not relicense them.
The parts of this code ***not*** licensed under the GNU GPv3 are listed below, if in doubt, all my code has the license at the top:

- Anything in `SENetAPI` (SENetworkAPI)
- Anything in `TextHudAPI` (TextHudAPI mod)

Please contact the respective authors for redistribution rights for these parts of the mod. In regards to redistributing my code:

This mod is GPLv3. That means you can reupload it or any mod that contains it _as long as_ you:

- Keep all existing license notices intact
- Credit me
- List your changes (easiest way is with git and github repo)
- Make _all_ the source code of the relevent mod avaliable freely and publically with no restrictions placed on its access.
- Make your mod GPLv3 as well
- Give me your first born child

(ok that last one isn't actually legally binding)

If in doubt, ask me in comments or the Keen discord (@Natomic). 
Full license is avaliable [here](https://github.com/0x00002a/DeadlyAccel-BasicJuicePack/LICENSE). I reserve the right to ask 
for your mod to be yeeted if you have reused my mod without obeying the license.

### Source

The full source code for this mod can be found here: https://github.com/0x00002a/DeadlyAccel
