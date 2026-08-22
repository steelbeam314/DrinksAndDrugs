using UnityEngine;

namespace DrinksAndDrugs
{
    internal static class MoodleIcons
    {
        private static Sprite _blank;

        /// <summary>
        /// CUCoreLib ignores moodles with a null sprite, so use a 1x1 transparent icon instead.
        /// </summary>
        public static Sprite Blank => _blank ??= CreateBlankSprite();

        private static Sprite CreateBlankSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.clear);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                100f);
        }
    }
}
