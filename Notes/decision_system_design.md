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