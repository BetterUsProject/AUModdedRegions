using System;
using System.Collections.Generic;
using System.IO;

namespace RegionInstaller
{
    public static class ConfigFile
    {
        public static ConfigData LoadOrCreate(string configPath)
        {
            EnsureExists(configPath);
            return Parse(configPath);
        }

        private static void EnsureExists(string configPath)
        {
            if (!File.Exists(configPath))
            {
                string defaultConfig = @"KeepInnerslothRegions = false
# For information on how to create a glitched lobby, see this page: https://augl.net/download
GlitchedLobbiesRegion = false

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
                File.WriteAllText(configPath, defaultConfig);
                return;
            }

            EnsureSettingExists(configPath, "KeepInnerslothRegions", "KeepInnerslothRegions = false");
            EnsureSettingExists(configPath, "GlitchedLobbiesRegion", "# For information on how to create a glitched lobby, see this page: https://augl.net/download\nGlitchedLobbiesRegion = false");
        }

        private static void EnsureSettingExists(string filePath, string keyName, string contentToAdd)
        {
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith(keyName, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            var newLines = new List<string>(contentToAdd.Split('\n'));
            newLines.AddRange(lines);
            File.WriteAllLines(filePath, newLines);
        }

        private static ConfigData Parse(string filePath)
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
                        if (key == "keepinnerslothregions" && bool.TryParse(val, out bool keep))
                            configData.KeepInnerslothRegions = keep;
                        else if (key == "glitchedlobbiesregion" && bool.TryParse(val, out bool glitched))
                            configData.GlitchedLobbiesRegion = glitched;
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
    }
}