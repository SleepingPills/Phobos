using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Phobos.ECS.Entities;

namespace Phobos.ECS.Systems;

public class BaseActorTaskSystem
{
    protected readonly ActorList Actors = new(16);
    private readonly HashSet<Actor> _actorSet = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void AddActor(Actor actor)
    {
        if (!_actorSet.Add(actor))
            return;

        Actors.Add(actor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void RemoveActor(Actor actor)
    {
        Actors.SwapRemove(actor);
        _actorSet.Remove(actor);
    }
}