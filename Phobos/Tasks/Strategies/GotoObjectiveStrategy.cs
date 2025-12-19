using Phobos.Components.Squad;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Navigation;

namespace Phobos.Tasks.Strategies;

public class GotoObjectiveStrategy(SquadData dataset, LocationQueue locationQueue, float hysteresis) : BaseStrategy(hysteresis)
{
    private readonly ComponentArray<SquadObjective> _objectiveComponents = dataset.GetComponentArray<SquadObjective>();
    
    public override void UpdateUtility()
    {
        var squads = dataset.Entities.Values;
        for (var i = 0; i < squads.Count; i++)
        {
            var squad = squads[i];
            squad.Strategies.Add(new StrategyScore(0.5f, this));
        }
    }

    public override void Update()
    {
        var squads = dataset.Entities.Values;
        
        for (var i = 0; i < squads.Count; i++)
        {
            var squad = squads[i];
            
            var objective = _objectiveComponents[squad.Id];

            if (objective.Location != null) continue;
            
            objective.Location = locationQueue.Next();
            DebugLog.Write($"Assigned {objective.Location} to {squad}");
        }
    }
}