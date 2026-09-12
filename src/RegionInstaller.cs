using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RegionInstaller
{
    [BepInPlugin(Id, Name, Version)]
    [BepInProcess("Among Us.exe")]
    public class RegionInstallerPlugin : BasePlugin
    {
        public const string Id = "com.nb1x.regioninstaller";
        public const string Name = "RegionInstaller";
        public const string Version = "1.2.0";

        public static string ConfigPath => Path.Combine(Paths.ConfigPath, "CustomRegions.cfg");
        public Harmony Harmony { get; } = new Harmony(Id);

        public override void Load()
        {
            Log.LogInfo($"[RegionInstaller] Loading {Name} v{Version}...");

            ConfigData configData = ConfigFile.LoadOrCreate(ConfigPath);

            if (configData.GlitchedLobbiesRegion)
            {
                configData.KeepInnerslothRegions = true;
                GlitchedLobbies.AddGlitchedLobbiesRegion(configData.Regions);
            }

            if (configData.KeepInnerslothRegions)
            {
                InnerslothRegions.AddInnerslothRegions(configData.Regions);
            }

            string regionJsonPath = Path.Combine(Application.persistentDataPath, "regionInfo.json");
            ProcessAndCleanRegionJson(regionJsonPath, configData);

            Harmony.PatchAll();

            SceneManager.add_sceneLoaded((Action<Scene, LoadSceneMode>)((scene, _) =>
            {
                if (scene.name == "MainMenu")
                {
                    Log.LogInfo("[RegionInstaller] MainMenu loaded, injecting regions...");
                    InjectRegions(configData.Regions);
                }
            }));
        }

        private void ProcessAndCleanRegionJson(string regionFilePath, ConfigData configData)
        {
            try
            {
                bool needsRewrite = true;
                JsonNode? rootNode = null;

                if (File.Exists(regionFilePath) && new FileInfo(regionFilePath).Length > 0)
                {
                    string content = File.ReadAllText(regionFilePath);
                    rootNode = JsonNode.Parse(content);
                    if (rootNode?["Regions"] is JsonArray existingRegions)
                    {
                        if (AreRegionsMatching(existingRegions, configData.Regions, configData.KeepInnerslothRegions))
                        {
                            needsRewrite = false;
                        }
                    }
                }

                if (needsRewrite)
                {
                    Log.LogInfo("[RegionInstaller] Rewriting regionInfo.json...");
                    rootNode = CreateEmptyRegionStructure();
                    var regionsArray = rootNode["Regions"]!.AsArray();

                    foreach (var reg in configData.Regions)
                    {
                        if (!reg.IsValid) continue;
                        regionsArray.Add(BuildRegionNode(reg.Name, reg.FullUrl, reg.SelectedPort, reg.Dtls));
                    }

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(regionFilePath, rootNode.ToJsonString(options));
                    Log.LogInfo("[RegionInstaller] regionInfo.json successfully updated!");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[RegionInstaller] Error processing regionInfo.json: {ex.Message}");
            }
        }

        private bool AreRegionsMatching(JsonArray existingRegions, List<ParsedRegion> parsedRegions, bool keepInnersloth)
        {
            var validCfgRegions = parsedRegions.FindAll(r => r.IsValid);

            int totalExpected = validCfgRegions.Count;
            int totalExisting = 0;

            foreach (var node in existingRegions)
            {
                if (node == null) continue;
                bool isOfficial = InnerslothRegions.IsOfficialInnersloth(node.ToJsonString());
                if (!isOfficial || keepInnersloth)
                {
                    totalExisting++;
                }
            }

            if (totalExisting != totalExpected) return false;

            foreach (var reg in validCfgRegions)
            {
                bool found = false;
                foreach (var node in existingRegions)
                {
                    if (node != null && node.ToJsonString().Contains(reg.FullUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            return true;
        }

        private static JsonNode CreateEmptyRegionStructure() => new JsonObject { ["CurrentRegionIdx"] = 0, ["Regions"] = new JsonArray() };

        private static JsonNode BuildRegionNode(string name, string host, ushort port, bool dtls)
        {
            return new JsonObject
            {
                ["$type"] = "StaticHttpRegionInfo, Assembly-CSharp",
                ["Name"] = name,
                ["PingServer"] = host,
                ["Servers"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["Name"] = "Http-1",
                        ["Ip"] = host,
                        ["Port"] = port,
                        ["UseDtls"] = dtls,
                        ["Players"] = 0,
                        ["ConnectionFailures"] = 0
                    }
                },
                ["TargetServer"] = null,
                ["TranslateName"] = 1003
            };
        }

        private void InjectRegions(List<ParsedRegion> parsedRegions)
        {
            ServerManager serverMngr = DestroyableSingleton<ServerManager>.Instance;
            if (serverMngr == null) return;

            foreach (var reg in parsedRegions)
            {
                if (!reg.IsValid) continue;

                var serverInfo = new ServerInfo("http-1", reg.FullUrl, reg.SelectedPort, reg.Dtls);
                var serversArray = new Il2CppReferenceArray<ServerInfo>(new ServerInfo[] { serverInfo });

                var regionInfo = new StaticHttpRegionInfo(reg.Name, (StringNames)1003, reg.FullUrl, serversArray);

                serverMngr.AddOrUpdateRegion(regionInfo.Cast<IRegionInfo>());
                Log.LogInfo($"[RegionInstaller] Region '{reg.Name}' injected into memory.");
            }
        }
    }

    [HarmonyPatch(typeof(ServerManager.JsonServerData), nameof(ServerManager.JsonServerData.CleanAndMerge))]
    public static class PatchCleanAndMerge
    {
        [HarmonyPrefix]
        public static bool Prefix() => false;
    }
}