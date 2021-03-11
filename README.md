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
	- Scaling base (logarithmic base to use for damage scaling)
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
- `<SafeMaximum>`: Acceleration greater than this value will cause the character to take damage. Damage is logarithmically proportioanl to how far
					the current acceleration is over this value. The default is 5g because I found that to be the most balanced and is reasonable I think 
					for someone in a futuristic space suit
- `<DamageScaleBase>`: Base for logarithmic damage scaling. Default is 20, higher values mean less damage and slower scaling.

