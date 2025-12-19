using System.Collections.Generic;
using Comfort.Common;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Tasks.Strategies;

namespace Phobos.Orchestration;

public class StrategySystem(SquadData dataset)
{
    private readonly List<BaseStrategy> _strategies = [];
    
    public void RemoveSquad(Squad squad)
    {
        DebugLog.Write($"Removing {squad} from all strategies");
        for (var i = 0; i < _strategies.Count; i++)
        {
            var strategy = _strategies[i];
            strategy.Deactivate(squad);
        }
    }

    public void RegisterStrategy(BaseStrategy strategy)
    {
        _strategies.Add(strategy);
    }
    
    public void Update()
    {
        // Update utilities
        for (var i = 0; i < _strategies.Count; i++)
        {
            _strategies[i].UpdateUtility();
        }

        var squads = dataset.Entities.Values;
        
        for (var i = 0; i < squads.Count; i++)
        {
            var squad = squads[i];

            if (squad.Count == 0)
            {
                if (squad.CurrentStrategy != null)
                {
                    squad.CurrentStrategy.Deactivate(squad);
                    squad.CurrentStrategy = null;
                }
                
                continue;
            }

            var highestScore = -1f;
            BaseStrategy nextStrategy = null;
            
            for (var j = 0; j < squad.Strategies.Count; j++)
            {
                var entry = squad.Strategies[j];
                var score = entry.Score + entry.Strategy.Hysteresis;
                
                if (score <= highestScore) continue;
                
                highestScore = score;
                nextStrategy = entry.Strategy;
            }
            
            Singleton<Telemetry>.Instance.UpdateScores(squad);
            
            squad.Strategies.Clear();

            if (squad.CurrentStrategy == nextStrategy || nextStrategy == null) continue;
            
            squad.CurrentStrategy?.Deactivate(squad);
            nextStrategy.Activate(squad);
            squad.CurrentStrategy = nextStrategy;
        }

        // Run the strategy logic
        for (var i = 0; i < _strategies.Count; i++)
        {
            var strategy = _strategies[i];
            strategy.Update();
        }
    }
}