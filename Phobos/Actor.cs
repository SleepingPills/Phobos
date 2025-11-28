using System;
using System.Text;
using Comfort.Common;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using Phobos.ECS;
using Phobos.ECS.Systems;
using Phobos.Strategy;
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

public class Actor : CustomLayer, IEquatable<Actor>
{
    public const string LayerName = "Phobos:Actor";
    
    public readonly int Id;
    public readonly int SquadId;

    private readonly Orchestrator _orchestrator;
    private bool _paused;
    private float _pauseTimer;

    public Actor(BotOwner botOwner, int priority) : base(botOwner, priority)
    {
        Id = botOwner.Id;
        SquadId = botOwner.BotsGroup.Id;

        _orchestrator = Singleton<Orchestrator>.Instance;

        // Have to turn this off otherwise bots will be deactivated far away.
        botOwner.StandBy.CanDoStandBy = false;
        
        botOwner.Brain.BaseBrain.OnLayerChangedTo += layer => Plugin.Log.LogInfo("Layer changed to " + layer.Name());
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
        _orchestrator.RemoveActor(this);
    }


    public override Action GetNextAction()
    {
        return new Action(typeof(DummyAction), "Dummy Action");
    }

    public override bool IsActive()
    {
        // ReSharper disable once InvertIf
        if (_paused)
        {
            if (_pauseTimer - Time.time > 0)
                return false;

            _paused = false;
        }
        
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

    // private bool Update(Location objective)
    // {
    //     if (!_executing)
    //     {
    //         _executing = true;
    //
    //         Plugin.Log.LogInfo($"Picked objective: {objective.Name} | {objective.Category}");
    //
    //         if (NavMesh.SamplePosition(BotOwner.Position, out var origin, 10, NavMesh.AllAreas))
    //         {
    //             if (NavMesh.SamplePosition(objective.Position, out var target, 10, NavMesh.AllAreas))
    //             {
    //                 var path = new NavMeshPath();
    //                 NavMesh.CalculatePath(origin.position, target.position, NavMesh.AllAreas, path);
    //
    //                 if (path.status == NavMeshPathStatus.PathInvalid)
    //                 {
    //                     DebugGizmos.Line(origin.position, target.position, expiretime: 0f, lineWidth: 0.1f, color: Color.red);
    //                     Plugin.Log.LogInfo($"Path status: {path.status}");
    //                     return false;
    //                 }
    //
    //                 var corners = path.corners;
    //
    //                 PathVis.Show(corners, Color.red, thickness: 0.1f);
    //
    //                 if (path.status == NavMeshPathStatus.PathPartial)
    //                 {
    //                     Plugin.Log.LogInfo($"Path status: {path.status} running secondary pathfinding");
    //
    //                     var path2 = new NavMeshPath();
    //                     NavMesh.CalculatePath(corners[^1], target.position, NavMesh.AllAreas, path2);
    //
    //                     if (path2.status == NavMeshPathStatus.PathInvalid)
    //                     {
    //                         Plugin.Log.LogInfo($"Path status: {path2.status} secondary pathfinding failed");
    //                     }
    //                     else
    //                     {
    //                         Plugin.Log.LogInfo($"Path status: {path2.status} running secondary succeeded");
    //                         PathVis.Show(path2.corners, Color.magenta, thickness: 0.1f);
    //                         corners = corners.Concat(path2.corners).ToArray();
    //                     }
    //                 }
    //
    //                 DebugGizmos.Line(corners[^1], target.position, expiretime: 0f, lineWidth: 0.1f, color: Color.blue);
    //                 DebugGizmos.Line(target.position, objective.Position, expiretime: 0f, lineWidth: 0.1f, color: Color.green);
    //
    //                 // if (path.status == NavMeshPathStatus.PathPartial)
    //                 // {
    //                 //     var distanceToEndOfPath = Vector3.Distance(target.position, path.corners[^1]);
    //                 //     if (distanceToEndOfPath > 20f)
    //                 //     {
    //                 //         Plugin.Log.LogInfo($"Path status: {path.status} failing due to distance {distanceToEndOfPath} > 20");
    //                 //         return false;
    //                 //     }
    //                 // }
    //
    //                 BotOwner.Mover.GoToByWay(corners, 0);
    //                 _executing = true;
    //                 return true;
    //             }
    //         }
    //
    //         Plugin.Log.LogInfo($"Failed to get point on the navmesh close enough to the target");
    //     }
    //
    //     // We'll enforce these whenever the bot is under way
    //     BotOwner.SetPose(1f);
    //     BotOwner.BotLay.GetUp(true);
    //     BotOwner.DoorOpener.ManualUpdate();
    //
    //     // BotOwner.Mover.ActualPathController.CurPath.CurIndex
    //
    //     if (BotOwner.Mover.ActualPathController.CurPath == null)
    //         return true;
    //
    //     BotOwner.Mover.ActualPathController.CurPath.IsPointOnWay(BotOwner.Position, 1f);
    //
    //     // Env Id == 0 is outside
    //     if (!BotOwner.Mover.NoSprint
    //         && BotOwner.GetPlayer.Physical.CanSprint
    //         && BotOwner.AIData.EnvironmentId == 0
    //         && CalculateAveragePathAngle(BotOwner.Mover.ActualPathController.CurPath.Vector3_0,
    //             BotOwner.Mover.ActualPathController.CurPath.CurIndex) < 15f)
    //     {
    //         BotOwner.Mover.Sprint(true);
    //     }
    //     else
    //         BotOwner.Mover.Sprint(false);
    //
    //     BotOwner.Steering.LookToMovingDirection(520);
    //
    //     return true;
    // }
    
    public override bool IsCurrentActionEnding()
    {
        return false;
    }

    public bool Equals(Actor other)
    {
        if (ReferenceEquals(other, null))
            return false;

        return Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public override void BuildDebugText(StringBuilder sb)
    {
        sb.AppendLine($"Squad {SquadId}, Size: {_squad.Count}");
        sb.AppendLine($"Squad Objective: {_squad.Objective?.Category} | {_squad.Objective?.Name}");
        sb.AppendLine($"Mover ismoving: {BotOwner.Mover.IsMoving} iscometo: {BotOwner.Mover.IsComeTo(1f, false)}");
        sb.AppendLine($"Standby: {BotOwner.StandBy.StandByType} candostandby: {BotOwner.StandBy.CanDoStandBy}");
        sb.AppendLine(
            $"CurPath: {BotOwner.Mover.ActualPathController.CurPath} progress {BotOwner.Mover.ActualPathController.CurPath?.CurIndex}/{BotOwner.Mover.ActualPathController.CurPath?.Length}"
        );
    }

    private static float CalculateAveragePathAngle(Vector3[] pathCorners, int startIndex = 0, int count = 3)
    {
        // Validate inputs
        if (pathCorners == null || pathCorners.Length < 3)
            return 0f;

        if (startIndex < 0 || startIndex >= pathCorners.Length - 2)
            return 0f;

        // Clamp count to available corners
        var maxCount = pathCorners.Length - startIndex - 2;
        if (count > maxCount)
            count = maxCount;

        if (count <= 0)
            return 0f;

        var angleMax = 0f;

        // Calculate angles between consecutive segments
        for (var i = 0; i < count; i++)
        {
            var idx = startIndex + i;
            var pointA = pathCorners[idx];
            var pointB = pathCorners[idx + 1];
            var pointC = pathCorners[idx + 2];

            // Calculate direction vectors
            var directionAb = (pointB - pointA).normalized;
            var directionBc = (pointC - pointB).normalized;

            // Calculate angle between the two direction vectors
            var angle = Vector3.Angle(directionAb, directionBc);
            if (angle > angleMax)
                angleMax = angle;
        }

        return angleMax;
    }

    // private bool EmbarkToObjective(Location objective)
    // {
    //     var pathStatus = BotOwner.Mover.GoToPoint(objective.Position, true, 5f, true);
    //
    //     if (pathStatus == NavMeshPathStatus.PathInvalid)
    //     {
    //         State = ActorState.Invalid;
    //         return false;
    //     }
    //
    //     var pathController = BotOwner.Mover.ActualPathController;
    //     if (pathStatus == NavMeshPathStatus.PathPartial)
    //     {
    //         var distanceToEndOfPath = Vector3.Distance(BotOwner.Position, pathController.CurPath.LastCorner());
    //         if (distanceToEndOfPath > 5f)
    //         {
    //             State = ActorState.Invalid;
    //             return false;
    //         }
    //     }
    //     
    //     State = ActorState.Executing;
    //     return true;
    // }

    // private bool Move()
    // {
    //     var pathController = BotOwner.Mover.ActualPathController;
    //     
    //     if (pathController.CurPath == null)
    //     {
    //         State = ActorState.Ready;
    //         return false;
    //     }
    //     
    //     // TODO: Check if we reached the objective
    //     
    //     BotOwner.Mover.Sprint(true);
    //
    //     return true;
    // }

    public void PauseDuration(float duration)
    {
        _paused = true;
        _pauseTimer = Time.time + duration;
    }

    public void PauseUntil(float timer)
    {
        _paused = true;
        _pauseTimer = timer;
    }

    public void Resume()
    {
        _paused = false;
    }
}