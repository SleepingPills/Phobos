using EFT;
using Phobos.Entities;

namespace Phobos.Data;

public class AgentArray(int capacity = 32) : EntityArray<Agent>(capacity)
{
    public Agent Add(BotOwner bot, int taskCount)
    {
        var id = Reserve();
        var agent = new Agent(id, bot, new float[taskCount]);
        Values.Add(agent);
        return agent;
    }
}