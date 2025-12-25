using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Comfort.Common;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Helpers;
using Phobos.Tasks;
using Phobos.Tasks.Strategies;

namespace Phobos.Orchestration;

public class StrategyManager(SquadData dataset, Task<Squad>[] tasks) : BaseTaskManager<Squad>(tasks)
{
    private readonly TimePacing _pacing = new(0.5f);
    
    public void Update()
    {
        if (_pacing.Blocked())
            return;
        
        UpdateScores();
        PickTasks();
        UpdateTasks();
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PickTasks()
    {
        var squads = dataset.Entities.Values;
        
        for (var i = 0; i < squads.Count; i++)
        {
            var squad = squads[i];
            var assignment = squad.TaskAssignment;

            if (squad.Size == 0)
            {
                if (assignment.Task != null)
                {
                    assignment.Task.Deactivate(squad);
                    squad.TaskAssignment = new TaskAssignment();
                }

                continue;
            }

            PickTask(squad);
        }
    }
}