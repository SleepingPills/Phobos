using System.Collections.Generic;
using Phobos.Diag;
using Phobos.ECS.Entities;
using Phobos.ECS.Systems;
using Phobos.Navigation;
using Phobos.Objectives;

namespace Phobos.ECS;

// ReSharper disable MemberCanBePrivate.Global
// Every AI project needs a shitty, llm generated component name like "orcehstrator".
public class SystemOrchestrator : IActorSystem
{
    public readonly MovementSystem MovementSystem;
    public readonly ActorTaskSystem ActorTaskSystem;
    public readonly SquadOrchestrator SquadOrchestrator;
    private readonly List<IActorSystem> _systems;

    public SystemOrchestrator(NavJobExecutor navJobExecutor, ObjectiveQueue objectiveQueue)
    {
        DebugLog.Write("Creating MovementSystem");
        MovementSystem = new MovementSystem(navJobExecutor);
        DebugLog.Write("Creating ActorTaskSystem");
        ActorTaskSystem = new ActorTaskSystem(MovementSystem);
        DebugLog.Write("Creating SquadOrchestrator");
        SquadOrchestrator = new SquadOrchestrator(ActorTaskSystem, objectiveQueue);

        _systems = [MovementSystem, ActorTaskSystem, SquadOrchestrator];
    }

    public void AddActor(Actor actor)
    {
        DebugLog.Write($"Adding {actor} to Phobos systems");
        for (var i = 0; i < _systems.Count; i++)
        {
            _systems[i].AddActor(actor);
        }
    }

    public void RemoveActor(Actor actor)
    {
        DebugLog.Write($"Removing {actor} from Phobos systems");
        for (var i = 0; i < _systems.Count; i++)
        {
            _systems[i].RemoveActor(actor);
        }
    }

    public void Update()
    {
        SquadOrchestrator.Update();
        ActorTaskSystem.Update();
        MovementSystem.Update();
    }
}