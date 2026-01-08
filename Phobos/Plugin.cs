using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DrakiaXYZ.BigBrain.Brains;
using Phobos.Enums;
using Phobos.Patches;
using UnityEngine;

namespace Phobos;

[BepInPlugin("com.janky.phobos", "Janky-Phobos", PhobosVersion)]
[SuppressMessage("ReSharper", "HeapView.ObjectAllocation.Evident")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class Plugin : BaseUnityPlugin
{
    public const string PhobosVersion = "0.1.0";

    public static ManualLogSource Log;

    public static ConfigEntry<float> RaidConvergence;
    public static ConfigEntry<float> RaidConvergenceRandomness;
    private static ConfigEntry<bool> _loggingEnabled;
    
    private void Awake()
    {
        Log = Logger;

        StartCoroutine(DelayedLoad());
    }

    private IEnumerator DelayedLoad()
    {
        // We wait for 5 seconds to allow all the 500 shonky mods (incl. this one) an average user installs to load 
        yield return new WaitForSeconds(5);

        // Config
        SetupConfig();

        Log.LogInfo("Initialization finished");

        if (_loggingEnabled.Value)
        {
            Log.LogInfo("Logging enabled");
        }
        else
        {
            Log.LogInfo("Logging disabled");
            BepInEx.Logging.Logger.Sources.Remove(Log);
        }
        
        // Patches
        new PhobosInitPatch().Enable();
        new PhobosDisposePatch().Enable();
        new PhobosFrameUpdatePatch().Enable();
        
        new MovementContextIsAIPatch().Enable();
        new TestBotMoverManualFixedUpdatePatch().Enable();
        // new TestBotMoverManualUpdatePatch().Enable();
        
        // Misc setup
        var brains = new List<string>()
        {
            nameof(BsgBrain.PMC),
            nameof(BsgBrain.PmcUsec),
            nameof(BsgBrain.PmcBear),
            nameof(BsgBrain.Assault),
            nameof(BsgBrain.Knight),
            nameof(BsgBrain.BigPipe),
            nameof(BsgBrain.BirdEye),
            nameof(BsgBrain.SectantPriest),
            nameof(BsgBrain.SectantWarrior),
        };
        
        // TODO: Revert to 19
        BrainManager.AddCustomLayer(typeof(PhobosLayer), brains,119);

        // This layer makes scavs stand still doing bugger all, remove it
        BrainManager.RemoveLayer("AssaultEnemyFar", brains);
    }
    
    private void SetupConfig()
    {
        const string general = "01. General";
        const string debug = "02. Debug";

        /*
         * General
         */
        RaidConvergence = Config.Bind(general, "Raid Convergence", 0f, new ConfigDescription(
            "",
            new AcceptableValueRange<float>(-10f, 10f),
            new ConfigurationManagerAttributes { Order = 2 }
        ));
        
        RaidConvergenceRandomness = Config.Bind(general, "Raid Convergence Randomness", 1f, new ConfigDescription(
            "",
            new AcceptableValueRange<float>(0f, 1f),
            new ConfigurationManagerAttributes { Order = 1 }
        ));

        /*
         * Deboog
         */
        _loggingEnabled = Config.Bind(debug, "Enable Debug Logging (RESTART)", true, new ConfigDescription(
            "Duh. Requires restarting the game to take effect.",
            null,
            new ConfigurationManagerAttributes { Order = 1 }
        ));
    }
}