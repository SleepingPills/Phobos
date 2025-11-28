using System.Collections.Generic;

namespace Phobos.ECS;

// Operationalizes and orchestrates organizational synergies 
public class Orchestrator
{
    private List<IPhobosSystem> _systems;

    public void Update()
    {
        
    }

    public void RemoveActor(Actor actor)
    {
        for (var i = 0; i < _systems.Count; i++)
        {
            _systems[i].RemoveActor(actor);
        }
    }
}