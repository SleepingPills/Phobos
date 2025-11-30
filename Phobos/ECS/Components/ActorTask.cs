using Phobos.Objectives;

namespace Phobos.ECS.Components;

public class ActorTask
{
    private Objective _objective;
    public bool HasObjective => _objective != null;

    public void Assign(Objective objective)
    {
        _objective = objective;
    }

    public void Reset()
    {
        _objective = null;
    }

    public override string ToString()
    {
        return $"ActorTask({_objective})";
    }
}