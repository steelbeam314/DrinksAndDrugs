using UnityEngine;

namespace DrinksAndDrugs
{
    internal static class ItemIcons
    {
        private const int Size = 16;
        private const float PixelsPerUnit = 8f;

        public static Sprite Bottle(Color liquid)
        {
            return CreateSprite(DrawBottle(liquid));
        }

        public static Sprite BottleMask()
        {
            return CreateSprite(DrawBottleMask());
        }

        public static Sprite Syringe(Color liquid)
        {
            return CreateSprite(DrawSyringe(liquid));
        }

        private static Sprite CreateSprite(Color32[] pixels)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, Size, Size),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit);
        }

        private static Color32[] DrawBottle(Color liquid)
        {
            Color32[] pixels = ClearPixels();
            Color32 glass = new Color32(210, 220, 230, 230);
            Color32 outline = new Color32(28, 32, 38, 255);
            Color32 cap = new Color32(90, 90, 96, 255);
            Color32 fill = ToColor32(liquid, 230);

            FillRect(pixels, 5, 2, 6, 10, glass);
            FillRect(pixels, 6, 3, 4, 8, fill);
            FillRect(pixels, 6, 12, 4, 2, glass);
            FillRect(pixels, 6, 14, 4, 2, cap);
            DrawRect(pixels, 5, 2, 6, 12, outline);
            DrawRect(pixels, 6, 14, 4, 2, outline);
            return pixels;
        }

        private static Color32[] DrawBottleMask()
        {
            Color32[] pixels = ClearPixels();
            Color32 mask = new Color32(255, 255, 255, 255);
            FillRect(pixels, 6, 3, 4, 8, mask);
            return pixels;
        }

        private static Color32[] DrawSyringe(Color liquid)
        {
            Color32[] pixels = ClearPixels();
            Color32 body = new Color32(230, 236, 240, 255);
            Color32 outline = new Color32(28, 32, 38, 255);
            Color32 plunger = new Color32(70, 74, 80, 255);
            Color32 needle = new Color32(170, 176, 184, 255);
            Color32 fill = ToColor32(liquid, 230);

            FillRect(pixels, 3, 6, 9, 4, body);
            FillRect(pixels, 4, 7, 6, 2, fill);
            FillRect(pixels, 1, 6, 2, 4, plunger);
            FillRect(pixels, 12, 7, 3, 2, needle);
            DrawRect(pixels, 3, 6, 9, 4, outline);
            return pixels;
        }

        private static Color32[] ClearPixels()
        {
            var pixels = new Color32[Size * Size];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;
            return pixels;
        }

        private static void FillRect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                    SetPixel(pixels, px, py, color);
            }
        }

        private static void DrawRect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
        {
            for (int px = x; px < x + width; px++)
            {
                SetPixel(pixels, px, y, color);
                SetPixel(pixels, px, y + height - 1, color);
            }

            for (int py = y; py < y + height; py++)
            {
                SetPixel(pixels, x, py, color);
                SetPixel(pixels, x + width - 1, py, color);
            }
        }

        private static void SetPixel(Color32[] pixels, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size)
                return;

            pixels[y * Size + x] = color;
        }

        private static Color32 ToColor32(Color color, byte alpha)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
                alpha);
        }
    }
}
