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
        
        // Misc setup
        var brains = new List<string>()
        {
            EBrain.PMC.ToString(),
            EBrain.PmcUsec.ToString(),
            EBrain.PmcBear.ToString(),
            EBrain.Assault.ToString()
        };
        
        BrainManager.AddCustomLayer(typeof(Actor), brains,10000);

        // This should be the new peaceful action
        // BrainManager.RemoveLayer("Utility peace", brains);
    }
    
    private void SetupConfig()
    {
        // const string general = "01. General";
        const string debug = "02. Debug";

        /*
         * General
         */
        

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