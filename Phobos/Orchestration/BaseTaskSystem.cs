using System.Runtime.CompilerServices;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Tasks;

namespace Phobos.Orchestration;

public class BaseTaskSystem<TEntity>(Task<TEntity>[] tasks) where TEntity : Entity
{
    public void RemoveEntity(TEntity entity)
    {
        DebugLog.Write($"Removing {entity} from all actions");
        for (var i = 0; i < tasks.Length; i++)
        {
            var task = tasks[i];
            task.Deactivate(entity);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void UpdateScores()
    {
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i].UpdateScore(i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void UpdateTasks()
    {
        for (var i = 0; i < tasks.Length; i++)
        {
            var action = tasks[i];
            action.Update();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void PickTask(TEntity entity)
    {
        var assignment = entity.TaskAssignment;

        var highestScore = -1f;
        var nextTaskOrdinal = 0;

        // Seed the next task values from the current task - including hysteresis
        if (assignment.Task != null)
        {
            nextTaskOrdinal = assignment.Ordinal;
            highestScore = entity.TaskScores[assignment.Ordinal] + assignment.Task.Hysteresis;
        }

        Task<TEntity> nextTask = null;

        for (var j = 0; j < tasks.Length; j++)
        {
            var task = tasks[j];
            var score = entity.TaskScores[j];

            if (score <= highestScore) continue;

            highestScore = score;
            nextTaskOrdinal = j;
            nextTask = task;
        }

        // Don't need to check whether the next task is the current task, because in that case nextTask will be null
        if (nextTask == null) return;

        assignment.Task?.Deactivate(entity);
        nextTask.Activate(entity);

        entity.TaskAssignment = new TaskAssignment(nextTask, nextTaskOrdinal);
    }
}