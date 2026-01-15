using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EFT;
using Phobos.Entities;

namespace Phobos.Orchestration;

public class BsgBotRegistry
{
    private readonly List<Agent> _agents = [];
    
    public void AddAgent(Agent agent)
    {
        var bsgId = agent.Bot.Id;

        if (bsgId >= _agents.Count)
        {
            var padding = bsgId - _agents.Count + 1;

            for (var i = 0; i < padding; i++)
            {
                _agents.Add(null);
            }
        }
        
        _agents[bsgId] = agent;
    }

    public void RemoveAgent(Agent agent)
    {
        var bsgId = agent.Bot.Id;
        
        if (bsgId >= _agents.Count)
        {
            return;
        }
        
        _agents[bsgId] = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPhobosActive(BotOwner bot)
    {
        var bsgId = bot.Id;
        
        if (bot.Id >= _agents.Count)
        {
            return false;
        }
        
        var agent = _agents[bsgId];
        
        return agent != null && agent.IsActive;
    }
}