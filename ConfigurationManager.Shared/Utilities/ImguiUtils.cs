using System;
using UnityEngine;

namespace ConfigurationManager.Utilities
{
    internal static class ImguiUtils
    {
        private static Texture2D _tooltipBg;
        private static Texture2D _windowBackground;

        public static void DrawWindowBackground(Rect position)
        {
            if (!_windowBackground)
            {
                _windowBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                _windowBackground.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 1));
                _windowBackground.Apply();
            }

            GUI.DrawTexture(position, _windowBackground);
        }

        public static void DrawContolBackground(Rect position, Color color = default)
        {
            if (!_tooltipBg)
            {
                _tooltipBg = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                _tooltipBg.SetPixel(0, 0, Color.black);
                _tooltipBg.Apply();
            }

            GUI.DrawTexture(position, _tooltipBg);
        }
    }
}
