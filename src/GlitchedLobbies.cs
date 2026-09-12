using System;
using System.Collections.Generic;

namespace RegionInstaller
{
    public static class GlitchedLobbies
    {
        public static void AddGlitchedLobbiesRegion(List<ParsedRegion> regions)
        {
            bool hasGl = regions.Exists(r => r.Address.Equals("augl.net", StringComparison.OrdinalIgnoreCase));
            if (!hasGl)
            {
                regions.Add(new ParsedRegion
                {
                    Name = "AUGL Codes",
                    Address = "augl.net",
                    Https = true,
                    Dtls = false,
                    Port1 = 443,
                    Port2 = 22023
                });
            }
        }
    }
}