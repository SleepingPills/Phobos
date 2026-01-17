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
    public const string PhobosVersion = "0.1.4";

    public static ManualLogSource Log;

    public static ConfigEntry<Vector2> ObjectiveGuardDuration;
    public static ConfigEntry<Vector2> ObjectiveAdjustedGuardDuration;
    public static ConfigEntry<Vector2> ObjectiveGuardDurationCut;

    public static ConfigEntry<bool> ScavSquadsEnabled;
    public static ConfigEntry<float> ZoneRadiusScale;
    public static ConfigEntry<float> ZoneForceScale;
    public static ConfigEntry<float> ZoneRadiusDecayScale;

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

        // Ensure that the configuration files are created
        // ReSharper disable once ObjectCreationAsStatement
        new PhobosConfig();

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
        new PhobosFrameUpdatePatch().Enable();
        new PhobosDisposePatch().Enable();
        
        new GotoPositionTeleportFixPatch().Enable();
        new BotMoverTeleportFixPatch().Enable();
        new MovementContextIsAIPatch().Enable();
        new EnableVaultPatch().Enable();
        new BotMoverManualFixedUpdatePatch().Enable();
        // new BotMoverManualUpdatePatch().Enable();

        // Takes over scavs at high priority when at long range
        new BypassAssaultEnemyFarPatch().Enable();
        // High priority (79!) and causes bots to get stuck
        new BypassExfiltrationPatch().Enable();

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

        BrainManager.AddCustomLayer(typeof(PhobosLayer), brains, 19);
    }

    private void SetupConfig()
    {
        const string general = "01. General";
        const string objectives = "02. Objectives";
        const string zones = "03. Zones";
        const string debug = "XX. Diagnostics";

        /*
         * General
         */
        ScavSquadsEnabled = Config.Bind(general, "Brown Tide (RESTART)", false, new ConfigDescription(
            "Allows scavs to form squads. Beware! They'll tend to congeal into massive tides that sweep over the map.",
            null,
            new ConfigurationManagerAttributes { Order = 1 }
        ));

        /*
         * Objectives
         */
        ObjectiveGuardDuration = Config.Bind(objectives, "Base Guard Duration (RESTART)", new Vector2(60f, 120f), new ConfigDescription(
            "Base guarding duration range. Squads will wait a bit at their objectives before moving on.",
            null,
            new ConfigurationManagerAttributes { Order = 3 }
        ));
        ObjectiveAdjustedGuardDuration = Config.Bind(objectives, "Adjusted Guard Duration (RESTART)", new Vector2(3.5f, 6.5f), new ConfigDescription(
            "Duration that squads can guard quest and synthetic objectives once all the members are at the location.",
            null,
            new ConfigurationManagerAttributes { Order = 2 }
        ));
        ObjectiveGuardDurationCut = Config.Bind(objectives, "Guard Duration Cut (RESTART)", new Vector2(0.1f, 0.5f), new ConfigDescription(
            "How much to scale down the remaining wait time for loot objectives once all the members are at the location",
            null,
            new ConfigurationManagerAttributes { Order = 1 }
        ));


        /*
         * General
         */
        ZoneRadiusScale = Config.Bind(zones, "Zone Radius Scale", 1f, new ConfigDescription(
            "Scales the radius of the zones on the map.",
            new AcceptableValueRange<float>(0f, 10f),
            new ConfigurationManagerAttributes { Order = 3 }
        ));
        ZoneRadiusScale.SettingChanged += ZoneParametersChanged;

        ZoneForceScale = Config.Bind(zones, "Zone Force Scale", 1f, new ConfigDescription(
            "Scales the forces exerted by the zones on the map. Negative scaling flips the sign, turning attractors into repulsors and vice versa.",
            new AcceptableValueRange<float>(-10f, 10f),
            new ConfigurationManagerAttributes { Order = 2 }
        ));
        ZoneForceScale.SettingChanged += ZoneParametersChanged;

        ZoneRadiusDecayScale = Config.Bind(zones, "Zone Force Decay Scale", 1f, new ConfigDescription(
            "Scales the zone force decay exponent.",
            new AcceptableValueRange<float>(0f, 5f),
            new ConfigurationManagerAttributes { Order = 1 }
        ));
        ZoneRadiusDecayScale.SettingChanged += ZoneParametersChanged;

        /*
         * Deboog
         */
        Config.Bind(debug, "Camera Coords", "", new ConfigDescription(
            "Displays the camera coordinates to aid positioning.",
            null,
            new ConfigurationManagerAttributes { Order = 4, CustomDrawer = CameraCoordsToggle }
        ));

        Config.Bind(debug, "Advection Grid", "", new ConfigDescription(
            "Displays information about the location system state.",
            null,
            new ConfigurationManagerAttributes { Order = 3, CustomDrawer = LocationSystemTelemetryToggle }
        ));

        Config.Bind(debug, "Movement Gizmos", "", new ConfigDescription(
            "Displays information about the movement system state.",
            null,
            new ConfigurationManagerAttributes { Order = 2, CustomDrawer = MovementTelemetryToggle }
        ));

        _loggingEnabled = Config.Bind(debug, "Enable Debug Logging (RESTART)", true, new ConfigDescription(
            "Duh. Requires restarting the game to take effect.",
            null,
            new ConfigurationManagerAttributes { Order = 1 }
        ));
    }

    private static void ZoneParametersChanged(object sender, EventArgs args)
    {
        Singleton<PhobosManager>.Instance?.LocationSystem.CalculateZones();
    }

    private static void CameraCoordsToggle(ConfigEntryBase entry)
    {
        if (GUILayout.Button("Toggle"))
        {
            var gameWorld = Singleton<GameWorld>.Instance;

            if (gameWorld == null)
                return;

            if (!gameWorld.TryGetComponent<CameraTelemetry>(out var telemetry))
            {
                gameWorld.GetOrAddComponent<CameraTelemetry>();
                return;
            }

            Destroy(telemetry);
        }
    }

    private static void LocationSystemTelemetryToggle(ConfigEntryBase entry)
    {
        if (GUILayout.Button("Toggle"))
        {
            var gameWorld = Singleton<GameWorld>.Instance;

            if (gameWorld == null)
                return;

            if (!gameWorld.TryGetComponent<ZoneTelemetry>(out var telemetry))
            {
                gameWorld.GetOrAddComponent<ZoneTelemetry>();
                return;
            }

            Destroy(telemetry);
        }

        if (GUILayout.Button("Reload Zones"))
        {
            Singleton<PhobosManager>.Instance?.LocationSystem.CalculateZones();
        }
    }

    private static void MovementTelemetryToggle(ConfigEntryBase entry)
    {
        if (GUILayout.Button("Toggle"))
        {
            var gameWorld = Singleton<GameWorld>.Instance;

            if (gameWorld == null)
                return;

            if (!gameWorld.TryGetComponent<MoveTelemetry>(out var telemetry))
            {
                gameWorld.GetOrAddComponent<MoveTelemetry>();
                return;
            }

            Destroy(telemetry);
        }
    }
}