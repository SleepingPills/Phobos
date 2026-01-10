using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using Phobos.Diag;
using Phobos.Enums;
using Phobos.Orchestration;
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

    public static ConfigEntry<float> HotspotRadius;
    public static ConfigEntry<float> HotSpotRadiusDecay;
    
    public static ConfigEntry<float> RaidAdvection;
    public static ConfigEntry<float> RaidAdvectionRandomness;
    
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
        new BotMoverManualFixedUpdatePatch().Enable();
        new EnableVaultPatch().Enable();
        // new BotMoverManualUpdatePatch().Enable();
        
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
        BrainManager.AddCustomLayer(typeof(PhobosLayer), brains,int.MaxValue);

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
        HotspotRadius = Config.Bind(general, "Hotspot Radius", 200f, new ConfigDescription(
            "",
            new AcceptableValueRange<float>(0f, 1000f),
            new ConfigurationManagerAttributes { Order = 4 }
        ));
        HotspotRadius.SettingChanged += HotspotParametersChanged;
        
        HotSpotRadiusDecay = Config.Bind(general, "Hotspot Decay", 1f, new ConfigDescription(
            "",
            new AcceptableValueRange<float>(0f, 4f),
            new ConfigurationManagerAttributes { Order = 3 }
        ));
        HotSpotRadiusDecay.SettingChanged += HotspotParametersChanged;
        
        RaidAdvection = Config.Bind(general, "Advection", 0f, new ConfigDescription(
            "",
            new AcceptableValueRange<float>(-10f, 10f),
            new ConfigurationManagerAttributes { Order = 2 }
        ));
        
        RaidAdvectionRandomness = Config.Bind(general, "Advection Randomness", 1f, new ConfigDescription(
            "",
            new AcceptableValueRange<float>(0f, 1f),
            new ConfigurationManagerAttributes { Order = 1 }
        ));

        /*
         * Deboog
         */
        Config.Bind(debug, "Location System Telemetry", "", new ConfigDescription(
            "Displays information about the location system state",
            null,
            new ConfigurationManagerAttributes { Order = 2, CustomDrawer = LocationSystemTelemetryToggle }
        ));
        
        _loggingEnabled = Config.Bind(debug, "Enable Debug Logging (RESTART)", true, new ConfigDescription(
            "Duh. Requires restarting the game to take effect.",
            null,
            new ConfigurationManagerAttributes { Order = 1 }
        ));
    }

    private static void HotspotParametersChanged(object sender, EventArgs args)
    {
        Singleton<PhobosManager>.Instance?.LocationSystem.CalculateAdvectionField();
    }
    
    private static void LocationSystemTelemetryToggle(ConfigEntryBase entry)
    {
        if (GUILayout.Button("Show"))
        {
            var gameWorld = Singleton<GameWorld>.Instance;
        
            if (gameWorld == null)
                return;
            
            gameWorld.GetOrAddComponent<LocationSystemTelemetry>();
        }

        if (GUILayout.Button("Hide"))
        {
            var gameWorld = Singleton<GameWorld>.Instance;
        
            if (gameWorld == null)
                return;
            
            var component =  gameWorld.GetComponent<LocationSystemTelemetry>();
            
            if (component == null)
                return;
            
            Destroy(component);
        }
    }
}