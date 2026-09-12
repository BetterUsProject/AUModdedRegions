using System;
using System.Collections.Generic;

namespace RegionInstaller
{
    public static class InnerslothRegions
    {
        public static readonly string[] OfficialDomains = new string[]
        {
            "innersloth.com",
            "among.us",
            "matchmaker.among.us",
            "matchmaker-as.among.us",
            "matchmaker-eu.among.us"
        };

        public static void AddInnerslothRegions(List<ParsedRegion> regions)
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

        public static bool IsOfficialInnersloth(string json)
        {
            foreach (var domain in OfficialDomains)
            {
                if (json.Contains(domain, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}