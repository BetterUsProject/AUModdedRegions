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

namespace AUModdedRegions
{
    [BepInPlugin(Id, Name, Version)]
    [BepInProcess("Among Us.exe")]
    public class AUModdedRegionsPlugin : BasePlugin
    {
        public const string Id = "com.nb1x.aumoddedregions";
        public const string Name = "AUModdedRegions";
        public const string Version = "1.3.0";

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
            Log.LogInfo($"[AUModdedRegions] Lancement de {Name} v{Version}...");

            EnsureConfigFileExists();
            
            var parsedRegions = ParseConfigRegions(ConfigPath);
            ProcessAndCleanRegionJson(parsedRegions);

            Harmony.PatchAll();

            SceneManager.add_sceneLoaded((Action<Scene, LoadSceneMode>)((scene, _) =>
            {
                if (scene.name == "MainMenu")
                {
                    Log.LogInfo("[AUModdedRegions] Scène MainMenu détectée, injection des régions...");
                    InjectRegions(parsedRegions);
                }
            }));
        }

        private static void EnsureConfigFileExists()
        {
            if (File.Exists(ConfigPath)) return;

            string defaultConfig = @"[Region 1]
Name = Niko_NA
Adress = au-us.niko233.top
Https = true
Dtls = false
Port = 443, 22023

[Region 2]
Name = Niko_EU
Adress = au-eu.niko233.top
Https = true
Dtls = false
Port = 443, 22023

[Region 3]
Name = Niko_AS
Adress = au-as.niko233.top
Https = true
Dtls = false
Port = 443, 22023

[Region 4]
Name = Niko_CN
Adress = au-cn.niko233.top
Https = true
Dtls = false
Port = 443, 22023

[Region 5]
Name = Modded NA
Adress = aumods.org
Https = true
Dtls = false
Port = 443, 22023

[Region 6]
Name = Modded EU
Adress = au-eu.duikbo.at
Https = true
Dtls = false
Port = 443, 22023

[Region 7]
Name = Modded AS
Adress = au-as.duikbo.at
Https = true
Dtls = false
Port = 443, 22023

[Region 8]
Name = Default
Adress =
Https =
Dtls =
Port = 443, 22023
";
            File.WriteAllText(ConfigPath, defaultConfig);
        }

        private void ProcessAndCleanRegionJson(List<ParsedRegion> parsedRegions)
        {
            string regionFilePath = Path.Combine(Application.persistentDataPath, "regionInfo.json");

            try
            {
                JsonNode rootNode;

                if (File.Exists(regionFilePath) && new FileInfo(regionFilePath).Length > 0)
                {
                    string content = File.ReadAllText(regionFilePath);
                    rootNode = JsonNode.Parse(content) ?? CreateEmptyRegionStructure();
                }
                else
                {
                    rootNode = CreateEmptyRegionStructure();
                }

                JsonArray regionsArray = rootNode["Regions"]?.AsArray() ?? new JsonArray();

                for (int i = regionsArray.Count - 1; i >= 0; i--)
                {
                    string regionJson = regionsArray[i]?.ToJsonString() ?? "";
                    if (IsOfficialInnersloth(regionJson))
                    {
                        regionsArray.RemoveAt(i);
                    }
                }

                foreach (var reg in parsedRegions)
                {
                    if (string.Equals(reg.Name, "Default", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(reg.Adress))
                        continue;

                    if (string.IsNullOrWhiteSpace(reg.Name) || string.IsNullOrWhiteSpace(reg.Adress))
                        continue;

                    string protocol = reg.Https ? "https://" : "http://";
                    string host = $"{protocol}{reg.Adress}";

                    if (!HasRegion(regionsArray, host))
                    {
                        regionsArray.Add(BuildRegionNode(reg.Name, host, reg.Https ? reg.Port1 : reg.Port2, reg.Dtls));
                    }
                }

                rootNode["Regions"] = regionsArray;

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(regionFilePath, rootNode.ToJsonString(options));

                Log.LogInfo("[AUModdedRegions] Fichier regionInfo.json nettoyé et mis à jour !");
            }
            catch (Exception ex)
            {
                Log.LogError($"[AUModdedRegions] Erreur lors du traitement du fichier regionInfo.json : {ex.Message}");
            }
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

        private bool HasRegion(JsonArray regionsArray, string host)
        {
            foreach (var node in regionsArray)
            {
                if (node != null && node.ToJsonString().Contains(host, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
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
                if (string.Equals(reg.Name, "Default", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(reg.Adress))
                    continue;

                if (string.IsNullOrWhiteSpace(reg.Name) || string.IsNullOrWhiteSpace(reg.Adress))
                    continue;

                ushort selectedPort = reg.Https ? reg.Port1 : reg.Port2;
                string protocol = reg.Https ? "https://" : "http://";
                string fullUrl = $"{protocol}{reg.Adress}";

                var serverInfo = new ServerInfo("http-1", fullUrl, selectedPort, reg.Dtls);
                var serversArray = new Il2CppReferenceArray<ServerInfo>(new ServerInfo[] { serverInfo });

                var regionInfo = new StaticHttpRegionInfo(reg.Name, (StringNames)1003, fullUrl, serversArray);

                serverMngr.AddOrUpdateRegion(regionInfo.Cast<IRegionInfo>());
                Log.LogInfo($"[AUModdedRegions] Région '{reg.Name}' injectée en mémoire.");
            }
        }

        private static List<ParsedRegion> ParseConfigRegions(string filePath)
        {
            var result = new List<ParsedRegion>();
            if (!File.Exists(filePath)) return result;

            ParsedRegion? current = null;

            foreach (var line in File.ReadAllLines(filePath))
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    if (current != null) result.Add(current);
                    current = new ParsedRegion();
                    continue;
                }

                if (current == null || !trimmed.Contains('=')) continue;

                var parts = trimmed.Split(new[] { '=' }, 2);
                string key = parts[0].Trim().ToLowerInvariant();
                string val = parts[1].Trim();

                switch (key)
                {
                    case "name":
                        current.Name = val;
                        break;
                    case "adress":
                    case "address":
                        current.Adress = val;
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

            if (current != null) result.Add(current);

            return result;
        }

        private class ParsedRegion
        {
            public string Name { get; set; } = "";
            public string Adress { get; set; } = "";
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