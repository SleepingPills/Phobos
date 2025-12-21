using System;
using System.Collections.Generic;
using EFT;
using Phobos.Components;
using Phobos.Entities;

namespace Phobos.Data;

public class Dataset<T, TE>(TE entities) where TE : EntityArray<T> where T : Entity
{
    public readonly TE Entities =  entities;
    
    private readonly List<IComponentArray> _components = [];
    private readonly Dictionary<Type, IComponentArray> _componentsTypeMap = new();

    protected void AddEntityComponents(T entity)
    {
        for (var i = 0; i < _components.Count; i++)
        {
            var component = _components[i];
            component.Add(entity.Id);
        }
    }
    
    public void RemoveEntity(T entity)
    {
        Entities.Remove(entity);
        
        for (var i = 0; i < _components.Count; i++)
        {
            var component = _components[i];
            component.Remove(entity.Id);
        }
    }
    
    public void RegisterComponent(IComponentArray componentArray)
    {
        _componentsTypeMap.Add(componentArray.GetType(), componentArray);
        _components.Add(componentArray);
    }
    
    public ComponentArray<TC> GetComponentArray<TC>() where TC : class, new()
    {
        return (ComponentArray<TC>)_componentsTypeMap[typeof(ComponentArray<TC>)];
    }
}

public class AgentData() : Dataset<Agent, AgentArray>(new AgentArray())
{
    public Agent AddEntity(BotOwner bot, int taskCount)
    {
        var agent = Entities.Add(bot, taskCount);
        
        AddEntityComponents(agent);
                
        return agent;
    }
}

public class SquadData() : Dataset<Squad, SquadArray>(new SquadArray())
{
    public Squad AddEntity(int taskCount)
    {
        var squad = Entities.Add(taskCount);
        
        AddEntityComponents(squad);
                
        return squad;
    }
}