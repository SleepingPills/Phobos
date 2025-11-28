# High Level Plan
1. First PoC with basic squad level goal planning
2. Holistic strategic squad AI using GOAP for immersive bot behavior (buzzword bingo)

# PoC
1. On spawn, Actor takes the current objective from the squad
2. On Update
   1. If objective is null, we bail out.
   2. If objective is not-null and 

## Main Layer Logic
1. Check if bot is nonnull and alive, if not drop from the squad and disable the layer itself.

# End Goals (add ideas over time)
## Gameplay
* GOAP style strategic planning including picking ideal points of entry, approaching from multiple angles, etc...
* Strategically picking objectives at raid start.

* Bot personalities affect strategic planning
* Bosses and special bot types can have "hunt the player(s)" objectives.
  * Some even can have "stalk" objectives where they try to stay hidden.

## Objectives
* Stalk players (bosses/rogues only?)
* Quests
* Loot goblining
* Exfil (after enough other objectives have been accomplished or got hurt too much)
* Regroup (if a team member is under attack or the team got spread out too much)

## Infra
* Add job queues for various operations like pathfinding. We'll only do X number of these per frame to smooth out exec.

# Notes
## Pathing Logic
* Run one iteration of the pathing. If it's not invalid, we run with it.
* As the bot nears the final corner (we can check the current corner progress in the controller) we check if the path was partial. If yes, we invalidate the path and it again and observe if it got us closer to the destination.
* If the improvement is less than 5m or 25%, we consider the pathing complete and do not further try to improve it.
* If the bot was sidetracked, we invalidate the path and recalculate it again.
* Eventually we add a partial invalidation - if the bot was sidetracked, we'll only try to find a path back to our last corner.
* If the bot is on the path but hasn't progressed at all to the next point, it's probably stuck.

## Layer usage state
`BotOwner.Brain.Agent.UsingLayer` can be used to check if the strategic layer is being used or not
NB: This can't be done from the layer itself since it doesn't run if it's not being used currently.

## Loot & Quest Locations
`LocationScene` and `Location` contain some pointers on how to get stuff and what stuff is available.

## Pathing
PathControllerClass.SetPath() can be most likely used to set an already precalculated path.

So the group operation is this:

1. Calculate one squad wide path plan at the start of the round from the current spawn to a number of objectives and finally to the exfil. This will be main strategic path.
2. Tactically we'll then just make sure that the bots follow this path and regroup at the nearest waypoint after interruptions.



