using System.Text;
using Comfort.Common;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using Phobos.Diag;
using Phobos.Entities;
using Phobos.Orchestration;
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
    private const int ActivationDelay = 5; 

    private int _activationFrame;

    private readonly PhobosManager _phobosManager;
    private readonly Agent _agent;
    private readonly Squad _squad; 
    
    public PhobosLayer(BotOwner botOwner, int priority) : base(botOwner, priority)
    {
        // Have to turn this off otherwise bots will be deactivated far away.
        botOwner.StandBy.CanDoStandBy = false;
        botOwner.StandBy.Activate();
        
        _phobosManager = Singleton<PhobosManager>.Instance;
        
        _agent = _phobosManager.AddAgent(botOwner);

        var bsgSquadId = _agent.Bot.BotsGroup.Id;
        _squad = _phobosManager.SquadRegistry[bsgSquadId];

        botOwner.Brain.BaseBrain.OnLayerChangedTo += OnLayerChanged;
        botOwner.GetPlayer.OnPlayerDead += OnDead;
    }

    private void OnDead(Player player, IPlayer lastAggressor, DamageInfoStruct damageInfo, EBodyPart part)
    {
        player.OnPlayerDead -= OnDead;
        _agent.IsActive = false;
        _phobosManager.RemoveAgent(_agent);
    }

    private void OnLayerChanged(AICoreLayerClass<BotLogicDecision> layer)
    {
        if (layer.Name() == LayerName)
        {
            _activationFrame = Time.frameCount;
        }
        else if (_agent.IsActive)
        {
            DebugLog.Write($"Deactivating {_agent}");
            _agent.IsActive = false;
        }
        
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
        var isHealing = BotOwner.Medecine.Using || BotOwner.Medecine.SurgicalKit.HaveWork || BotOwner.Medecine.FirstAid.Have2Do;
        return !isHealing;
    }
    
    public override bool IsCurrentActionEnding()
    {
        // SAIN unfortunately messes up the bot move state. We need to delay activation by a couple of frames to allow the state to get updated
        // properly by the BSG code, otherwise bots will be teleported to the last BSG managed position.
        
        // ReSharper disable once InvertIf
        if (!_agent.IsActive && Time.frameCount - _activationFrame > ActivationDelay)
        {
            DebugLog.Write($"Activating {_agent}");
            _agent.IsActive = true;
        }
        
        return false;
    }

    public override void BuildDebugText(StringBuilder sb)
    {
        sb.AppendLine($"{_agent} Task: {_agent.TaskAssignment.Task}");
        sb.AppendLine($"{_agent.Movement}");
        sb.AppendLine("*** Generic ***");
        sb.AppendLine($"HasEnemy: {BotOwner.Memory.HaveEnemy} UnderFire: {BotOwner.Memory.IsUnderFire}");
        sb.AppendLine($"Pose: {BotOwner.GetPlayer.MovementContext.PoseLevel} Speed: {BotOwner.Mover?.DestMoveSpeed}");
        sb.AppendLine("*** Squad ***");
        sb.AppendLine($"{_squad}, size: {_squad.Size}");
        sb.AppendLine("*** Actions ***");
        Singleton<Telemetry>.Instance.GenerateUtilityReport(_agent, sb);
        // sb.AppendLine($"Standby: {BotOwner.StandBy.StandByType} CanDoStandBy: {BotOwner.StandBy.CanDoStandBy}");
    }
}