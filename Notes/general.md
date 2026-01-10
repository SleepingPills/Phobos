# Formation Movement
We now have a squad leader, so we can anchor everything to this.

* Only the squad leader gets the objectives. Make sure to add a condition to the score that prevents other bots getting it.
* Other bots will have FollowSquadLead action, where the formation movement will be built.
* The squad itself will decide on and set up the formations. It'll choose between diamond and other formations.
* Formations will always be built one step ahead, so that the various path checks can be done asynchronously.

Formation Types:
* Diamond formation: if the next corner is more than 5m away
* Column formation: if it's less than that. This formation will simply cause the non squad lead bots to go the next corner.

Formation Logic:
* Calculate the formation targets 1 step ahead using navmesh raycasts.
* The bot will be moving to the current formation target
* We'll also calculate idealized current positions around the squad lead which will track where the squad members should be in any given time. They'll slow down and speed up to be close to these points.

Bot Move Notes:
* If the squad lead detects low cohesion, it'll slow down
* Other bots will maintain speed, unless they are within 1m of the current formation position, in which case they'll match the squad lead speed.