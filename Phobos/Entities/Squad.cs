using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Phobos.Helpers;
using Phobos.Tasks.Strategies;

namespace Phobos.Entities;


public class Squad(int id, float[] taskScores) : Entity(id, taskScores)
{
    public readonly List<Agent> Members = new(6);
    
    public int Size {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Members.Count;
    }

    public void AddAgent(Agent member)
    {
        Members.Add(member);
    }

    public void RemoveAgent(Agent member)
    {
        Members.SwapRemove(member);
    }
    
    public override string ToString()
    {
        return $"Squad(id: {Id})";
    }
}