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
using Phobos.Config;
using Phobos.Diag;
using Phobos.Enums;
using Phobos.Orchestration;
using Phobos.Patches;
using UnityEngine;

namespace Phobos;

[BepInPlugin("com.janky.phobos", "Janky-Phobos", PhobosVersion)]
[BepInDependency("xyz.drakia.waypoints")]
[SuppressMessage("ReSharper", "HeapView.ObjectAllocation.Evident")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class Plugin : BaseUnityPlugin
{
    public const string PhobosVersion = "0.1.0";

    public static ManualLogSource Log;

    public static ConfigEntry<bool> ScavSquadsEnabled;
    public static ConfigEntry<float> ZoneRadiusScale;
    public static ConfigEntry<float> ZoneForceScale;
    public static ConfigEntry<float> ZoneRadiusDecayScale;
    
    public static ConfigEntry<bool> EmbarkOnFullSquad;
    
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
            nameof(BsgBrain.SectantWarrior)
        };
        
        BrainManager.AddCustomLayer(typeof(PhobosLayer), brains,19);

        // This layer makes scavs stand still doing bugger all, remove it
        BrainManager.RemoveLayer("AssaultEnemyFar", brains);
        
        // Ensure that the configuration files are created
        // ReSharper disable once ObjectCreationAsStatement
        new PhobosConfig();
    }
    
    private void SetupConfig()
    {
        const string general = "01. General";
        const string objectives = "02. Objectives";
        const string debug = "XX. Diagnostics";

        /*
         * General
         */
        ScavSquadsEnabled = Config.Bind(general, "Brown Tide (RESTART)", false, new ConfigDescription(
            "Allows scavs to form squads. Beware! They'll tend to congeal into massive tides that sweep over the map.",
            null,
            new ConfigurationManagerAttributes { Order = 4 }
        ));
        
        ZoneRadiusScale = Config.Bind(general, "Zone Radius Scale", 1f, new ConfigDescription(
            "Scales the radius of the zones on the map.",
            new AcceptableValueRange<float>(0f, 10f),
            new ConfigurationManagerAttributes { Order = 3 }
        ));
        ZoneRadiusScale.SettingChanged += ZoneParametersChanged;
        
        ZoneForceScale = Config.Bind(general, "Zone Force Scale", 1f, new ConfigDescription(
            "Scales the forces exerted by the zones on the map. Negative scaling flips the sign, turning attractors into repulsors and vice versa.",
            new AcceptableValueRange<float>(-10f, 10f),
            new ConfigurationManagerAttributes { Order = 2 }
        ));
        ZoneForceScale.SettingChanged += ZoneParametersChanged;
        
        ZoneRadiusDecayScale = Config.Bind(general, "Zone Force Decay Scale", 1f, new ConfigDescription(
            "Scales the zone force decay exponent.",
            new AcceptableValueRange<float>(0f, 5f),
            new ConfigurationManagerAttributes { Order = 1 }
        ));
        ZoneRadiusDecayScale.SettingChanged += ZoneParametersChanged;
        
        /*
         * Objectives
         */
        EmbarkOnFullSquad = Config.Bind(objectives, "Embark Immediately", false, new ConfigDescription(
            "Embark immediately when the whole squad reaches the objective.",
            null,
            new ConfigurationManagerAttributes { Order = 1 }
        ));
        
        /*
         * Deboog
         */
        Config.Bind(debug, "Camera Coords", "", new ConfigDescription(
            "Displays the camera coordinates to aid positioning.",
            null,
            new ConfigurationManagerAttributes { Order = 3, CustomDrawer = CameraCoordsToggle }
        ));
        
        Config.Bind(debug, "Location System", "", new ConfigDescription(
            "Displays information about the location system state.",
            null,
            new ConfigurationManagerAttributes { Order = 2, CustomDrawer = LocationSystemTelemetryToggle }
        ));
        
        _loggingEnabled = Config.Bind(debug, "Enable Debug Logging (RESTART)", true, new ConfigDescription(
            "Duh. Requires restarting the game to take effect.",
            null,
            new ConfigurationManagerAttributes { Order = 1 }
        ));
    }

    private static void ZoneParametersChanged(object sender, EventArgs args)
    {
        Singleton<PhobosManager>.Instance?.AssignmentSystem.CalculateZones();
    }
    
    private static void CameraCoordsToggle(ConfigEntryBase entry)
    {
        if (GUILayout.Button("Show"))
        {
            var gameWorld = Singleton<GameWorld>.Instance;
        
            if (gameWorld == null)
                return;
            
            gameWorld.GetOrAddComponent<CameraTelemetry>();
        }

        // ReSharper disable once InvertIf
        if (GUILayout.Button("Hide"))
        {
            var gameWorld = Singleton<GameWorld>.Instance;
        
            if (gameWorld == null)
                return;
            
            var component =  gameWorld.GetComponent<CameraTelemetry>();
            
            if (component == null)
                return;
            
            Destroy(component);
        }
    }

    
    private static void LocationSystemTelemetryToggle(ConfigEntryBase entry)
    {
        if (GUILayout.Button("Show Map"))
        {
            var gameWorld = Singleton<GameWorld>.Instance;
        
            if (gameWorld == null)
                return;
            
            gameWorld.GetOrAddComponent<ZoneTelemetry>();
        }

        if (GUILayout.Button("Hide Map"))
        {
            var gameWorld = Singleton<GameWorld>.Instance;
        
            if (gameWorld == null)
                return;
            
            var component =  gameWorld.GetComponent<ZoneTelemetry>();
            
            if (component == null)
                return;
            
            Destroy(component);
        }
        
        if (GUILayout.Button("Reload Zones"))
        {
            Singleton<PhobosManager>.Instance?.AssignmentSystem.CalculateZones();
        }
    }
}