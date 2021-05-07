# Deadly Acceleration Changelog


## v1.5.0

This version is mostly QoL and bugfixes. 

### Features

- Damage due to jetpack is no longer ignored even if IgnoreJetpack is true. Config value added to adjust this. Accel of the locked to 
	grid is used instead. IgnoredGridNames also applies here 
- HUD hidden in creative. Due to limitations of the mod api I cannot detect if a player is invincible so this is the next best thing. 
	A config option has been added to adjust this 
- Editing config via command-line in multiplayer. Only admins can use this currently

### Fixes

- Memory leak and resultant duplicated HUD elements
- Damage when walking. The detection logic for checking if the player is walking _should_ now be watertight. So no more insta-death when 
	clipping off a block (hurray!)
- IgnoredGridNames now applies to the grid you are standing on as well. Note this uses the old detection system so it may not always be 100% accurate
- The HUD elements will no longer be drawn while in any form of menu (inventory, escape menu, etc)
- Will no longer spam the info log with a bunch of XML 


## v1.4.0

This version has several notable improvements to the UI and playability of this mod (MP support) as well as "juice" item support. 

### Features 

- Juice items, inc
	- Scripting API to allow any other mod to create "juice" items 
	- "Bottle HUD" showing the current fill level (1 unit of a juice item is considered to be a full bottle) of the juice being used 
	- Support for multiple juice types, ranked based on API settings 
- Toxicity mechanic, inc
	- Toxicity HUD showing your current buildup (when it hits 100% you stop being able to use juice)
	- Toxicity decay (based on the juice used)
	- Toxicity buildup is based on the juice used and how much damage it is being used to prevent 
- Multiplayer support. Features (including this update) are now tested in dedicated servers and hosted multiplayer althought not with multiple 
	people (dedicated server testing simulates client of hosted multiplayer to some extent).
- Icons! No more hard to read, misaligned red text when you are in danger now there is a large flashing warning triangle. There are 
	also icons for the bottle and toxicity hud
