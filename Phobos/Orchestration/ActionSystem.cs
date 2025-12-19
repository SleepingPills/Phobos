using System.Collections.Generic;
using Comfort.Common;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using BaseAction = Phobos.Tasks.Actions.BaseAction;

namespace Phobos.Orchestration;

public class ActionSystem(AgentData dataset)
{
    private readonly List<BaseAction> _actions = [];
    
    // TODO Add multiple action lists according to different utility update frequencies: realtime, high (0.1s), low (1s)
    //      Not every action needs their utility updated every frame. Basically only combat will run real time.
    
    public void RemoveAgent(Agent agent)
    {
        DebugLog.Write($"Removing {agent} from all actions");
        for (var i = 0; i < _actions.Count; i++)
        {
            var action = _actions[i];
            action.Deactivate(agent);
        }
    }

    public void RegisterAction(BaseAction action)
    {
        _actions.Add(action);
    }
    
    public void Update()
    {
        // Update utilities
        for (var i = 0; i < _actions.Count; i++)
        {
            _actions[i].UpdateUtility();
        }

        var agents = dataset.Entities.Values;
        
        for (var i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];

            if (!agent.IsActive)
            {
                if (agent.CurrentAction != null)
                {
                    agent.CurrentAction.Deactivate(agent);
                    agent.CurrentAction = null;
                }
                
                continue;
            }

            var highestScore = -1f;
            BaseAction nextAction = null;
            
            for (var j = 0; j < agent.Actions.Count; j++)
            {
                var entry = agent.Actions[j];
                var score = entry.Score + entry.Action.Hysteresis;
                
                if (score <= highestScore) continue;
                
                highestScore = score;
                nextAction = entry.Action;
            }
            
            Singleton<Telemetry>.Instance.UpdateScores(agent);
            
            agent.Actions.Clear();

            if (agent.CurrentAction == nextAction || nextAction == null) continue;
            
            agent.CurrentAction?.Deactivate(agent);
            nextAction.Activate(agent);
            agent.CurrentAction = nextAction;
        }

        // Run the action logic
        for (var i = 0; i < _actions.Count; i++)
        {
            var action = _actions[i];
            action.Update();
        }
    }
}