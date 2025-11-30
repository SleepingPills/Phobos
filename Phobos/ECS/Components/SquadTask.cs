using Phobos.Objectives;

namespace Phobos.ECS.Components;

public class SquadTask
{
    public Objective Objective;
    public bool HasObjective => Objective != null;

    public void Assign(Objective objective)
    {
        Objective = objective;
    }

    public void Reset()
    {
        Objective = null;
    }

    public override string ToString()
    {
        return $"SquadTask({Objective})";
    }
}