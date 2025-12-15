using System.Text;
using Comfort.Common;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using Phobos.Diag;
using Phobos.ECS;
using Phobos.ECS.Entities;
using Phobos.Entities;
using UnityEngine;

namespace Phobos;

internal class DummyAction(BotOwner botOwner) : CustomLogic(botOwner)
{
    public override void Start()
    {
    }

    public override void Stop()
    {
    }

    public override void Update(CustomLayer.ActionData data)
    {
    }
}

public class PhobosLayer : CustomLayer
{
    private const string LayerName = "PhobosLayer";

    private readonly SystemOrchestrator _systemOrchestrator;
    private readonly Agent _agent;
    private readonly Squad _squad;
    
    public PhobosLayer(BotOwner botOwner, int priority) : base(botOwner, priority)
    {
        // Have to turn this off otherwise bots will be deactivated far away.
        botOwner.StandBy.CanDoStandBy = false;
        botOwner.StandBy.Activate();
        
        _systemOrchestrator = Singleton<SystemOrchestrator>.Instance;
        
        _agent = new Agent(botOwner);
        _systemOrchestrator.AddAgent(_agent);
        _squad = _systemOrchestrator.SquadOrchestrator.GetSquad(_agent.SquadId);

        botOwner.Brain.BaseBrain.OnLayerChangedTo += OnLayerChanged;
        botOwner.GetPlayer.OnPlayerDead += OnDead;
    }

    private void OnDead(Player player, IPlayer lastAggressor, DamageInfoStruct damageInfo, EBodyPart part)
    {
        player.OnPlayerDead -= OnDead;
        _agent.IsLayerActive = false;
        _systemOrchestrator.RemoveAgent(_agent);
    }

    private void OnLayerChanged(AICoreLayerClass<BotLogicDecision> layer)
    {
        _agent.IsLayerActive = layer.Name() == LayerName;
        DebugLog.Write($"{_agent} layer changed to: {layer.Name()} priority: {layer.Priority}");
    }
    
    public override string GetName()
    {
        return LayerName;
    }

    public override Action GetNextAction()
    {
        return new Action(typeof(DummyAction), "Dummy Action");
    }

    public override bool IsActive()
    {
        var isHealing = false;
        
        if (BotOwner.Medecine != null)
        {
            isHealing = BotOwner.Medecine.Using;
        
            if (BotOwner.Medecine.FirstAid != null)
                isHealing |= BotOwner.Medecine.FirstAid.Have2Do;
            if (BotOwner.Medecine.SurgicalKit.HaveWork)
                isHealing |= BotOwner.Medecine.SurgicalKit.HaveWork;
        }

        var isInCombat = BotOwner.Memory.IsUnderFire || (BotOwner.Memory.HaveEnemy && Time.time - BotOwner.Memory.LastEnemyTimeSeen < 20f);
        
        if (isHealing || isInCombat)
            return false;
        
        return _agent.IsPhobosActive;
    }
    
    public override bool IsCurrentActionEnding()
    {
        return false;
    }

    public override void BuildDebugText(StringBuilder sb)
    {
        sb.AppendLine("*** Actor ***");
        sb.AppendLine($"{_agent}");
        sb.AppendLine($"{_agent.Task}");
        sb.AppendLine($"{_agent.Movement}");
        sb.AppendLine($"HasEnemy: {BotOwner.Memory.HaveEnemy} UnderFire: {BotOwner.Memory.IsUnderFire}");
        sb.AppendLine($"Pose: {BotOwner.GetPlayer.MovementContext.PoseLevel} Speed: {BotOwner.Mover?.DestMoveSpeed}");
        sb.AppendLine($"Standby: {BotOwner.StandBy.StandByType} candostandby: {BotOwner.StandBy.CanDoStandBy}");
        sb.AppendLine("*** Squad ***");
        sb.AppendLine($"{_squad}, size: {_squad.Count}, {_squad.Objective}");
    }
}