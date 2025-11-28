using System.Collections.Generic;
using Phobos.Extensions;

namespace Phobos.ECS;


public class BasicSystem : IPhobosSystem
{
    protected readonly List<Actor> Actors = [];

    public void AddActor(Actor actor)
    {
        Actors.Add(actor);
    }

    public void RemoveActor(Actor actor)
    {
        Actors.RemoveSwap(actor);
    }
}