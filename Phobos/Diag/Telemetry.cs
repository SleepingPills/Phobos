using System.Diagnostics;
using System.Text;
using Phobos.Entities;
using Phobos.Orchestration;

namespace Phobos.Diag;

public class Telemetry(PhobosSystem phobos)
{
    [Conditional("DEBUG")]
    public void GenerateUtilityReport(Agent agent, StringBuilder sb)
    {
        var actions = phobos.ActionSystem.Tasks;
        
        for (var i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            var score = agent.TaskScores[i];
            var prefix = action == agent.TaskAssignment.Task ? "*" : "";
            sb.AppendLine($"{prefix}{action.GetType().Name}: {score:0.00}");
        }
    }
}