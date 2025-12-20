namespace Phobos.Tasks;

public readonly struct TaskAssignment(BaseTask task, int ordinal)
{
    public readonly BaseTask Task = task;
    public readonly int Ordinal = ordinal;
}