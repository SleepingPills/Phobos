using System.Collections.Generic;
using Phobos.ECS.Entities;
using Phobos.ECS.Systems;
using Phobos.Navigation;
using Phobos.Objectives;

namespace Phobos.ECS;

// ReSharper disable MemberCanBePrivate.Global
public class SystemOrchestrator : IActorSystem
{
    public readonly MovementSystem MovementSystem;
    public readonly ObjectiveSystem ObjectiveSystem;
    public readonly SquadOrchestrator SquadOrchestrator;
    private readonly List<IActorSystem> _systems;

    public SystemOrchestrator(NavJobExecutor navJobExecutor, ObjectiveQueue objectiveQueue)
    {
        MovementSystem = new MovementSystem(navJobExecutor);
        ObjectiveSystem = new ObjectiveSystem(MovementSystem);
        SquadOrchestrator = new SquadOrchestrator(ObjectiveSystem, objectiveQueue);

        _systems = [MovementSystem, ObjectiveSystem, SquadOrchestrator];
    }

    public void AddActor(Actor actor)
    {
        for (var i = 0; i < _systems.Count; i++)
        {
            _systems[i].AddActor(actor);
        }
    }

    public void RemoveActor(Actor actor)
    {
        for (var i = 0; i < _systems.Count; i++)
        {
            _systems[i].RemoveActor(actor);
        }
    }

    public void Update()
    {
        SquadOrchestrator.Update();
        ObjectiveSystem.Update();
        MovementSystem.Update();
    }
}