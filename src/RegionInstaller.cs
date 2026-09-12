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

        private static readonly string[] OfficialDomains = new string[]
        {
            "innersloth.com",
            "among.us",
            "matchmaker.among.us",
            "matchmaker-as.among.us",
            "matchmaker-eu.among.us"
        };

        public override void Load()
        {
            Log.LogInfo($"[RegionInstaller] Loading {Name} v{Version}...");

            EnsureConfigFileExists();
            
            var configData = ParseConfig(ConfigPath);
            
            if (configData.KeepInnerslothRegions)
            {
                AddInnerslothRegions(configData.Regions);
            }

            ProcessAndCleanRegionJson(configData.Regions, configData.KeepInnerslothRegions);

            Harmony.PatchAll();

            SceneManager.add_sceneLoaded((Action<Scene, LoadSceneMode>)((scene, _) =>
            {
                if (scene.name == "MainMenu")
                {
                    Log.LogInfo("[RegionInstaller] MainMenu scene detected, injecting regions...");
                    InjectRegions(configData.Regions);
                }
            }));
        }

        private static void AddInnerslothRegions(List<ParsedRegion> regions)
        {
            bool hasEu = regions.Exists(r => r.Address.Equals("matchmaker-eu.among.us", StringComparison.OrdinalIgnoreCase));
            bool hasAs = regions.Exists(r => r.Address.Equals("matchmaker-as.among.us", StringComparison.OrdinalIgnoreCase));
            bool hasNa = regions.Exists(r => r.Address.Equals("matchmaker.among.us", StringComparison.OrdinalIgnoreCase));

            int insertIndex = regions.FindIndex(r => string.Equals(r.Name, "Default", StringComparison.OrdinalIgnoreCase));
            if (insertIndex < 0) insertIndex = regions.Count;

            if (!hasEu)
            {
                regions.Insert(insertIndex, new ParsedRegion { Name = "Europe", Address = "matchmaker-eu.among.us", Https = true, Dtls = true, Port1 = 443, Port2 = 22023 });
                insertIndex++;
            }
            if (!hasAs)
            {
                regions.Insert(insertIndex, new ParsedRegion { Name = "Asia", Address = "matchmaker-as.among.us", Https = true, Dtls = true, Port1 = 443, Port2 = 22023 });
                insertIndex++;
            }
            if (!hasNa)
            {
                regions.Insert(insertIndex, new ParsedRegion { Name = "North America", Address = "matchmaker.among.us", Https = true, Dtls = true, Port1 = 443, Port2 = 22023 });
            }
        }

        private static void EnsureConfigFileExists()
        {
            if (File.Exists(ConfigPath))
            {
                EnsureSettingExistsInConfig(ConfigPath);
                return;
            }

            string defaultConfig = @"KeepInnerslothRegions = false

[Region 1]
Name = Skeld.net
Address = play.skeld.net
Https = true
Dtls = false
Port = 443, 22023

[Region 2]
Name = Niko_NA
Address = au-us.niko233.top
Https = true
Dtls = false
Port = 443, 22023

[Region 3]
Name = Niko_EU
Address = au-eu.niko233.top
Https = true
Dtls = false
Port = 443, 22023

[Region 4]
Name = Niko_AS
Address = au-as.niko233.top
Https = true
Dtls = false
Port = 443, 22023

[Region 5]
Name = Modded NA
Address = aumods.org
Https = true
Dtls = false
Port = 443, 22023

[Region 6]
Name = Modded EU
Address = au-eu.duikbo.at
Https = true
Dtls = false
Port = 443, 22023

[Region 7]
Name = Modded AS
Address = au-as.duikbo.at
Https = true
Dtls = false
Port = 443, 22023

[Region 8]
Name = Default
Address =
Https =
Dtls =
Port = 443, 22023
";
            File.WriteAllText(ConfigPath, defaultConfig);
        }

        private static void EnsureSettingExistsInConfig(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            bool found = false;

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("KeepInnerslothRegions", StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                var newLines = new List<string> { "KeepInnerslothRegions = false" };
                newLines.AddRange(lines);
                File.WriteAllLines(filePath, newLines);
            }
        }

        private void ProcessAndCleanRegionJson(List<ParsedRegion> parsedRegions, bool keepInnersloth)
        {
            string regionFilePath = Path.Combine(Application.persistentDataPath, "regionInfo.json");

            try
            {
                bool needsRewrite = false;
                JsonNode? rootNode = null;

                if (!File.Exists(regionFilePath) || new FileInfo(regionFilePath).Length == 0)
                {
                    needsRewrite = true;
                }
                else
                {
                    string content = File.ReadAllText(regionFilePath);
                    rootNode = JsonNode.Parse(content);
                    if (rootNode == null)
                    {
                        needsRewrite = true;
                    }
                    else
                    {
                        JsonArray existingRegions = rootNode["Regions"]?.AsArray() ?? new JsonArray();
                        if (!AreRegionsMatching(existingRegions, parsedRegions, keepInnersloth))
                        {
                            needsRewrite = true;
                        }
                    }
                }

                if (needsRewrite)
                {
                    Log.LogInfo("[RegionInstaller] Differences detected in regionInfo.json (or file missing/empty). Rewriting completely...");
                    rootNode = CreateEmptyRegionStructure();
                    JsonArray regionsArray = rootNode["Regions"]?.AsArray() ?? new JsonArray();

                    foreach (var reg in parsedRegions)
                    {
                        if (string.Equals(reg.Name, "Default", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(reg.Address))
                            continue;

                        if (string.IsNullOrWhiteSpace(reg.Name) || string.IsNullOrWhiteSpace(reg.Address))
                            continue;

                        string protocol = reg.Https ? "https://" : "http://";
                        string host = $"{protocol}{reg.Address}";

                        regionsArray.Add(BuildRegionNode(reg.Name, host, reg.Https ? reg.Port1 : reg.Port2, reg.Dtls));
                    }
                    rootNode["Regions"] = regionsArray;
                }

                if (rootNode != null && rootNode["Regions"] is JsonArray currentRegionsArray)
                {
                    if (!keepInnersloth)
                    {
                        for (int i = currentRegionsArray.Count - 1; i >= 0; i--)
                        {
                            string regionJson = currentRegionsArray[i]?.ToJsonString() ?? "";
                            if (IsOfficialInnersloth(regionJson))
                            {
                                currentRegionsArray.RemoveAt(i);
                            }
                        }
                    }
                    rootNode["Regions"] = currentRegionsArray;
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(regionFilePath, rootNode?.ToJsonString(options));

                Log.LogInfo("[RegionInstaller] regionInfo.json successfully verified and updated!");
            }
            catch (Exception ex)
            {
                Log.LogError($"[RegionInstaller] Error while processing regionInfo.json: {ex.Message}");
            }
        }

        private bool AreRegionsMatching(JsonArray existingRegions, List<ParsedRegion> parsedRegions, bool keepInnersloth)
        {
            var validCfgRegions = parsedRegions.FindAll(r => !(string.Equals(r.Name, "Default", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(r.Address)) && !string.IsNullOrWhiteSpace(r.Name));

            int totalExpected = validCfgRegions.Count;
            int totalExisting = 0;

            foreach (var node in existingRegions)
            {
                if (node != null)
                {
                    bool isOfficial = IsOfficialInnersloth(node.ToJsonString());
                    if (!isOfficial || keepInnersloth)
                    {
                        totalExisting++;
                    }
                }
            }

            if (totalExisting != totalExpected) return false;

            foreach (var reg in validCfgRegions)
            {
                string protocol = reg.Https ? "https://" : "http://";
                string host = $"{protocol}{reg.Address}";
                bool found = false;

                foreach (var node in existingRegions)
                {
                    if (node != null && node.ToJsonString().Contains(host, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found) return false;
            }

            return true;
        }

        private bool IsOfficialInnersloth(string json)
        {
            foreach (var domain in OfficialDomains)
            {
                if (json.Contains(domain, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private JsonNode CreateEmptyRegionStructure()
        {
            return new JsonObject
            {
                ["CurrentRegionIdx"] = 0,
                ["Regions"] = new JsonArray()
            };
        }

        private JsonNode BuildRegionNode(string name, string host, ushort port, bool dtls)
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
                if (string.Equals(reg.Name, "Default", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(reg.Address))
                    continue;

                if (string.IsNullOrWhiteSpace(reg.Name) || string.IsNullOrWhiteSpace(reg.Address))
                    continue;

                ushort selectedPort = reg.Https ? reg.Port1 : reg.Port2;
                string protocol = reg.Https ? "https://" : "http://";
                string fullUrl = $"{protocol}{reg.Address}";

                var serverInfo = new ServerInfo("http-1", fullUrl, selectedPort, reg.Dtls);
                var serversArray = new Il2CppReferenceArray<ServerInfo>(new ServerInfo[] { serverInfo });

                var regionInfo = new StaticHttpRegionInfo(reg.Name, (StringNames)1003, fullUrl, serversArray);

                serverMngr.AddOrUpdateRegion(regionInfo.Cast<IRegionInfo>());
                Log.LogInfo($"[RegionInstaller] Region '{reg.Name}' injected into memory.");
            }
        }

        private static ConfigData ParseConfig(string filePath)
        {
            var configData = new ConfigData();
            if (!File.Exists(filePath)) return configData;

            ParsedRegion? current = null;

            foreach (var line in File.ReadAllLines(filePath))
            {
                string trimmed = line.Trim();

                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    if (current != null) configData.Regions.Add(current);
                    current = new ParsedRegion();
                    continue;
                }

                if (trimmed.Contains('='))
                {
                    var parts = trimmed.Split(new[] { '=' }, 2);
                    string key = parts[0].Trim().ToLowerInvariant();
                    string val = parts[1].Trim();

                    if (current == null)
                    {
                        if (key == "keepinnerslothregions")
                        {
                            if (bool.TryParse(val, out bool keep))
                            {
                                configData.KeepInnerslothRegions = keep;
                            }
                        }
                    }
                    else
                    {
                        switch (key)
                        {
                            case "name":
                                current.Name = val;
                                break;
                            case "address":
                            case "adress":
                                current.Address = val;
                                break;
                            case "https":
                                bool.TryParse(val, out bool https);
                                current.Https = https;
                                break;
                            case "dtls":
                                bool.TryParse(val, out bool dtls);
                                current.Dtls = dtls;
                                break;
                            case "port":
                                var ports = val.Split(',');
                                if (ports.Length > 0 && ushort.TryParse(ports[0].Trim(), out ushort p1))
                                    current.Port1 = p1;
                                if (ports.Length > 1 && ushort.TryParse(ports[1].Trim(), out ushort p2))
                                    current.Port2 = p2;
                                break;
                        }
                    }
                }
            }

            if (current != null) configData.Regions.Add(current);

            return configData;
        }

        private class ConfigData
        {
            public bool KeepInnerslothRegions { get; set; } = false;
            public List<ParsedRegion> Regions { get; set; } = new List<ParsedRegion>();
        }

        private class ParsedRegion
        {
            public string Name { get; set; } = "";
            public string Address { get; set; } = "";
            public bool Https { get; set; } = true;
            public bool Dtls { get; set; } = false;
            public ushort Port1 { get; set; } = 443;
            public ushort Port2 { get; set; } = 22023;
        }
    }

    [HarmonyPatch(typeof(ServerManager.JsonServerData), nameof(ServerManager.JsonServerData.CleanAndMerge))]
    public static class PatchCleanAndMerge
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            return false;
        }
    }
}