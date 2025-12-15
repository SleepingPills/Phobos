using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Phobos.Diag;
using Phobos.ECS.Components;
using Phobos.ECS.Entities;
using Phobos.Helpers;
using Phobos.Navigation;

namespace Phobos.ECS.Systems;

public class SquadStrategySystem(LocationQueue locationQueue)
{
    private readonly TimePacing _pacing = new(1f);

    public void Update(List<Squad> squads)
    {
        if (_pacing.Blocked())
            return;

        for (var i = 0; i < squads.Count; i++)
        {
            var squad = squads[i];

            UpdateSquad(squad);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateSquad(Squad squad)
    {
        if (squad.Objective == null)
        {
            // If the squad does not have an objective yet, grab one.
            var location = locationQueue.Next();
            squad.Objective = location;
            DebugLog.Write($"Assigned {location} to {squad}");
        }

        var finishedCount = 0;

        for (var i = 0; i < squad.Members.Count; i++)
        {
            var member = squad.Members[i];

            if (member.Objective.Status is ObjectiveStatus.Success or ObjectiveStatus.Failed)
            {
                finishedCount++;
            }
        }

        if (finishedCount == squad.Count)
        {
            DebugLog.Write($"{squad} objective finished, resetting target locations.");
            squad.Objective = null;
        }
    }
}