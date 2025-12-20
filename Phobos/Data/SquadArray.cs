using Phobos.Entities;

namespace Phobos.Data;

public class SquadArray(int capacity=16) : EntityArray<Squad>(capacity)
{
    public Squad Add(int taskCount)
    {
        var id = Reserve();
        var squad = new Squad(id, new float[taskCount]);
        Values.Add(squad);
        return squad;
    }
}