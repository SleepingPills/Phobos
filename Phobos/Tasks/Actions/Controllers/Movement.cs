using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Phobos.Entities;

namespace Phobos.Tasks.Actions.Controllers;

public class MovementController
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(List<Agent> agents)
    {
        
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Activate(Agent agent)
    {
        
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deactivate(Agent agent)
    {
        agent.Movement.CurrentJob = null;
    }
}