# Deadly Acceleration API guide 

This is a guide/reference for the juice API exposed by Deadly Acceleration. 
If you want an example mod, check out [Deadly Acceleration - Basic Juice Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2464816132).


## Setup

1. Create a mod with the usual folder structure
2. Copy the `API` folder from [`Data/Scripts/DeadlyAccel/API`](/Data/Scripts/DeadlyAccel/API), in this repo, to `Data/Scripts/Your mod name/`
3. Create a session component and make sure its priority is less than 2 (this ensures it loads before Deadly Acceleration)
4. Add `using Natomic.DeadlyAccel.API` to the top of your session file 
5. Create field of type `DeadlyAccelAPI` in your session and call `Init` on it in `LoadData`, passing in your callback function which is where
	you will register all your juice items
6. Make sure to call `Dispose()` on the api field in `UnloadData`, otherwise you will get memory leaks and probably other fun stuff


## Registering items 

Once you have setup your callback function, you will need to actually register your items. The syntax for doing so is as follows:

```cs
api.RegisterJuiceDefinition(new JuiceDefinition {
	SubtypeId: "", // SubtypeId of the item you want to register. Note that it must be a Component type
	ToxicityPerMitigated: 0f, // Toxicity buildup per 1 point of damage mitigated. At 100 juice stops being usable 
	ToxicityDecay: 0f, // Decay of toxicity per update (currently every 10 ticks)
	Ranking: 0, // Determines order of use. Lower ranked juice items are consumed before higher ranked ones
	ConsumptionRate: 0f // Number of units of the item required to prevent 1 point of damage. Can be less than 1
});
```


