using System.Linq;
using System.Runtime.CompilerServices;
using EFT;
using Phobos.Components;
using Phobos.Components.Squad;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Navigation;
using Phobos.Tasks;
using Phobos.Tasks.Actions;
using Phobos.Tasks.Strategies;

namespace Phobos.Orchestration;

public class PhobosSystem
{
    public delegate void RegisterComponentsDelegate(Registry<IComponentArray> registry);
    public delegate void RegisterActionsDelegate(Registry<Task<Agent>> actions);
    public delegate void RegisterStrategiesDelegate(Registry<Task<Squad>> strategies);
    
    public static RegisterComponentsDelegate OnRegisterAgentComponents;
    public static RegisterComponentsDelegate OnRegisterSquadComponents;
    
    public static RegisterActionsDelegate OnRegisterActions;
    public static RegisterStrategiesDelegate OnRegisterStrategies;

    public SquadRegistry SquadRegistry
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    public readonly AgentData AgentData;
    public readonly SquadData SquadData;
    
    public readonly ActionSystem ActionSystem;
    public readonly StrategySystem StrategySystem;

    public PhobosSystem()
    {
        AgentData = new AgentData();
        SquadData = new SquadData();
        
        RegisterComponents();
        var actions = RegisterActions();
        var strategies = RegisterStrategies();
        
        ActionSystem = new ActionSystem(AgentData, actions);
        StrategySystem = new StrategySystem(SquadData, strategies);
        
        SquadRegistry =  new SquadRegistry(SquadData, StrategySystem);
    }
    
    public Agent AddAgent(BotOwner bot)
    {
        var agent = AgentData.AddEntity(bot, ActionSystem.Tasks.Length);
        SquadRegistry.AddAgent(agent);
        return agent;
    }

    public void RemoveAgent(Agent agent)
    {
        AgentData.RemoveEntity(agent);
        SquadRegistry.RemoveAgent(agent);
        
        ActionSystem.RemoveEntity(agent);
    }

    public void Update()
    {
        ActionSystem.Update();
        StrategySystem.Update();
    }
    
    private void RegisterComponents()
    {
        var agentComponentDefs = new Registry<IComponentArray>();
        var squadComponentDefs = new Registry<IComponentArray>();
        
        agentComponentDefs.Add(new ComponentArray<Objective>());
        squadComponentDefs.Add(new ComponentArray<SquadObjective>());
        
        OnRegisterAgentComponents?.Invoke(agentComponentDefs);
        foreach (var value in agentComponentDefs.Values)
        {
            AgentData.RegisterComponent(value);
        }

        OnRegisterSquadComponents?.Invoke(squadComponentDefs);
        foreach (var value in squadComponentDefs.Values)
        {
            SquadData.RegisterComponent(value);
        }
    }
    
    private Task<Agent>[] RegisterActions()
    {
        var actions = new Registry<Task<Agent>>();
        
        actions.Add(new GotoObjectiveAction(AgentData, 0.25f));
        
        OnRegisterActions?.Invoke(actions);
        
        return actions.Values.ToArray();
    }

    
    private Task<Squad>[] RegisterStrategies()
    {
        var strategies = new Registry<Task<Squad>>();
        
        strategies.Add(new GotoObjectiveStrategy(SquadData, AgentData, new LocationQueue(), 0.25f));
        
        OnRegisterStrategies?.Invoke(strategies);
        
        return strategies.Values.ToArray();
    }
}