using UnityEngine;

namespace DrinksAndDrugs
{
    internal static class ItemIcons
    {
        private const int Size = 16;
        private const int JarWidth = 6;
        private const int JarHeight = 12;
        private const float PixelsPerUnit = 8f;

        public static Sprite Bottle(Color liquid)
        {
            return CreateSprite(DrawBottle(liquid), Size, Size);
        }

        public static Sprite BottleMask()
        {
            return CreateSprite(DrawBottleMask(), Size, Size);
        }

        public static Sprite Syringe(Color liquid)
        {
            return CreateSprite(DrawSyringe(liquid), Size, Size);
        }

        public static Sprite JarMask()
        {
            return CreateSprite(DrawJarMask(), Size, Size);
        }

        /// <summary>
        /// Fill mask aligned to the embedded 6x12 pickle jar sprites.
        /// </summary>
        public static Sprite JarMaskAsset()
        {
            return CreateSprite(DrawJarMaskAsset(), JarWidth, JarHeight);
        }

        /// <summary>
        /// Tight crop of a jar sprite so empty padding is not part of inventory sizing.
        /// </summary>
        public static Sprite CropToOpaque(Sprite jarSprite)
        {
            if (jarSprite == null)
                return null;

            int width = Mathf.Max(1, Mathf.RoundToInt(jarSprite.rect.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(jarSprite.rect.height));
            int minX = 0;
            int minY = 0;
            int maxX = width - 1;
            int maxY = height - 1;
            if (!TryOpaqueBounds(jarSprite, width, height, ref minX, ref minY, ref maxX, ref maxY))
                return jarSprite;

            int cropWidth = maxX - minX + 1;
            int cropHeight = maxY - minY + 1;
            if (cropWidth == width && cropHeight == height)
                return jarSprite;

            Color[] source = ReadSpritePixels(jarSprite, width, height);
            Color32[] pixels = ClearPixels(cropWidth, cropHeight);
            if (source != null)
            {
                for (int y = 0; y < cropHeight; y++)
                {
                    for (int x = 0; x < cropWidth; x++)
                        pixels[y * cropWidth + x] = source[(minY + y) * width + (minX + x)];
                }
            }

            return CreateSprite(pixels, cropWidth, cropHeight, jarSprite.pixelsPerUnit, new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Fill mask matching the jar sprite. Only the opaque jar body is filled so
        /// empty padding does not leak. Inset keeps a glass rim so world walls are
        /// not covered by the fill sitting behind the sprite.
        /// </summary>
        public static Sprite JarMaskMatching(Sprite jarSprite, int insetX = 0, int insetBottom = 0, int insetTop = 0)
        {
            if (jarSprite == null)
                return JarMaskAsset();

            int width = Mathf.Max(1, Mathf.RoundToInt(jarSprite.rect.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(jarSprite.rect.height));
            int minX = 0;
            int minY = 0;
            int maxX = width - 1;
            int maxY = height - 1;
            TryOpaqueBounds(jarSprite, width, height, ref minX, ref minY, ref maxX, ref maxY);

            minX += Mathf.Max(0, insetX);
            maxX -= Mathf.Max(0, insetX);
            minY += Mathf.Max(0, insetBottom);
            maxY -= Mathf.Max(0, insetTop);
            if (minX > maxX || minY > maxY)
            {
                minX = 0;
                minY = 0;
                maxX = width - 1;
                maxY = height - 1;
            }

            Color32[] pixels = ClearPixels(width, height);
            Color32 mask = new Color32(255, 255, 255, 255);
            FillRect(pixels, width, minX, minY, maxX - minX + 1, maxY - minY + 1, mask);

            Vector2 pivot = new Vector2(0.5f, 0.5f);
            if (jarSprite.rect.width > 0f && jarSprite.rect.height > 0f)
                pivot = new Vector2(
                    jarSprite.pivot.x / jarSprite.rect.width,
                    jarSprite.pivot.y / jarSprite.rect.height);

            return CreateSprite(pixels, width, height, jarSprite.pixelsPerUnit, pivot);
        }

        private static bool TryOpaqueBounds(
            Sprite jarSprite,
            int width,
            int height,
            ref int minX,
            ref int minY,
            ref int maxX,
            ref int maxY)
        {
            Color[] source = ReadSpritePixels(jarSprite, width, height);
            if (source == null)
                return false;

            int foundMinX = width;
            int foundMinY = height;
            int foundMaxX = -1;
            int foundMaxY = -1;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].a <= 0.01f)
                    continue;

                int x = i % width;
                int y = i / width;
                if (x < foundMinX)
                    foundMinX = x;
                if (x > foundMaxX)
                    foundMaxX = x;
                if (y < foundMinY)
                    foundMinY = y;
                if (y > foundMaxY)
                    foundMaxY = y;
            }

            if (foundMaxX < foundMinX)
                return false;

            minX = foundMinX;
            minY = foundMinY;
            maxX = foundMaxX;
            maxY = foundMaxY;
            return true;
        }

        private static Color[] ReadSpritePixels(Sprite jarSprite, int width, int height)
        {
            Texture2D texture = jarSprite != null ? jarSprite.texture : null;
            if (texture == null || !texture.isReadable)
                return null;

            return texture.GetPixels(
                Mathf.RoundToInt(jarSprite.rect.x),
                Mathf.RoundToInt(jarSprite.rect.y),
                width,
                height);
        }

        private static Sprite CreateSprite(Color32[] pixels, int width, int height)
        {
            return CreateSprite(pixels, width, height, PixelsPerUnit, new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateSprite(
            Color32[] pixels,
            int width,
            int height,
            float pixelsPerUnit,
            Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                pivot,
                pixelsPerUnit);
        }

        private static Color32[] DrawBottle(Color liquid)
        {
            Color32[] pixels = ClearPixels(Size, Size);
            Color32 glass = new Color32(210, 220, 230, 230);
            Color32 outline = new Color32(28, 32, 38, 255);
            Color32 cap = new Color32(90, 90, 96, 255);
            Color32 fill = ToColor32(liquid, 230);

            FillRect(pixels, Size, 5, 2, 6, 10, glass);
            FillRect(pixels, Size, 6, 3, 4, 8, fill);
            FillRect(pixels, Size, 6, 12, 4, 2, glass);
            FillRect(pixels, Size, 6, 14, 4, 2, cap);
            DrawRect(pixels, Size, 5, 2, 6, 12, outline);
            DrawRect(pixels, Size, 6, 14, 4, 2, outline);
            return pixels;
        }

        private static Color32[] DrawBottleMask()
        {
            Color32[] pixels = ClearPixels(Size, Size);
            Color32 mask = new Color32(255, 255, 255, 255);
            FillRect(pixels, Size, 6, 3, 4, 8, mask);
            return pixels;
        }

        private static Color32[] DrawSyringe(Color liquid)
        {
            Color32[] pixels = ClearPixels(Size, Size);
            Color32 body = new Color32(230, 236, 240, 255);
            Color32 outline = new Color32(28, 32, 38, 255);
            Color32 plunger = new Color32(70, 74, 80, 255);
            Color32 needle = new Color32(170, 176, 184, 255);
            Color32 fill = ToColor32(liquid, 230);

            FillRect(pixels, Size, 3, 6, 9, 4, body);
            FillRect(pixels, Size, 4, 7, 6, 2, fill);
            FillRect(pixels, Size, 1, 6, 2, 4, plunger);
            FillRect(pixels, Size, 12, 7, 3, 2, needle);
            DrawRect(pixels, Size, 3, 6, 9, 4, outline);
            return pixels;
        }

        private static Color32[] DrawJarMask()
        {
            Color32[] pixels = ClearPixels(Size, Size);
            Color32 mask = new Color32(255, 255, 255, 255);
            FillRect(pixels, Size, 5, 3, 6, 9, mask);
            return pixels;
        }

        private static Color32[] DrawJarMaskAsset()
        {
            // Full sprite — fill is drawn behind the jar, so opaque label/lid hide it.
            Color32[] pixels = ClearPixels(JarWidth, JarHeight);
            Color32 mask = new Color32(255, 255, 255, 255);
            FillRect(pixels, JarWidth, 0, 0, JarWidth, JarHeight, mask);
            return pixels;
        }

        private static Color32[] ClearPixels(int width, int height)
        {
            var pixels = new Color32[width * height];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;
            return pixels;
        }

        private static void FillRect(Color32[] pixels, int stride, int x, int y, int width, int height, Color32 color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                    SetPixel(pixels, stride, px, py, color);
            }
        }

        private static void DrawRect(Color32[] pixels, int stride, int x, int y, int width, int height, Color32 color)
        {
            for (int px = x; px < x + width; px++)
            {
                SetPixel(pixels, stride, px, y, color);
                SetPixel(pixels, stride, px, y + height - 1, color);
            }

            for (int py = y; py < y + height; py++)
            {
                SetPixel(pixels, stride, x, py, color);
                SetPixel(pixels, stride, x + width - 1, py, color);
            }
        }

        private static void SetPixel(Color32[] pixels, int stride, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= stride)
                return;

            int index = y * stride + x;
            if (index >= pixels.Length)
                return;

            pixels[index] = color;
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
