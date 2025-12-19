using Phobos.Components;
using Phobos.Data;
using Phobos.Entities;

namespace Phobos.Tasks.Actions;

public class GuardAction(AgentData dataset) : BaseAction(hysteresis: 0.05f)
{
    private readonly ComponentArray<Guard> _guardComponents = dataset.GetComponentArray<Guard>();

    public override void UpdateUtility()
    {
        /*
         * Objective proximity: 1 if near the objective, 0 otherwise
         * Squad cohesion: the less squad cohesion there is, the higher utility this objective gets.
         */
        var agents = dataset.Entities.Values;
        
        for (var i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            agent.Actions.Add(new ActionScore(0.5f, this));
        }
    }
    
    public override void Update()
    {
        for (var i = 0; i < ActiveAgents.Count; i++)
        {
            var agent = ActiveAgents[i];
            var guard = _guardComponents[agent.Id];
        }
    }
    
    public override void Activate(Agent agent)
    {
        base.Activate(agent);
        
        // DebugLog.Write($"Assigned {objective} to {agent}");
        //
        // var objective = agent.Task.GuardComponent;
        //
        // objective.Location = location;
        //
        // agent.Task.Current = objective;
        // agent.Movement.Speed = 1f;
        //
        // Activate(agent);
    }

    public override void Deactivate(Agent agent)
    {
        base.Deactivate(agent);
    }
}