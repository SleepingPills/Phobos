using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using UnityEngine;

namespace Phobos.Config;

public struct Range(float min, float max)
{
    public static Range Zero =>  new Range(0f, 0f);
    public static Range ZeroOne => new Range(0f, 1f);
    
    [JsonRequired] public float Min { get; set; } = min;
    [JsonRequired] public float Max { get; set; } = max;
        
    public float SampleGaussian()
    {
        // Box-Muller transform using Unity's Random
        var u1 = 1.0f - Random.value;
        var u2 = 1.0f - Random.value;
        var stdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        
        // Map from standard normal to range, using sigma ≈ (max-min)/6 
        // so ~99.7% of samples fall within [min, max]
        var mean = (Min + Max) * 0.5f;
        var sigma = (Max - Min) / 6.0f;
        
        return Mathf.Clamp(mean + stdNormal * sigma, Min, Max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float SampleUniform()
    {
        return Random.Range(Min, Max);
    }
}