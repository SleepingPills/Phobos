using EFT;
using Phobos.Entities;

namespace Phobos.Data;

public class AgentArray(int capacity = 32) : EntityArray<Agent>(capacity)
{
    public Agent Add(BotOwner bot)
    {
        var id = Reserve();
        var agent = new Agent(bot, id);
        Values.Add(agent);
        return agent;
    }
}