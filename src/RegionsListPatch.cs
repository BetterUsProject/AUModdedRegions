using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

namespace RegionInstaller
{
    [HarmonyPatch(typeof(ServerDropdown), nameof(ServerDropdown.FillServerOptions))]
    public static class ServerDropdownPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ServerDropdown __instance)
        {
            if (__instance == null || ServerManager.Instance?.AvailableRegions == null)
                return true;

            var regions = ServerManager.Instance.AvailableRegions.ToList();
            if (regions.Count == 0) return true;

            bool isFindGame = SceneManager.GetActiveScene().name == "FindAGame";
            int maxPerColumn = isFindGame ? 7 : 6;
            float columnWidth = 3.2f;
            float rowSpacing = 0.42f;

            int totalRegions = regions.Count;
            int totalColumns = Mathf.Max(1, Mathf.CeilToInt((float)totalRegions / maxPerColumn));
            int maxRows = Mathf.Min(totalRegions, maxPerColumn);

            float bgWidth = totalColumns > 1 ? (columnWidth * totalColumns) + 1.2f : 5.0f;
            float bgHeight = (maxRows * rowSpacing) + 0.9f;

            // X inchangé, Y remonté un peu
            float startX = -0.1f; 
            float startY = __instance.y_posButton - (isFindGame ? 0.15f : 0.05f);

            if (__instance.background != null)
            {
                __instance.background.size = new Vector2(bgWidth, bgHeight);
                float xOffset = (totalColumns - 1) * (columnWidth / 2f);
                __instance.background.transform.localPosition = new Vector3(xOffset, __instance.initialYPos - (bgHeight - 1.2f) / 2f, 0f);
            }

            int index = 0;
            foreach (var region in regions)
            {
                if (region == null) continue;

                var button = __instance.ButtonPool.Get<ServerListButton>();
                if (button == null) continue;

                int column = index / maxPerColumn;
                int row = index % maxPerColumn;

                float xPos = startX + (column * columnWidth);
                float yPos = startY - (row * rowSpacing);

                button.transform.localPosition = new Vector3(xPos, yPos, -1f);
                button.transform.localScale = Vector3.one;

                if (button.Text != null)
                {
                    button.Text.enableAutoSizing = false;
                    button.Text.fontSize = 3.5f;
                    button.Text.text = region.Name;
                    button.Text.ForceMeshUpdate(false, false);
                }

                if (button.Button != null)
                {
                    button.Button.OnClick.RemoveAllListeners();
                    IRegionInfo capturedRegion = region;
                    button.Button.OnClick.AddListener((System.Action)(() => __instance.ChooseOption(capturedRegion)));
                    __instance.controllerSelectable.Add(button.Button);
                }

                index++;
            }

            return false;
        }
    }
}