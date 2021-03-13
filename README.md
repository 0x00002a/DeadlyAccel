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
- Both linear (forward, left, right, up, down) and angular (pitch, roll, yaw) acceleration is used
- Highly customisable 
	- How much damage is reduced by for cockpits (cushioning)
	- Safe point (when do you start taking damage)
	- Scaling base (exponent to use for damage scaling)
	- Whether to ignore jetpack or not (vanilla jetpack exherts >3g of force when using dampers)
- Warnings when exceeding limit (in case you didn't notice the red borders and sound effects)
- Changes how you fight and build ships (no more 180 hairpins at 100m/s)
- Fighter cockpit is now actually useful (it has a _much_ heigher cushion effect allowing you to survive longer at high g)

### Planned features

This is a whishlist of what I _might_ do with this mod, no promises:

- "Juice" item(s) for increasing resiliance to high g (100% stolen from The Expanse)
- Block damage. Ships can be torn apart by acceleration too (well, force), it just requires a lot more of it

### Support for modded cockpits

This mod is partially compatable with modded cockpits. While by default modded cockpits will have no cushioning applied, 
they can be added to the config same as vanilla.

## Configuration 

- `<CushioningBlocks>`: List of values for cushioning factors. 
						Final damage per tick is multiplied by the 1 - cushioning value 
						for the cockpit before being applied (e.g. fighter cockpit has a value of 0.9 and therefore reduces all damage by 90%)
- `<IgnoreJetpack>`: Whether to ignore force applied due to the jetpack (Warning: Setting this to false may mean you are killed by your jetpack dampers)
- `<SafeMaximum>`: Acceleration greater than this value will cause the character to take damage. Damage is expontential proportioanl to how far
					the current acceleration is over this value. The default is 5g because I found that to be the most balanced and is reasonable I think 
					for someone in a futuristic space suit (pilots can apparently survive up to 7g "safely" with proper support but I found this too hard to reach in vanilla)
- `<DamageScaleBase>`: Exponent for damage scaling. Higher values means damage will increase faster further past the safe point 

## Reuse/Licensing 

All of _my_ code in this repository/mod is licensed under the GNU GPLv3. Some parts of this code are not my own work and I cannot and do not relicense them.
The parts of this code ***not*** licensed under the GNU GPv3 are listed below, if in doubt, all my code has the license at the top:

- `Log.cs` (author Digi)
- Anything in `SENetAPI` (SENetworkAPI)
- Anything in `TextHudAPI` (TextHudAPI mod)

Please contact the respective authors for redistribution rights for these parts of the mod. In regards to redistributing my code, read the license:

```
    DeadlyAccel Space Engineers mod
    Copyright (C) 2021 Natasha England-Elbro

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
```

