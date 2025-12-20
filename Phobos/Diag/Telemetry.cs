using System.Collections.Generic;
using System.Diagnostics;
using Phobos.Entities;
using Phobos.Tasks.Actions;
using Phobos.Tasks.Strategies;

namespace Phobos.Diag;

public class Telemetry
{
    // private readonly Dictionary<Agent, AgentTelemetry> _agentTelemetry = new();
    // private readonly Dictionary<Squad, SquadTelemetry> _squadTelemetry = new();

    public string GenerateUtilityReport()
    {
        return "";
    }

    public string GenerateAgentReport(Agent agent)
    {
        return "";
    }

    [Conditional("DEBUG")]
    public void UpdateScores(float[] scores)
    {
        // var telemetry = _agentTelemetry[agent];
        // telemetry.Scores.Clear();
        // telemetry.Scores.AddRange(agent.Actions);
    }
    
    [Conditional("DEBUG")]
    public void UpdateScores(Squad squad)
    {
        // var telemetry = _squadTelemetry[squad];
        // telemetry.Scores.Clear();
        // telemetry.Scores.AddRange(squad.Strategies);
    }

    [Conditional("DEBUG")]
    public void AddEntity(Agent agent)
    {
        DebugLog.Write($"Adding {agent} to Telemetry");
        // _agentTelemetry[agent] = new();
    }
    
    [Conditional("DEBUG")]
    public void AddEntity(Squad squad)
    {
        DebugLog.Write($"Adding {squad} to Telemetry");
        // _squadTelemetry[squad] = new();
    }

    [Conditional("DEBUG")]
    public void RemoveEntity(Agent agent)
    {
        DebugLog.Write($"Removing {agent} from Telemetry");
        // _agentTelemetry.Remove(agent);
    }
    
    [Conditional("DEBUG")]
    public void RemoveEntity(Squad squad)
    {
        DebugLog.Write($"Removing {squad} from Telemetry");
        // _squadTelemetry.Remove(squad);
    }
}