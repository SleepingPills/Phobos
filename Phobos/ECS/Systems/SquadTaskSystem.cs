using System.Collections.Generic;
using Phobos.Diag;
using Phobos.ECS.Entities;
using Phobos.Objectives;

namespace Phobos.ECS.Systems;

public class SquadTaskSystem(ActorTaskSystem actorTaskSystem, ObjectiveQueue objectiveQueue)
{
    public void Update(List<Squad> squads)
    {
        for (var i = 0; i < squads.Count; i++)
        {
            var squad = squads[i];
            
            // If the squad has an objective, make sure all the members get it
            if (!squad.Task.HasObjective)
            {
                // If the squad does not have an objective yet, grab one.
                var objective = objectiveQueue.Next();
                squad.Task.Assign(objective);
                DebugLog.Write($"Assigned {objective} to {squad}");
            }

            for (var j = 0; j < squad.Members.Count; j++)
            {
                var member = squad.Members[j];
                if (member.Task.HasObjective)
                    continue;
                    
                actorTaskSystem.AssignObjective(member, squad.Task.Objective);
            }
        }
    }
}