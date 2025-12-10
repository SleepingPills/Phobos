namespace Phobos.ECS.Components.Objectives;

public enum ObjectiveStatus
{
    Suspended,
    Active,
    Success,
    Failed
}

public enum ObjectiveType
{
    Quest = 0,
    Guard
}

public abstract class Objective(ObjectiveType objectiveType)
{
    // Used for indexing into the array of systems to get this objective type
    public readonly int TypeId = (int) objectiveType;
}