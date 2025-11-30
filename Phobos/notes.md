# TODO:
* Add a mechanism to the objective system to resubmit the objective if the actor is reactivated
* Add movement and stance status tracking to a component, so we keep track of stuff like the path angle jitter, etc.
* Add pathing status tracking to the movement system, so we keep track of where we are on the path
* Add stamina system to the movement and sprinting
* Add simplistic door handling where the bot auto-opens nearby doors and stops sprinting if there's a door within 5 meters

# High Level Plan
1. First PoC with basic squad level goal planning
2. Holistic strategic squad AI using GOAP for immersive bot behavior (buzzword bingo)

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

# Notes
## Doors
* Initial door logic will be to put all doors in a grid, we then look up nearby doors on every frame and doors which are closer than 3m we open
* We also slow down movement whenever in 5m vicinity of doors to avoid glitching into them.

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
### New squad objective
1. Assign new target to the routing
2. Submit to the PathfinderSystem
3. Once finished the PathfinderSystem pushes back to the 
### Bot was sidetracked
1. Submit path for recalculation with unchanged target
2. Notify bot that the path changed and the moving logic needs to be run
