# TODO:
* Sprinting
  * Re-enable sprinting
  * Make sprinting a toggle on the move action, once the action finishes, we reset everything to default.
  * Add stamina system to the movement and sprinting
  * Add movement and stance status tracking to a component, so we keep track of stuff like the path angle jitter, etc.
* Implement GOAP

# Strategic Planning
* Implement a GOAP (or task queue) like setup at the squad level, not the actors as thought initially

The squad will get a strategic plan, and will try to complete it. Inside that plan, it'll direct the squad members based on the plan steps.
We'll have interrupts when a squad member gets into combat or spots an enemy for example, which will cause the plan to be changed. E.g. a squaddie
getting into combat will insert the Assist objective into the squad plan at the front, so it gets carried out before the rest of the strategic plan.
More importantly, this will operate at the squad level. The Assist objective will have a squad level system associated with it that will ensure that
all out-of-combat squaddies go to the bots who are in combat. Once this objective is finished, the squad as a whole will resume the rest of the strategic plan. 

This is where we can then integrate the combat oriented squad objectives inside a full GOAP framework, which will swap out the peace time squad logic
to completely different, squad oriented tactics and direct each squad member accordingly.

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
## Objectives, Actions and Systems
### Permanent Systems
MovementSystem and LookSystem are always on, as bots will almost always be on the move and looking at something.
** The LookSystem itself will have no timing built in, it will simply look forward at the actual path if there's no other current look target.**

### Actions
Actions will be temporary in nature, e.g. We'll have a CarefulMove action that when activated, will slow down the movement (if any is happening),
or LookAtAction, which will cause the bot to look at a specific point for a duration. Multiple actions can be happening at once.

### Objectives
Objectives are strategic goals. E.g. reach a location, guard an area, go to a teammate. Etc. There will only be none or one objective at a time.


## Doors
* Initial door logic will be to put all doors in a grid, we then look up nearby doors on every frame and doors which are closer than 3m we open
* We also slow down movement whenever in 5m vicinity of doors to avoid glitching into them.
