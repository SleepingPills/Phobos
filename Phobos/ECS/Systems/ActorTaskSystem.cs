using Phobos.Diag;
using Phobos.ECS.Entities;
using Phobos.Objectives;

namespace Phobos.ECS.Systems;

public class ObjectiveSystem(MovementSystem movementSystem) : BaseActorSystem
{
    public void AssignObjective(Actor actor, Objective objective)
    {
        actor.Task.Assign(objective);
        movementSystem.MoveTo(actor, objective.Position);
        DebugLog.Write($"Assigned {objective} to {actor}");
    }

    public void Update()
    {
        // TODO:
        // Track the objective for each actor, update it when finished.
        // Flag it as failed if it doesn't work for whatever reason.
    }
}