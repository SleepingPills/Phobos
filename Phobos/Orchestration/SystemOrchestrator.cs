using Phobos.Diag;
using Phobos.ECS.Actions;
using Phobos.ECS.Entities;
using Phobos.ECS.Systems;
using Phobos.Entities;
using Phobos.Navigation;

namespace Phobos.ECS;

// ReSharper disable MemberCanBePrivate.Global
// Every AI project needs a shitty, llm generated component name like "orchestrator".
public class SystemOrchestrator
{
    public readonly SquadOrchestrator SquadOrchestrator;
    public readonly ActionOrchestrator ActionOrchestrator;
    
    public readonly MovementSystem MovementSystem;
    public readonly AgentList LiveAgents;

    public SystemOrchestrator(NavJobExecutor navJobExecutor, LocationQueue locationQueue)
    {
        LiveAgents = new AgentList(32);
        
        DebugLog.Write("Creating MovementSystem");
        MovementSystem = new MovementSystem(navJobExecutor, LiveAgents);
        
        DebugLog.Write("Creating SquadOrchestrator");
        SquadOrchestrator = new SquadOrchestrator(locationQueue);
        
        DebugLog.Write("Creating ActionOrchestrator");
        ActionOrchestrator = new ActionOrchestrator(LiveAgents);
    }

    public void AddAgent(Agent agent)
    {
        DebugLog.Write($"Adding {agent} to Phobos systems");
        LiveAgents.Add(agent);
        SquadOrchestrator.AddAgent(agent);
    }

    public void RemoveAgent(Agent agent)
    {
        LiveAgents.SwapRemove(agent);
        SquadOrchestrator.RemoveAgent(agent);
        ActionOrchestrator.RemoveAgent(agent);
    }

    public void Update()
    {
        SquadOrchestrator.Update();
        ActionOrchestrator.Update();
        
        QuestObjectiveSystem.Update();
        MovementSystem.Update();
    }
}