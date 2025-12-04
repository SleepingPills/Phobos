using System.Text;
using Comfort.Common;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using Phobos.Diag;
using Phobos.ECS;
using Phobos.ECS.Entities;
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
    private readonly Actor _actor;
    private readonly Squad _squad;
    
    public PhobosLayer(BotOwner botOwner, int priority) : base(botOwner, priority)
    {
        // Have to turn this off otherwise bots will be deactivated far away.
        botOwner.StandBy.CanDoStandBy = false;
        botOwner.StandBy.Activate();
        
        _systemOrchestrator = Singleton<SystemOrchestrator>.Instance;
        
        _actor = new Actor(botOwner);
        _systemOrchestrator.AddActor(_actor);
        _squad = _systemOrchestrator.SquadOrchestrator.GetSquad(_actor.SquadId);

        botOwner.Brain.BaseBrain.OnLayerChangedTo += OnLayerChanged;
        botOwner.GetPlayer.OnPlayerDead += OnDead;
    }

    private void OnDead(Player player, IPlayer lastAggressor, DamageInfoStruct damageInfo, EBodyPart part)
    {
        player.OnPlayerDead -= OnDead;
        _actor.IsLayerActive = true;
        _systemOrchestrator.RemoveActor(_actor);
    }

    private void OnLayerChanged(AICoreLayerClass<BotLogicDecision> layer)
    {
        _actor.IsLayerActive = layer.Name() == LayerName;
        DebugLog.Write($"{_actor} layer changed to: {layer.Name()} priority: {layer.Priority}");
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

        // var isInCombat = BotOwner.Memory.IsUnderFire || (BotOwner.Memory.HaveEnemy && Time.time - BotOwner.Memory.LastEnemyTimeSeen < 10f);
        return !isHealing && _actor.IsPhobosActive;
    }
    
    public override bool IsCurrentActionEnding()
    {
        return false;
    }

    public override void BuildDebugText(StringBuilder sb)
    {
        sb.AppendLine("*** Actor ***");
        sb.AppendLine($"{_actor}");
        sb.AppendLine($"{_actor.Task}");
        sb.AppendLine($"{_actor.Movement}");
        sb.AppendLine($"HasEnemy: {BotOwner.Memory.HaveEnemy} UnderFire: {BotOwner.Memory.IsUnderFire}");
        sb.AppendLine($"Pose: {BotOwner.GetPlayer.MovementContext.PoseLevel}");
        sb.AppendLine($"Standby: {BotOwner.StandBy.StandByType} candostandby: {BotOwner.StandBy.CanDoStandBy}");
        sb.AppendLine("*** Squad ***");
        sb.AppendLine($"{_squad}, size: {_squad.Count}, {_squad.Task}");
    }
}