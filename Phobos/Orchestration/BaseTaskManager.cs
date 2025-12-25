using System.Runtime.CompilerServices;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Tasks;

namespace Phobos.Orchestration;

public class BaseTaskManager<TEntity>(Task<TEntity>[] tasks) where TEntity : Entity
{
    public readonly Task<TEntity>[] Tasks = tasks;
    
    public void RemoveEntity(TEntity entity)
    {
        DebugLog.Write($"Removing {entity} from {this}");
        entity.TaskAssignment.Task?.Deactivate(entity);
        entity.TaskAssignment = new TaskAssignment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void UpdateScores()
    {
        for (var i = 0; i < Tasks.Length; i++)
        {
            Tasks[i].UpdateScore(i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void UpdateTasks()
    {
        for (var i = 0; i < Tasks.Length; i++)
        {
            var action = Tasks[i];
            action.Update();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void PickTask(TEntity entity)
    {
        var assignment = entity.TaskAssignment;

        var highestScore = 0f;
        var nextTaskOrdinal = 0;

        // Seed the next task values from the current task - including hysteresis
        if (assignment.Task != null)
        {
            nextTaskOrdinal = assignment.Ordinal;
            highestScore = entity.TaskScores[assignment.Ordinal] + assignment.Task.Hysteresis;
        }

        Task<TEntity> nextTask = null;

        for (var j = 0; j < Tasks.Length; j++)
        {
            var task = Tasks[j];
            var score = entity.TaskScores[j];

            if (score <= highestScore) continue;

            highestScore = score;
            nextTaskOrdinal = j;
            nextTask = task;
        }

        // Don't need to check whether the next task is the current task, because in that case nextTask will be null
        if (nextTask == null) return;
        
        DebugLog.Write($"{entity} changing task from {assignment.Task} to {nextTask} with utility {highestScore}");

        assignment.Task?.Deactivate(entity);
        nextTask.Activate(entity);

        entity.TaskAssignment = new TaskAssignment(nextTask, nextTaskOrdinal);
    }

    public override string ToString()
    {
        return GetType().Name;
    }
}