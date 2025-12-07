using System;
using System.Collections.Generic;
using Phobos.Diag;
using Phobos.ECS.Components;
using Phobos.ECS.Entities;
using Phobos.Navigation;

namespace Phobos.ECS.Systems;

public class SquadTaskSystem(ObjectiveSystem objectiveSystem, LocationQueue locationQueue)
{
    public void Update(List<Squad> squads)
    {
        for (var i = 0; i < squads.Count; i++)
        {
            var squad = squads[i];
            
            if (squad.ObjectiveLocation == null)
            {
                // If the squad does not have an objective yet, grab one.
                var location = locationQueue.Next();
                squad.ObjectiveLocation = location;
                DebugLog.Write($"Assigned {location} to {squad}");
            }

            var finishedCount = 0;
            
            // If the squad has an objective, make sure all the members get it
            for (var j = 0; j < squad.Members.Count; j++)
            {
                var member = squad.Members[j];
                
                if (!member.IsActive)
                    continue;

                switch (member.Objective.Status)
                {
                    case ObjectiveStatus.Suspended:
                        objectiveSystem.BeginObjective(member, squad.ObjectiveLocation);
                        break;
                    case ObjectiveStatus.Active:
                        break;
                    case ObjectiveStatus.Completed:
                    case ObjectiveStatus.Failed:
                        finishedCount++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // TODO: finish this
            // if (finishedCount == squad.Count)
            // {
            //     DebugLog.Write($"{squad} objective finished, assigning new objective.");
            //     squad.ObjectiveLocation = null;
            // }
            
            // Squad members who are too far ahead will get slowed down
        }
    }
}