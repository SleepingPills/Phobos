using System.Runtime.CompilerServices;
using EFT;
using Phobos.Navigation;
using UnityEngine;

namespace Phobos.Components;


public class Movement(BotOwner bot)
{
    public Vector3? Target;
    public NavJob CurrentJob;
    
    public float Speed = 1f;
    public float Pose = 1f;
    public bool Sprint = false;
    public bool Prone = false;
    
    public int Retry = 0;

    public BotCurrentPathAbstractClass ActualPath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => bot.Mover.ActualPathController.CurPath;
    }

    public override string ToString()
    {
        return $"Movement({Target} Try: {Retry} Path: {ActualPath?.CurIndex}/{ActualPath?.Length})";
    }
}