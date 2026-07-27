using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using System;
using System.IO;
using System.Text;

namespace AUModdedRegions
{
    [BepInPlugin("com.aumoddedregions", "AUModdedRegions", "1.0.0")]
    public class AUModdedRegionsPlugin : BasePlugin
    {
        // Contenu JSON par défaut injecté si besoin
        private const string DEFAULT_REGION_JSON = @"{
  ""CurrentRegionIdx"": 0,
  ""Regions"": [
    {
      ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"",
      ""Name"": ""Niko233(NA)"",
      ""PingServer"": ""https://au-us.niko233.top"",
      ""Servers"": [
        {
          ""Name"": ""http-1"",
          ""Ip"": ""https://au-us.niko233.top"",
          ""Port"": 443,
          ""UseDtls"": false,
          ""Players"": 0,
          ""ConnectionFailures"": 0
        }
      ],
      ""TargetServer"": null,
      ""TranslateName"": 1003
    },
    {
      ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"",
      ""Name"": ""Niko233(EU)"",
      ""PingServer"": ""https://au-eu.niko233.top"",
      ""Servers"": [
        {
          ""Name"": ""http-1"",
          ""Ip"": ""https://au-eu.niko233.top"",
          ""Port"": 443,
          ""UseDtls"": false,
          ""Players"": 0,
          ""ConnectionFailures"": 0
        }
      ],
      ""TargetServer"": null,
      ""TranslateName"": 1003
    },
    {
      ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"",
      ""Name"": ""Niko233(AS)"",
      ""PingServer"": ""https://au-as.niko233.top"",
      ""Servers"": [
        {
          ""Name"": ""http-1"",
          ""Ip"": ""https://au-as.niko233.top"",
          ""Port"": 443,
          ""UseDtls"": false,
          ""Players"": 0,
          ""ConnectionFailures"": 0
        }
      ],
      ""TargetServer"": null,
      ""TranslateName"": 1003
    },
    {
      ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"",
      ""Name"": ""Niko233(CN1)"",
      ""PingServer"": ""https://au-cn.niko233.top"",
      ""Servers"": [
        {
          ""Name"": ""http-1"",
          ""Ip"": ""https://au-cn.niko233.top"",
          ""Port"": 443,
          ""UseDtls"": false,
          ""Players"": 0,
          ""ConnectionFailures"": 0
        }
      ],
      ""TargetServer"": null,
      ""TranslateName"": 1003
    },
    {
      ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"",
      ""Name"": ""Modded EU (MEU)"",
      ""PingServer"": ""https://au-eu.duikbo.at"",
      ""Servers"": [
        {
          ""Name"": ""Http-1"",
          ""Ip"": ""https://au-eu.duikbo.at"",
          ""Port"": 443,
          ""UseDtls"": false,
          ""Players"": 0,
          ""ConnectionFailures"": 0
        }
      ],
      ""TargetServer"": null,
      ""TranslateName"": 1003
    },
    {
      ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"",
      ""Name"": ""Modded NA (MNA)"",
      ""PingServer"": ""https://aumods.org"",
      ""Servers"": [
        {
          ""Name"": ""Http-1"",
          ""Ip"": ""https://aumods.org"",
          ""Port"": 443,
          ""UseDtls"": false,
          ""Players"": 0,
          ""ConnectionFailures"": 0
        }
      ],
      ""TargetServer"": null,
      ""TranslateName"": 1003
    },
    {
      ""$type"": ""StaticHttpRegionInfo, Assembly-CSharp"",
      ""Name"": ""Modded Asia (MAS)"",
      ""PingServer"": ""https://au-as.duikbo.at"",
      ""Servers"": [
        {
          ""Name"": ""Http-1"",
          ""Ip"": ""https://au-as.duikbo.at"",
          ""Port"": 443,
          ""UseDtls"": false,
          ""Players"": 0,
          ""ConnectionFailures"": 0
        }
      ],
      ""TargetServer"": null,
      ""TranslateName"": 1003
    }
  ]
}";

        // Liste des sous-domaines / domaines officiels InnerSloth à filtrer
        private static readonly string[] OfficialDomains = new string[]
        {
            "innersloth.com",
            "among.us",
            "matchmaker.among.us"
        };

        public override void Load()
        {
            ProcessRegionFile();

            // Patch Harmony pour intercepter CleanAndMerge et bloquer les serveurs officiels
            Harmony.CreateAndPatchAll(typeof(PatchCleanAndMerge));
            Log.LogInfo("AUModdedRegions chargé avec succès !");
        }

        private void ProcessRegionFile()
        {
            string regionFilePath = Path.Combine(Application.persistentDataPath, "regionInfo.json");

            try
            {
                // CAS 1 : Fichier inexistant ou vide
                if (!File.Exists(regionFilePath) || new FileInfo(regionFilePath).Length == 0)
                {
                    Log.LogInfo("[AUModdedRegions] regionInfo.json inexistant/vide. Génération du fichier custom...");
                    File.WriteAllText(regionFilePath, DEFAULT_REGION_JSON);
                    return;
                }

                string content = File.ReadAllText(regionFilePath);
                bool containsOfficial = CheckContainsOfficial(content);
                bool containsCustom = CheckContainsCustom(content);

                // CAS 2 : Contient seulement les serveurs officiels -> Remplacement
                if (containsOfficial && !containsCustom)
                {
                    Log.LogInfo("[AUModdedRegions] Uniquement des serveurs officiels détectés. Remplacement par le JSON custom...");
                    File.WriteAllText(regionFilePath, DEFAULT_REGION_JSON);
                }
                // CAS 3 : Contient officiels ET d'autres serveurs -> Suppression des officiels uniquement
                else if (containsOfficial && containsCustom)
                {
                    Log.LogInfo("[AUModdedRegions] Serveurs officiels et custom détectés. Nettoyage des serveurs officiels...");
                    string cleanedContent = RemoveOfficialRegions(content);
                    File.WriteAllText(regionFilePath, cleanedContent);
                }
                else
                {
                    Log.LogInfo("[AUModdedRegions] Fichier regionInfo.json déjà propre.");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[AUModdedRegions] Erreur lors du traitement du fichier regionInfo.json : {ex.Message}");
            }
        }

        private bool CheckContainsOfficial(string json)
        {
            foreach (var domain in OfficialDomains)
            {
                if (json.Contains(domain, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private bool CheckContainsCustom(string json)
        {
            return json.Contains("niko233.top", StringComparison.OrdinalIgnoreCase) ||
                   json.Contains("duikbo.at", StringComparison.OrdinalIgnoreCase) ||
                   json.Contains("aumods.org", StringComparison.OrdinalIgnoreCase);
        }

        private string RemoveOfficialRegions(string json)
        {
            string[] lines = json.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            StringBuilder sb = new StringBuilder();

            foreach (var line in lines)
            {
                bool isOfficialLine = false;
                foreach (var domain in OfficialDomains)
                {
                    if (line.Contains(domain, StringComparison.OrdinalIgnoreCase))
                    {
                        isOfficialLine = true;
                        break;
                    }
                }

                if (!isOfficialLine)
                {
                    sb.AppendLine(line);
                }
            }
            return sb.ToString();
        }
    }

    // Bloque le CleanAndMerge natif au démarrage
    [HarmonyPatch("ServerManager+JsonServerData", "CleanAndMerge")]
    public static class PatchCleanAndMerge
    {
        [HarmonyPrefix]
        public static bool Prefix() => false;
    }
}