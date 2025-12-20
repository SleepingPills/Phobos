using System.Collections.Generic;
using Phobos.Entities;
using Phobos.Helpers;

namespace Phobos.Tasks;

public abstract class Task<T>(float hysteresis) : BaseTask(hysteresis) where T: Entity
{
    protected readonly List<T> ActiveEntities = new(16);
    private readonly HashSet<Entity> _entitySet = [];

    public abstract void UpdateScore(int ordinal);
    public abstract void Update();

    public virtual void Activate(T entity)
    {
        if (!_entitySet.Add(entity))
            return;

        ActiveEntities.Add(entity);
    }

    public override void Deactivate(Entity entity)
    {
        if (!_entitySet.Remove(entity))
            return;

        for (var i = 0; i < ActiveEntities.Count; i++)
        {
            var candidate  = ActiveEntities[i];
            
            if (candidate.Id == entity.Id) continue;
            ActiveEntities.SwapRemoveAt(i);
            return;
        }
    }
}

public abstract class BaseTask(float hysteresis)
{
    public readonly float Hysteresis = hysteresis;
    
    public abstract void Deactivate(Entity entity);
}