using System;
using System.Collections.Generic;
using EFT;
using Phobos.Components;
using Phobos.Entities;
using Phobos.Tasks;
using Phobos.Tasks.Actions;

namespace Phobos.Data;

public class Dataset<T, TE>(TE entities, Task<T>[] tasks) where TE : EntityArray<T> where T : Entity
{
    public readonly TE Entities =  entities;
    public readonly Task<T>[] Tasks = tasks;
    
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

    public ComponentArray<TC> GetComponentArray<TC>() where TC : Component
    {
        return (ComponentArray<TC>)_componentsTypeMap[typeof(ComponentArray<TC>)];
    }
}

public class AgentData(Task<Agent>[] actions) : Dataset<Agent, AgentArray>(new AgentArray(), actions)
{
    public Agent AddEntity(BotOwner bot)
    {
        var agent = Entities.Add(bot, Tasks.Length);
        
        AddEntityComponents(agent);
                
        return agent;
    }
}

public class SquadData(Task<Squad>[] strategies) : Dataset<Squad, SquadArray>(new SquadArray(), strategies)
{
    public Squad AddEntity()
    {
        var squad = Entities.Add(Tasks.Length);
        
        AddEntityComponents(squad);
                
        return squad;
    }
}