using SPTarkov.Server.Core.Loaders; 
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.External;
using SPTarkov.Server.Web;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Servers; // For DatabaseServer
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Phobos.Server
{
    // Metadata Record - Purely for Loader identification
    public record ModMetadata : AbstractModMetadata, IModWebMetadata
    {
        public override string ModGuid { get; init; } = "com.janky.phobos-server";
        public override string Name { get; init; } = "Phobos Server";
        public override string Author { get; init; } = "Janky-Phobos";
        public override SemanticVersioning.Version Version { get; init; } = new SemanticVersioning.Version("1.0.0");
        public override SemanticVersioning.Range SptVersion { get; init; } = new SemanticVersioning.Range(">=4.0.0");
        public override string Url { get; init; } = "";
        public override string License { get; init; } = "MIT";
        
        public override List<string> Contributors { get; init; } = new List<string>();
        public override List<string> Incompatibilities { get; init; } = new List<string>();
        public override bool? IsBundleMod { get; init; } = false;
        public override Dictionary<string, SemanticVersioning.Range> ModDependencies { get; init; } = new Dictionary<string, SemanticVersioning.Range>();
    }

    // Logic Class - Instantiated by DI Container via [Injectable]
    [Injectable]
    public class PhobosServerMod : IOnLoad
    {
        private readonly DatabaseServer _databaseServer;

        public PhobosServerMod(DatabaseServer databaseServer)
        {
            _databaseServer = databaseServer;
        }

        public Task OnLoad()
        {
            // Run in background to avoid blocking server startup and wait for DB
            Task.Run(() => ProcessDiscardLimits());
            return Task.CompletedTask;
        }

        private async Task ProcessDiscardLimits()
        {
            int maxRetries = 60; // 60 seconds
            int delay = 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (_databaseServer == null) return;

                    // Attempt to get tables
                    object tables = _databaseServer.GetTables();
                    if (tables == null)
                    {
                        await Task.Delay(delay);
                        continue;
                    }

                    Type tablesType = tables.GetType();
                    
                    // Globals
                    object globals = null;
                    PropertyInfo globalsProp = tablesType.GetProperty("Globals");
                    if (globalsProp != null) globals = globalsProp.GetValue(tables);
                    else { FieldInfo gf = tablesType.GetField("Globals"); if (gf != null) globals = gf.GetValue(tables); }

                    if (globals == null) 
                    {
                        await Task.Delay(delay);
                        continue;
                    }

                    // Config (Found via dump: Property is named 'Configuration')
                    Type globalsType = globals.GetType();
                    PropertyInfo configProp = globalsType.GetProperty("Configuration");
                    object config = null;
                    if (configProp != null) config = configProp.GetValue(globals);
                    else { FieldInfo cf = globalsType.GetField("Configuration"); if (cf != null) config = cf.GetValue(globals); }
                    
                    // Fallback to 'Config' just in case
                    if (config == null)
                    {
                         configProp = globalsType.GetProperty("Config");
                         if (configProp != null) config = configProp.GetValue(globals);
                    }

                    if (config == null) 
                    {
                        await Task.Delay(delay);
                        continue;
                    }

                    Type configType = config.GetType();
                    PropertyInfo discardProp = configType.GetProperty("DiscardLimitsEnabled");
                    FieldInfo discardField = configType.GetField("DiscardLimitsEnabled");

                    bool modified = false;
                    if (discardProp != null)
                    {
                        discardProp.SetValue(config, false);
                        modified = true;
                    }
                    else if (discardField != null)
                    {
                        discardField.SetValue(config, false);
                        modified = true;
                    }

                    if (modified)
                    {
                        Console.WriteLine("[Phobos-Server] Discard limits disabled successfully.");
                        return; // Done
                    }
                    else 
                    {
                        Console.WriteLine("[Phobos-Server] WARNING: Could not find DiscardLimitsEnabled property/field.");
                        return; // Stop retrying if structure is wrong
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Phobos-Server] Error setting discard limits: {ex.Message}");
                }
                
                await Task.Delay(delay);
            }
            Console.WriteLine("[Phobos-Server] Timeout waiting for Database.");
        }
    }
}
