using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AUModdedRegions
{
    [BepInPlugin("com.aumoddedregions", "AUModdedRegions", "1.0.0")]
    public class AUModdedRegionsPlugin : BasePlugin
    {
        private static readonly string[] OfficialDomains = new string[]
        {
            "innersloth.com",
            "among.us",
            "matchmaker.among.us",
            "matchmaker-as.among.us",
            "matchmaker-eu.among.us"
        };

        // La liste des 8 régions moddées à garantir dans le fichier
        private static readonly List<(string Name, string Host)> DefaultModdedRegions = new()
        {
            ("Niko233 (EU)", "https://au-eu.niko233.top"),
            ("Niko233 (AS)", "https://au-as.niko233.top"),
            ("Niko233 (NA)", "https://au-us.niko233.top"),
            ("Niko233 (CN)", "https://au-cn.niko233.top"),
            ("Modded NA", "https://aumods.org"),
            ("Modded EU", "https://au-eu.duikbo.at"),
            ("Modded AS", "https://au-as.duikbo.at"),
            ("Skeld.net", "https://play.skeld.net")
        };

        public override void Load()
        {
            ProcessAndMergeRegions();

            // Bloque le CleanAndMerge natif d'Among Us pour pas qu'il remette Innersloth
            Harmony.CreateAndPatchAll(typeof(PatchCleanAndMerge));
            Log.LogInfo("AUModdedRegions chargé avec succès !");
        }

        private void ProcessAndMergeRegions()
        {
            string regionFilePath = Path.Combine(Application.persistentDataPath, "regionInfo.json");

            try
            {
                JsonNode rootNode;

                // 1. Lire le fichier s'il existe, sinon créer une structure JSON vide
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

                // 2. Filtrer et supprimer uniquement les régions Innersloth
                for (int i = regionsArray.Count - 1; i >= 0; i--)
                {
                    string regionJson = regionsArray[i]?.ToJsonString() ?? "";
                    if (IsOfficialInnersloth(regionJson))
                    {
                        regionsArray.RemoveAt(i);
                    }
                }

                // 3. Ajouter les 8 régions moddées si elles ne sont pas déjà présentes
                foreach (var (name, host) in DefaultModdedRegions)
                {
                    if (!HasRegion(regionsArray, host))
                    {
                        regionsArray.Add(BuildRegionNode(name, host));
                    }
                }

                // 4. Mettre à jour et sauvegarder le fichier
                rootNode["Regions"] = regionsArray;
                
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(regionFilePath, rootNode.ToJsonString(options));

                Log.LogInfo("[AUModdedRegions] Fichier regionInfo.json mis à jour ! (Innersloth viré, tes régions custom + régions moddées conservées/ajoutées).");
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

        private JsonNode BuildRegionNode(string name, string host)
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
                        ["Port"] = 443,
                        ["UseDtls"] = false,
                        ["Players"] = 0,
                        ["ConnectionFailures"] = 0
                    }
                },
                ["TargetServer"] = null,
                ["TranslateName"] = 1003
            };
        }
    }

    [HarmonyPatch("ServerManager+JsonServerData", "CleanAndMerge")]
    public static class PatchCleanAndMerge
    {
        [HarmonyPrefix]
        public static bool Prefix() => false;
    }
}