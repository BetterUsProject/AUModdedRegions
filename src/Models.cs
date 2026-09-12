using System;
using System.Collections.Generic;

namespace RegionInstaller
{
    public class ParsedRegion
    {
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public bool Https { get; set; } = true;
        public bool Dtls { get; set; } = false;
        public ushort Port1 { get; set; } = 443;
        public ushort Port2 { get; set; } = 22023;

        public ushort SelectedPort => Https ? Port1 : Port2;
        public string FullUrl => $"{(Https ? "https://" : "http://")}{Address}";
        public bool IsValid => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Address) && !(string.Equals(Name, "Default", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Address));
    }

    public class ConfigData
    {
        public bool KeepInnerslothRegions { get; set; } = false;
        public bool GlitchedLobbiesRegion { get; set; } = false;
        public List<ParsedRegion> Regions { get; set; } = new();
    }
}