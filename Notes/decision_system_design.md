# Overall architecture
We'll use Utility AI for both individual agents and squads. Squads could ostensibly use GOAP, but there's no real benefit for the complexity. Squads can always generate a primary strategic plan for the raid (e.g. go through a number of locations and then exfil at a particular place), and then assign a utility to it. 

* Squads will execute Strategies
* Agents will execute Actions

## Notes
* Tie Breaking:
  * If two or more actions have the same utility, switch to an *Undecided* action that makes the bot wait.
  * Alternatively, use an ex-ante priority defined for each action that we use as tie-breakers.

# Tactical Utility AI
## Actions
* Wait: fallback in case nothing else is triggering or there's a tie
* GotoObjective
* OpenDoor
* GotoObjectiveCareful: slows down and looks at stuff like doors

# Strategic Utility AI 
Squads will influence agent behavior by modifying specific components. E.g. the GotoObjectiveStrategy will pick an squad objective if there isn't one and set all agents' ObjectiveComponent to this.

# Systems
## Movement
!!! If we make this a Task subclass, the big benefit is that we can automatically handle wiping out the current movement target and nav job (if any) when the action gets deactivated.

* Nullable Target vector on the Movement component that contains the target destination.
* When the bot reaches the destination, we null out the target
* If the target is null, the movement bails out for the bot

Path Job Robustness:
* Stash away the most recently submitted path job on the agent
* When submitting a path job, if the current job on the agent is not null, we bail out (avoid submitting multiple jobs). This handles the issue where GotoObjective might keep submitting jobs over multiple frames.
* When handling the path job itself, check if it's the same instance as the one
stashed away on the agent, if it isn't (e.g. the action got deactivated and current job is null) we skip the job handling. This fixes the issue of a path job completing after deactivation.

Question: how do we efficiently match the movement target to objective target.

# Actions
## GotoObjective
### Utility
* If the agent has no objective selected, the action is not submitted to the agent at all.
* Otherwise the utility scales from 0.5 to 0.75, starting from being 100m away to 0m.
### Logic

# Strategies
## GotoObjective
### Utility


### Logic


TODO: Start fleshing out the actual logic