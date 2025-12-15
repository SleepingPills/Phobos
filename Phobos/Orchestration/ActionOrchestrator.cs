using System.Collections.Generic;
using Phobos.ECS.Entities;
using Phobos.Entities;
using BaseAction = Phobos.Actions.BaseAction;
using GuardAction = Phobos.Actions.GuardAction;
using ObjectiveAction = Phobos.ECS.Actions.ObjectiveAction;

namespace Phobos.ECS;

public class ActionOrchestrator
{
    private readonly List<Agent> _liveAgents;

    private readonly ObjectiveAction _objective;
    private readonly GuardAction _guard;
    private readonly List<BaseAction> _actions;
    
    public ActionOrchestrator(List<Agent> liveAgents)
    {
        _liveAgents = liveAgents;

        _objective = new ObjectiveAction();
        _guard = new GuardAction();
        _actions = [_objective, _guard];
    }

    public void RemoveAgent(Agent agent)
    {
        for (var i = 0; i < _actions.Count; i++)
        {
            var action = _actions[i];
            action.Deactivate(agent);
        }
    }
    
    public void Update()
    {
        // Go through each agent and update the utilities
        for (var i = 0; i < _liveAgents.Count; i++)
        {
            var agent = _liveAgents[i];
            
            if (!agent.IsActive)
            {
                agent.CurrentAction?.Deactivate(agent);
                continue;
            }
            
            UpdateActions(agent);
        }
        
        
        // Update the action executors
        _objective.Update();
        _guard.Update();
    }
}