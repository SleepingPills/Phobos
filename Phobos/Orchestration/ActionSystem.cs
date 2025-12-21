using System.Runtime.CompilerServices;
using Phobos.Data;
using Phobos.Entities;
using Phobos.Tasks;

namespace Phobos.Orchestration;

public class ActionSystem(AgentData dataset, Task<Agent>[] tasks) : TaskSystem<Agent>(tasks)
{
    public void Update()
    {
        UpdateScores();
        PickTasks();
        UpdateTasks();
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PickTasks()
    {
        var agents = dataset.Entities.Values;
        
        for (var i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            var assignment = agent.TaskAssignment;

            if (!agent.IsActive)
            {
                if (assignment.Task != null)
                {
                    assignment.Task.Deactivate(agent);
                    agent.TaskAssignment = new TaskAssignment();
                }

                continue;
            }

            PickTask(agent);
        }
    }
}