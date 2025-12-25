using System.Linq;
using System.Runtime.CompilerServices;
using EFT;
using Phobos.Components;
using Phobos.Components.Squad;
using Phobos.Data;
using Phobos.Entities;
using Phobos.Navigation;
using Phobos.Tasks;
using Phobos.Tasks.Actions;
using Phobos.Tasks.Strategies;

namespace Phobos.Orchestration;

public class PhobosManager
{
    public delegate void RegisterComponentsDelegate(DefinitionRegistry<IComponentArray> definitionRegistry);
    public delegate void RegisterActionsDelegate(DefinitionRegistry<Task<Agent>> actions);
    public delegate void RegisterStrategiesDelegate(DefinitionRegistry<Task<Squad>> strategies);
    
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
    
    public readonly ActionManager ActionManager;
    public readonly StrategyManager StrategyManager;
    
    public PhobosManager(NavJobExecutor navJobExecutor)
    {
        AgentData = new AgentData();
        SquadData = new SquadData();
        
        RegisterComponents();
        var actions = RegisterActions();
        var strategies = RegisterStrategies();
        
        ActionManager = new ActionManager(AgentData, actions);
        StrategyManager = new StrategyManager(SquadData, strategies);
        
        SquadRegistry = new SquadRegistry(SquadData, StrategyManager);
        }
    
    public Agent AddAgent(BotOwner bot)
    {
        var agent = AgentData.AddEntity(bot, ActionManager.Tasks.Length);
        SquadRegistry.AddAgent(agent);
        return agent;
    }

    public void RemoveAgent(Agent agent)
    {
        AgentData.RemoveEntity(agent);
        SquadRegistry.RemoveAgent(agent);
        
        ActionManager.RemoveEntity(agent);
    }

    public void Update()
    {
        ActionManager.Update();
        StrategyManager.Update();
    }
    
    private void RegisterComponents()
    {
        var agentComponentDefs = new DefinitionRegistry<IComponentArray>();
        var squadComponentDefs = new DefinitionRegistry<IComponentArray>();
        
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
        var actions = new DefinitionRegistry<Task<Agent>>();
        
        actions.Add(new GotoObjectiveAction(AgentData, 0.25f));
        
        OnRegisterActions?.Invoke(actions);
        
        return actions.Values.ToArray();
    }

    
    private Task<Squad>[] RegisterStrategies()
    {
        var strategies = new DefinitionRegistry<Task<Squad>>();
        
        strategies.Add(new GotoObjectiveStrategy(SquadData, AgentData, new LocationQueue(), 0.25f));
        
        OnRegisterStrategies?.Invoke(strategies);
        
        return strategies.Values.ToArray();
    }
}