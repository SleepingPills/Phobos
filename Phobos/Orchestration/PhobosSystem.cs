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

    private readonly AgentData _agentData;
    private readonly SquadData _squadData;
    
    private readonly ActionSystem _actionSystem;
    private readonly StrategySystem _strategySystem;

    private readonly Telemetry _telemetry;

    public PhobosSystem(Telemetry telemetry)
    {
        _agentData = new AgentData();
        _squadData = new SquadData();
        
        RegisterComponents();
        var actions = RegisterActions();
        var strategies = RegisterStrategies();
        
        _actionSystem = new ActionSystem(_agentData, actions);
        _strategySystem = new StrategySystem(_squadData, strategies);
        
        SquadRegistry =  new SquadRegistry(_squadData, _strategySystem, telemetry);
        
        _telemetry = telemetry;
    }
    
    public Agent AddAgent(BotOwner bot)
    {
        var agent = _agentData.AddEntity(bot, _actionSystem.TaskCount);
        SquadRegistry.AddAgent(agent);
        _telemetry.AddEntity(agent);
        return agent;
    }

    public void RemoveAgent(Agent agent)
    {
        _agentData.RemoveEntity(agent);
        SquadRegistry.RemoveAgent(agent);
        
        _actionSystem.RemoveEntity(agent);
        _telemetry.RemoveEntity(agent);
    }

    public void Update()
    {
        _actionSystem.Update();
        _strategySystem.Update();
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
            _agentData.RegisterComponent(value);
        }

        OnRegisterSquadComponents?.Invoke(squadComponentDefs);
        foreach (var value in squadComponentDefs.Values)
        {
            _squadData.RegisterComponent(value);
        }
    }
    
    private Task<Agent>[] RegisterActions()
    {
        var actions = new Registry<Task<Agent>>();
        
        actions.Add(new GotoObjectiveAction(_agentData, 0.25f));
        
        OnRegisterActions?.Invoke(actions);
        
        return actions.Values.ToArray();
    }

    
    private Task<Squad>[] RegisterStrategies()
    {
        var strategies = new Registry<Task<Squad>>();
        
        strategies.Add(new GotoObjectiveStrategy(_squadData, _agentData, new LocationQueue(), 0.25f));
        
        OnRegisterStrategies?.Invoke(strategies);
        
        return strategies.Values.ToArray();
    }
}