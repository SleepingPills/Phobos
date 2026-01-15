using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using Phobos.Config;
using Phobos.Data;
using Phobos.Entities;
using Phobos.Navigation;
using Phobos.Systems;
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

    public readonly string MapId;
    public readonly PhobosConfig Config;

    public readonly AgentData AgentData;
    public readonly SquadData SquadData;

    public readonly NavJobExecutor NavJobExecutor;
    
    public readonly MovementSystem MovementSystem;
    public readonly LookSystem LookSystem;
    public readonly AssignmentSystem AssignmentSystem;
    public readonly DoorSystem DoorSystem;

    public readonly ActionManager ActionManager;
    public readonly StrategyManager StrategyManager;
    
    public readonly SquadRegistry SquadRegistry;

    private readonly BsgBotRegistry _bsgBotRegistry;
    private readonly List<Agent> _liveAgents;

    public PhobosManager(BotsController botsController, BsgBotRegistry bsgBotRegistry)
    {
        var gameWorld = Singleton<GameWorld>.Instance;

        MapId = gameWorld.LocationId;
        Config = new PhobosConfig();
        
        List<Player> humanPlayers = [];

        var allPlayers = gameWorld.AllAlivePlayersList;

        // ReSharper disable once LoopCanBeConvertedToQuery
        for (var i = 0; i < allPlayers.Count; i++)
        {
            var player = allPlayers[i];
            if (player != null && !player.AIData.IsAI)
            {
                humanPlayers.Add(player);
            }
        }

        AgentData = new AgentData();
        SquadData = new SquadData();

        _liveAgents = AgentData.Entities.Values;

        NavJobExecutor = new NavJobExecutor();
        
        MovementSystem = new MovementSystem(NavJobExecutor, humanPlayers);
        LookSystem = new LookSystem();
        AssignmentSystem = new AssignmentSystem(MapId, Config, botsController);
        DoorSystem = new  DoorSystem();
        
        RegisterComponents();
        var actions = RegisterActions();
        var strategies = RegisterStrategies();

        ActionManager = new ActionManager(AgentData, actions);
        StrategyManager = new StrategyManager(SquadData, strategies);

        SquadRegistry = new SquadRegistry(SquadData, StrategyManager);
        _bsgBotRegistry = bsgBotRegistry;
    }
    
    public Agent AddAgent(BotOwner bot)
    {
        var agent = AgentData.AddEntity(bot, ActionManager.Tasks.Length);
        SquadRegistry.AddAgent(agent);
        _bsgBotRegistry.AddAgent(agent);
        return agent;
    }

    public void RemoveAgent(Agent agent)
    {
        AgentData.RemoveEntity(agent);
        SquadRegistry.RemoveAgent(agent);
        ActionManager.RemoveEntity(agent);
        _bsgBotRegistry.RemoveAgent(agent);
    }

    public void Update()
    {
        StrategyManager.Update();
        ActionManager.Update();
        MovementSystem.Update(_liveAgents);
        LookSystem.Update(_liveAgents);
        
        NavJobExecutor.Update();
    }

    private void RegisterComponents()
    {
        var agentComponentDefs = new DefinitionRegistry<IComponentArray>();
        var squadComponentDefs = new DefinitionRegistry<IComponentArray>();

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

        actions.Add(new GotoObjectiveAction(AgentData, MovementSystem,0.25f));

        OnRegisterActions?.Invoke(actions);

        return actions.Values.ToArray();
    }


    private Task<Squad>[] RegisterStrategies()
    {
        var strategies = new DefinitionRegistry<Task<Squad>>();
        
        strategies.Add(new GotoObjectiveStrategy(SquadData, AssignmentSystem, 0.25f));

        OnRegisterStrategies?.Invoke(strategies);

        return strategies.Values.ToArray();
    }
}