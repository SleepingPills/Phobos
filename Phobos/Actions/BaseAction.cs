using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Phobos.Data;
using Phobos.Entities;
using Phobos.Helpers;

namespace Phobos.Actions;

public abstract class BaseAction(Dataset dataset, float hysteresis)
{
    public readonly float Hysteresis = hysteresis;
    
    protected readonly List<Agent> ActiveAgents = new(16);
    private readonly HashSet<Agent> _agentSet = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Activate(Agent agent)
    {
        if (!_agentSet.Add(agent))
            return;

        ActiveAgents.Add(agent);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Deactivate(Agent agent)
    {
        if (!_agentSet.Remove(agent))
            return;

        ActiveAgents.SwapRemove(agent);
    }
}