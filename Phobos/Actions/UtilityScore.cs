namespace Phobos.Actions;

public struct UtilityScore(float score, BaseAction action)
{
    public readonly float Score = score;
    public readonly BaseAction Action = action;
}