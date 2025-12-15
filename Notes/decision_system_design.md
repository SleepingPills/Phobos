!!! Utility mechanism 
Before running the action logic, each Action runs an UpdateUtility where it'll iterate through all the agents, calculate the utility for each and write the result into each agent's UtilityScores list. The orchestrator will then pick the action with the highest utility from this list for each agent, check if it's the same as the current action and if yes, deactivate the current action and activate the new one.

!!! But this is all underpinned by the agent array having also a dense array that we can iterate through   

# Overall architecture
Likely to be a hybrid of GOAP for strategic/squad decision making and Utility AI for individual bots.

* Squads will execute Strategies
* Agents will execute Actions

# Tactical Utility AI
Example would be a simple two function setup:

```csharp
float QuestUtility(...)
{
    if (IsNearObjective)
        return 0f
    // Sharply approach 1 as we near the objective
    return Mathf.Clamp01((CurrentDistance / TotalDistance) ^ 4);
}

float WaitUtility(...)
{
    // If objective is reached, this becomes important. Otherwise it'll trigger if nothing else is triggering.
    return HasAchievedQuestObjective && IsNearObjective ? 1f : 0.001f;
}
```

The bot will then organically pick the best action depending on the utility. If it's very close to the objective, it might want to finish that first
even though an ally is in danger. On the other hand, if it's too far away from the nearest ally, it might want to head back and hook up with that ally
first.

## Implementation

!!! We only need two objects for actions: the Component and the System. The System can calculate the utility for all the actors inside it in one batch
completely removing the need for virtual method calls across all the actors. The System can also handle all the Activation/Removal gubbins.  

Action
* Hysteresis
  * each action will have a threshold by which a new action must beat it's score to dislodge it as the active action.
* CalculateUtility()
  * Runtime: this curve can be used to apply a decay to final utility score as a function of running time. This ensures that a particular action is not picked too many times in a row.
* Activate()
  * Adds the agent to the system for executing this utility.
* Deactivate()
  * Removes the agent from the execution system

Agent
* List<Action> Actions
* Action Current

### Notes
* Tie Breaking:
  * If two or more actions have the same utility, switch to an *Undecided* action that makes the bot wait.
  * Alternatively, use an ex-ante priority defined for each action that we use as tie breakers.

### Actions
* Wait: fallback in case nothing else is triggering or there's a tie
* GotoObjective
* OpenDoor
* GotoObjectiveCareful: slows down and looks at stuff like doors 


### Execution Loop


# Strategic GOAP AI 
TODO
