using DrakiaXYZ.BigBrain.Brains;
using EFT;
using Phobos.Components.Squad;
using Phobos.Data;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Navigation;
using Phobos.Tasks.Strategies;

namespace Phobos.Orchestration;

public class PhobosSystem
{
    public readonly AgentData AgentData;
    public readonly SquadData SquadData;
    
    public readonly ActionSystem ActionSystem;
    public readonly StrategySystem StrategySystem;
    
    public readonly SquadSystem SquadSystem;

    public readonly LocationQueue LocationQueue;
    
    private readonly Telemetry _telemetry;

    public PhobosSystem(Telemetry telemetry)
    {
        LocationQueue = new LocationQueue();
        
        AgentData = new AgentData();
        SquadData = new SquadData();
        
        ActionSystem = new ActionSystem(AgentData);
        StrategySystem = new StrategySystem(SquadData);
        
        SquadSystem =  new SquadSystem(SquadData, StrategySystem, telemetry);
        
        _telemetry = telemetry;
    }

    public void RegisterComponents()
    {
        // Register components with the datasets
        
        SquadData.RegisterComponent(new ComponentArray<SquadObjective>(id => new SquadObjective(id)));
    }

    public void RegisterActions()
    {
        // Register actions
    }

    public void RegisterStrategies()
    {
        StrategySystem.RegisterStrategy(new GotoObjectiveStrategy(SquadData, LocationQueue, 0.25f));
    }
    
    public Agent AddAgent(BotOwner bot)
    {
        var agent = AgentData.AddEntity(bot);
        
        DebugLog.Write($"Adding {agent} to Phobos");
        SquadSystem.AddAgent(agent);
        _telemetry.AddEntity(agent);
        return agent;
    }

    public void RemoveAgent(Agent agent)
    {
        DebugLog.Write($"Removing {agent} from Phobos");
        AgentData.RemoveEntity(agent);
        
        ActionSystem.RemoveAgent(agent);
        SquadSystem.RemoveAgent(agent);
        _telemetry.RemoveEntity(agent);
    }

    public void Update()
    {
        ActionSystem.Update();
        SquadSystem.Update();
    }
}