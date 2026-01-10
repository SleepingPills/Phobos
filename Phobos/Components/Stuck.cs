using UnityEngine;

namespace Phobos.Components;

public enum StuckState
{
    None,
    Vaulting,
    Jumping,
    Retrying,
    Teleport
}

public class Stuck
{
    // Thresholds
    public const float MaxMoveSpeed = 5f; // Maximum bot movement speed in m/s
    public const float StuckThresholdMultiplier = 0.25f; // Bot moving at less than 25% expected speed is stuck
    public const float VaultAttemptDelay = 1f;
    public const float JumpAttemptDelay = 2f + VaultAttemptDelay;
    public const float PathRetryDelay = 5f + JumpAttemptDelay;
    public const float TeleportDelay = 5f + PathRetryDelay;
    public const float GiveupDelay = 5f + PathRetryDelay;

    public Vector3 LastPosition;
    public float Timer;
    public StuckState State = StuckState.None;

    public override string ToString()
    {
        return $"Stuck(State: {State} timer: {Timer} last position: {LastPosition})";
    }
}