using System;
using System.Text;
using Comfort.Common;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using Phobos.ECS;
using Phobos.ECS.Entities;
using Phobos.ECS.Systems;
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
        
        // TODO: Expand this into the suspended detection system
        botOwner.Brain.BaseBrain.OnLayerChangedTo += layer => Plugin.Log.LogInfo("Layer changed to " + layer.Name());
        
        _systemOrchestrator = Singleton<SystemOrchestrator>.Instance;
        
        _actor = new Actor(botOwner);
        _systemOrchestrator.AddActor(_actor);
        _squad = _systemOrchestrator.SquadOrchestrator.GetSquad(_actor.SquadId);
    }
    
    public override string GetName()
    {
        return LayerName;
    }

    public override void Start()
    {
    }
    
    public override void Stop()
    {
        // We died/exfilled/etc.
        _systemOrchestrator.RemoveActor(_actor);
    }


    public override Action GetNextAction()
    {
        return new Action(typeof(DummyAction), "Dummy Action");
    }

    public override bool IsActive()
    {
        return true;

        // var isHealing = false;
        //
        // if (BotOwner.Medecine != null)
        // {
        //     isHealing = BotOwner.Medecine.Using;
        //
        //     if (BotOwner.Medecine.FirstAid != null)
        //         isHealing |= BotOwner.Medecine.FirstAid.Have2Do;
        //     if (BotOwner.Medecine.SurgicalKit.HaveWork)
        //         isHealing |= BotOwner.Medecine.SurgicalKit.HaveWork;
        // }
        //
        // var isInCombat = BotOwner.Memory.IsUnderFire || BotOwner.Memory.HaveEnemy || Time.time - BotOwner.Memory.LastEnemyTimeSeen < 30f;
        //
        // if (isHealing || isInCombat)
        //     return false;

        // return Update(objective);
    }
    
    public override bool IsCurrentActionEnding()
    {
        return false;
    }

    public override void BuildDebugText(StringBuilder sb)
    {
        sb.AppendLine("*** Actor ***");
        sb.AppendLine($"{_actor}, active: {_actor.IsActive}, paused: {_actor.Paused}, suspended: {_actor.Suspended}");
        sb.AppendLine($"{_actor.Task}, {_actor.Routing}");
        sb.AppendLine("*** Squad ***");
        sb.AppendLine($"{_squad}, size: {_squad.Count}, {_squad.Task}");
        sb.AppendLine($"Standby: {BotOwner.StandBy.StandByType} candostandby: {BotOwner.StandBy.CanDoStandBy}");
    }
}