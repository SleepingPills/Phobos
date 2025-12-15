using System;
using System.Collections.Generic;
using EFT;
using Phobos.Components;
using Phobos.Entities;

namespace Phobos.Data;

public class Dataset
{
    public readonly AgentArray Agents = new();
    private readonly List<IComponentArray> _components = [];
    private readonly Dictionary<Type, IComponentArray> _componentsTypeMap = new();

    public void AddAgent(BotOwner bot)
    {
        var agent = Agents.Add(bot);
        
        for (var i = 0; i < _components.Count; i++)
        {
            var component = _components[i];
            var item = component.Add(agent.Id);
            agent.Components.Add(item);
        }
    }
    
    public void RemoveAgent(Agent agent)
    {
        Agents.Remove(agent);
        
        for (var i = 0; i < _components.Count; i++)
        {
            var component = _components[i];
            component.Remove(agent.Id);
        }
    }
    
    public void RegisterComponent(IComponentArray componentArray)
    {
        _componentsTypeMap.Add(componentArray.GetType(), componentArray);
        _components.Add(componentArray);
    }

    public ComponentArray<T> GetComponentArray<T>() where T : class, IComponent
    {
        return (ComponentArray<T>)_componentsTypeMap[typeof(T)];
    }
}