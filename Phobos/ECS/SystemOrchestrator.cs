using Phobos.Diag;
using Phobos.ECS.Entities;
using Phobos.ECS.Systems;
using Phobos.Navigation;

namespace Phobos.ECS;

// ReSharper disable MemberCanBePrivate.Global
// Every AI project needs a shitty, llm generated component name like "orchestrator".
public class SystemOrchestrator
{
    public readonly MovementSystem MovementSystem;
    public readonly ObjectiveSystem ObjectiveSystem;
    public readonly SquadOrchestrator SquadOrchestrator;
    public readonly ActorList LiveActors;

    public SystemOrchestrator(NavJobExecutor navJobExecutor, LocationQueue locationQueue)
    {
        LiveActors = new ActorList(32);
        
        DebugLog.Write("Creating MovementSystem");
        MovementSystem = new MovementSystem(navJobExecutor, LiveActors);
        DebugLog.Write("Creating ActorTaskSystem");
        ObjectiveSystem = new ObjectiveSystem(MovementSystem);
        DebugLog.Write("Creating SquadOrchestrator");
        SquadOrchestrator = new SquadOrchestrator(ObjectiveSystem, locationQueue);
    }

    public void AddActor(Actor actor)
    {
        DebugLog.Write($"Adding {actor} to Phobos systems");
        LiveActors.Add(actor);
        SquadOrchestrator.AddActor(actor);
    }

    public void RemoveActor(Actor actor)
    {
        LiveActors.SwapRemove(actor);
        SquadOrchestrator.RemoveActor(actor);
    }

    public void Update()
    {
        SquadOrchestrator.Update();
        ObjectiveSystem.Update();
        MovementSystem.Update();
    }
}