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
    private readonly PhobosManager _phobos;
    private readonly Agent _agent;

    public PhobosLayer(BotOwner botOwner, int priority) : base(botOwner, priority)
    {
        // Have to turn this off otherwise bots will be deactivated far away.
        botOwner.StandBy.CanDoStandBy = false;
        botOwner.StandBy.Activate();

        _phobos = Singleton<PhobosManager>.Instance;
        _agent = _phobos.AddAgent(botOwner);

        botOwner.Brain.BaseBrain.OnLayerChangedTo += OnLayerChanged;
        botOwner.GetPlayer.OnPlayerDead += OnDead;

        // Door hack
        var botCollider = _agent.Bot.GetPlayer.CharacterController.GetCollider();
        var pomCollider = _agent.Bot.GetPlayer.POM.Collider;

        var doors = _phobos.DoorSystem.Doors;
        for (var i = 0; i < doors.Length; i++)
        {
            var door = doors[i];
            Physics.IgnoreCollision(pomCollider, door.Collider);
            EFTPhysicsClass.IgnoreCollision(botCollider, door.Collider);
        }
    }

    private void OnDead(Player player, IPlayer lastAggressor, DamageInfoStruct damageInfo, EBodyPart part)
    {
        player.OnPlayerDead -= OnDead;
        _agent.IsActive = false;
        _phobos.RemoveAgent(_agent);
    }

    private void OnLayerChanged(AICoreLayerClass<BotLogicDecision> layer)
    {
        if (layer.Name() == LayerName)
        {
            // Stop the canned bot mover
            Log.Debug($"{_agent} stopping builtin bot mover");
            _agent.Bot.Mover.Stop();
            _agent.IsActive = true;
        }
        else
        {
            if (_agent.IsActive)
            {
                // Final insurance that the bot is set to the navmwesh before we hand over the brain
                Log.Debug($"{_agent} setting player to navmesh");
                _agent.Bot.Mover.SetPlayerToNavMesh(_agent.Position);
                _agent.IsActive = false;
            }
        }

        Log.Debug($"{_agent} layer changed to: {layer.Name()} priority: {layer.Priority}");
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
        var isInCombat = BotOwner.Memory.IsUnderFire || BotOwner.Memory.HaveEnemy || Time.time - BotOwner.Memory.LastEnemyTimeSeen < 15f;
        return !isHealing && !isInCombat;
    }

    // ReSharper disable once InvertIf
    public override bool IsCurrentActionEnding()
    {
        return false;
    }

    public override void BuildDebugText(StringBuilder sb)
    {
        var pose = BotOwner.GetPlayer.MovementContext.PoseLevel;
        var destSpeed = BotOwner.Mover?.DestMoveSpeed;
        var actualSpeed = _agent.Player.MovementContext.CharacterMovementSpeed;

        var distMove = 0f;
        if (_agent.Movement.HasPath)
        {
            distMove = (_agent.Movement.Target - _agent.Position).sqrMagnitude;
        }

        var distObj = 0f;
        if (_agent.Objective.Location != null)
        {
            distObj = (_agent.Objective.Location.Position - _agent.Position).sqrMagnitude;
        }

        sb.AppendLine($"{_agent} Task: {_agent.TaskAssignment.Task}");
        sb.AppendLine($"{_agent.Movement} dist {distMove}");
        sb.AppendLine(_agent.Stuck.Soft.ToString());
        sb.AppendLine(_agent.Stuck.Hard.ToString());
        sb.AppendLine($"{_agent.Objective} dist {distObj}/{_agent.Objective.Location?.RadiusSqr}");
        sb.AppendLine("*** Generic ***");
        sb.AppendLine($"HasEnemy: {BotOwner.Memory.HaveEnemy} UnderFire: {BotOwner.Memory.IsUnderFire}");
        sb.AppendLine($"Pose: {pose} DestSpeed: {destSpeed} ActualSpeed: {actualSpeed}");
        sb.AppendLine("*** Squad ***");
        sb.AppendLine($"{_agent.Squad}, size: {_agent.Squad.Size}");
        sb.AppendLine($"{_agent.Squad.Objective}");
        sb.AppendLine("*** Actions ***");
        GenerateUtilityReport(sb);
        // sb.AppendLine($"Standby: {BotOwner.StandBy.StandByType} CanDoStandBy: {BotOwner.StandBy.CanDoStandBy}");
    }

    private void GenerateUtilityReport(StringBuilder sb)
    {
        var actions = _phobos.ActionManager.Tasks;

        for (var i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            var score = _agent.TaskScores[i];
            var prefix = action == _agent.TaskAssignment.Task ? "*" : "";
            sb.AppendLine($"{prefix}{action.GetType().Name}: {score:0.00}");
        }
    }
}