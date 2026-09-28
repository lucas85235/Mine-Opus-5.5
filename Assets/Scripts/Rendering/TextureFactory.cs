using UnityEngine;

namespace Mine
{
    /// <summary>
    /// Every texture in the game is painted here, pixel by pixel, at startup: block tiles (as a Texture2DArray),
    /// isometric inventory icons, UI sprites and the cloud map. Nothing is loaded from disk.
    /// </summary>
    public static class TextureFactory
    {
        public const int Tile = 16;

        public static Texture2DArray BlockArray { get; private set; }
        public static Color32[][] Tiles { get; private set; }
        public static Sprite[] Icons { get; private set; }
        public static Color32[][] ParticlePalettes { get; private set; }
        public static Texture2D CloudTexture { get; private set; }

        public static Sprite White { get; private set; }
        public static Sprite HotbarSlot { get; private set; }
        public static Sprite InventorySlot { get; private set; }
        public static Sprite SlotSelected { get; private set; }
        public static Sprite ButtonSprite { get; private set; }
        public static Sprite PanelSprite { get; private set; }
        public static Sprite DirtBackground { get; private set; }

        static bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            built = false;
        }

        public static void Build()
        {
            if (built) return;
            built = true;

            int count = (int)TexId.Count;
            Tiles = new Color32[count][];
            for (int i = 0; i < count; i++) Tiles[i] = Generate((TexId)i);

            BlockArray = new Texture2DArray(Tile, Tile, count, TextureFormat.RGBA32, true, false)
            {
                name = "BlockTextures",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 0
            };
            for (int i = 0; i < count; i++) BlockArray.SetPixels32(Tiles[i], i, 0);
            BlockArray.Apply(true, false);

            BuildParticlePalettes();
            BuildIcons();
            BuildUISprites();
            BuildClouds();
        }

        // ------------------------------------------------------------------ helpers

        static byte B(float v)
        {
            return (byte)(v < 0f ? 0f : (v > 255f ? 255f : v));
        }

        static Color32 C(int r, int g, int b, int a = 255)
        {
            return new Color32((byte)r, (byte)g, (byte)b, (byte)a);
        }

        static Color32 Mul(Color32 c, float f)
        {
            return new Color32(B(c.r * f), B(c.g * f), B(c.b * f), c.a);
        }

        static Color32 Mix(Color32 a, Color32 b, float t)
        {
            return new Color32(B(a.r + (b.r - a.r) * t), B(a.g + (b.g - a.g) * t), B(a.b + (b.b - a.b) * t), B(a.a + (b.a - a.a) * t));
        }

        static float R(int x, int y, int seed)
        {
            return MathUtil.Hash01(x, y, 0, seed);
        }

        /// <summary>Tileable smooth value noise over a size x size image with `cells` lattice cells.</summary>
        static float Smooth(int x, int y, int size, int cells, int seed)
        {
            float fx = x * cells / (float)size, fy = y * cells / (float)size;
            int x0 = (int)fx, y0 = (int)fy;
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
            x0 %= cells;
            y0 %= cells;
            float a = R(x0, y0, seed), b = R(x1, y0, seed), c = R(x0, y1, seed), d = R(x1, y1, seed);
            return MathUtil.Lerp(MathUtil.Lerp(a, b, tx), MathUtil.Lerp(c, d, tx), ty);
        }

        static Color32[] NewTile()
        {
            return new Color32[Tile * Tile];
        }

        static void Set(Color32[] t, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Tile || y >= Tile) return;
            t[y * Tile + x] = c;
        }

        static Color32 Get(Color32[] t, int x, int y)
        {
            return t[(y & 15) * Tile + (x & 15)];
        }

        static Color32[] Noisy(Color32 baseColor, float amount, int seed, float blob = 0.1f)
        {
            var t = NewTile();
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float f = 1f + (R(x, y, seed) - 0.5f) * 2f * amount;
                f *= 1f + (Smooth(x, y, Tile, 4, seed + 1) - 0.5f) * 2f * blob;
                t[y * Tile + x] = Mul(baseColor, f);
            }
            return t;
        }

        // ------------------------------------------------------------------ block tiles

        static Color32[] Generate(TexId id)
        {
            switch (id)
            {
                case TexId.GrassTop: return GrassTop();
                case TexId.GrassSide: return GrassSide();
                case TexId.Dirt: return Dirt(11);
                case TexId.Stone: return Stone(21);
                case TexId.Cobblestone: return Cobblestone();
                case TexId.Sand: return Sand();
                case TexId.Gravel: return Gravel();
                case TexId.OakLogSide: return Bark(C(104, 80, 48), 41, false);
                case TexId.OakLogTop: return LogTop(C(176, 142, 88), C(150, 116, 68), C(104, 80, 48), 43);
                case TexId.OakLeaves: return Leaves(C(62, 124, 40), 45, 0.2f);
                case TexId.Planks: return Planks(C(160, 128, 78), 51);
                case TexId.Glass: return Glass();
                case TexId.Water: return Water();
                case TexId.Bedrock: return Bedrock();
                case TexId.Snow: return Snow();
                case TexId.CoalOre: return Ore(C(38, 38, 38), C(70, 70, 70), 71);
                case TexId.IronOre: return Ore(C(216, 175, 147), C(176, 132, 104), 73);
                case TexId.BirchLogSide: return BirchBark();
                case TexId.BirchLogTop: return LogTop(C(206, 184, 130), C(186, 162, 110), C(222, 222, 212), 83);
                case TexId.BirchLeaves: return Leaves(C(104, 152, 62), 85, 0.2f);
                case TexId.SpruceLogSide: return Bark(C(66, 46, 26), 91, true);
                case TexId.SpruceLogTop: return LogTop(C(140, 104, 60), C(116, 84, 48), C(66, 46, 26), 93);
                case TexId.SpruceLeaves: return Leaves(C(40, 82, 52), 95, 0.14f);
                case TexId.StoneBricks: return StoneBricks();
                case TexId.Bricks: return Bricks();
                case TexId.CraftingTop: return CraftingTop();
                case TexId.CraftingSide: return CraftingSide();
                case TexId.SandstoneTop: return Noisy(C(220, 208, 162), 0.04f, 121, 0.05f);
                case TexId.SandstoneSide: return SandstoneSide();
                case TexId.SandstoneBottom: return SandstoneBottom();
                case TexId.Lamp: return Lamp();
                case TexId.Wool: return Wool();
                case TexId.TallGrass: return TallGrass();
                case TexId.FlowerRed: return Flower(C(200, 30, 36), C(50, 20, 20), 151);
                case TexId.FlowerYellow: return Flower(C(246, 214, 40), C(226, 150, 30), 153);
                default:
                    if (id >= TexId.Crack0 && id <= TexId.Crack9) return Crack(id - TexId.Crack0);
                    return Noisy(C(255, 0, 255), 0f, 1);
            }
        }

        static Color32[] GrassTop()
        {
            var t = NewTile();
            Color32 g = C(96, 158, 58);
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float f = 0.86f + R(x, y, 1) * 0.26f;
                f *= 0.92f + Smooth(x, y, Tile, 4, 2) * 0.16f;
                float s = R(x, y, 3);
                if (s < 0.09f) f *= 0.78f;
                else if (s > 0.94f) f *= 1.14f;
                t[y * Tile + x] = Mul(g, f);
            }
            return t;
        }

        static Color32[] GrassSide()
        {
            var t = Dirt(11);
            var top = GrassTop();
            for (int x = 0; x < Tile; x++)
            {
                int depth = 3 + (R(x, 0, 7) < 0.5f ? 1 : 0) + (R(x, 0, 8) < 0.3f ? 1 : 0) - (R(x, 0, 9) < 0.15f ? 1 : 0);
                for (int k = 0; k < depth; k++)
                {
                    int y = Tile - 1 - k;
                    t[y * Tile + x] = Mul(Get(top, x, y), k == depth - 1 ? 0.85f : 1f);
                }
            }
            return t;
        }

        static Color32[] Dirt(int seed)
        {
            var t = NewTile();
            Color32 d = C(134, 96, 67);
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float f = 0.86f + R(x, y, seed) * 0.28f;
                f *= 0.93f + Smooth(x, y, Tile, 4, seed + 1) * 0.14f;
                float s = R(x, y, seed + 2);
                Color32 c = Mul(d, f);
                if (s < 0.08f) c = Mul(d, 0.66f);
                else if (s > 0.965f) c = C(146, 132, 118);
                t[y * Tile + x] = c;
            }
            return t;
        }

        static Color32[] Stone(int seed)
        {
            var t = NewTile();
            Color32 s = C(125, 125, 125);
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float f = 0.9f + Smooth(x, y, Tile, 8, seed) * 0.2f;
                f *= 0.95f + R(x, y, seed + 1) * 0.1f;
                if (Smooth(x, y, Tile, 4, seed + 2) > 0.72f) f *= 0.86f;
                if (R(x, y, seed + 3) < 0.05f) f *= 0.8f;
                t[y * Tile + x] = Mul(s, f);
            }
            return t;
        }

        static void Voronoi(int x, int y, Vector2[] pts, out int nearest, out float d1, out float d2)
        {
            nearest = 0;
            d1 = float.MaxValue;
            d2 = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
            {
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    float dx = pts[i].x + ox * Tile - (x + 0.5f);
                    float dy = pts[i].y + oy * Tile - (y + 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < d1)
                    {
                        d2 = d1;
                        d1 = d;
                        nearest = i;
                    }
                    else if (d < d2)
                    {
                        d2 = d;
                    }
                }
            }
        }

        static Vector2[] Points(int count, int seed)
        {
            var pts = new Vector2[count];
            for (int i = 0; i < count; i++) pts[i] = new Vector2(R(i, 0, seed) * Tile, R(i, 1, seed) * Tile);
            return pts;
        }

        static Color32[] Cobblestone()
        {
            var t = NewTile();
            Vector2[] pts = Points(8, 31);
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                Voronoi(x, y, pts, out int n, out float d1, out float d2);
                Color32 c;
                if (d2 - d1 < 1.1f)
                {
                    c = Mul(C(66, 66, 66), 0.9f + R(x, y, 32) * 0.2f);
                }
                else
                {
                    float shade = 0.78f + R(n, 3, 33) * 0.42f;
                    shade *= 0.92f + R(x, y, 34) * 0.16f;
                    if (d1 < 1.6f) shade *= 1.08f;
                    c = Mul(C(118, 118, 118), shade);
                }
                t[y * Tile + x] = c;
            }
            return t;
        }

        static Color32[] Sand()
        {
            var t = NewTile();
            Color32 s = C(219, 206, 160);
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float f = 0.94f + R(x, y, 61) * 0.1f;
                float r = R(x, y, 62);
                if (r < 0.08f) f *= 0.86f;
                else if (r > 0.95f) f *= 1.05f;
                t[y * Tile + x] = Mul(s, f);
            }
            return t;
        }

        static Color32[] Gravel()
        {
            var t = NewTile();
            Vector2[] pts = Points(13, 63);
            Color32[] palette = { C(132, 126, 122), C(104, 98, 94), C(152, 144, 136), C(116, 106, 100), C(90, 86, 84) };
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                Voronoi(x, y, pts, out int n, out float d1, out float d2);
                Color32 c = palette[(int)(R(n, 5, 64) * palette.Length) % palette.Length];
                c = Mul(c, 0.92f + R(x, y, 65) * 0.16f);
                if (d2 - d1 < 0.9f) c = Mul(c, 0.7f);
                t[y * Tile + x] = c;
            }
            return t;
        }

        static Color32[] Bark(Color32 baseColor, int seed, bool fine)
        {
            var t = NewTile();
            for (int x = 0; x < Tile; x++)
            {
                float stripe = 0.84f + R(x, 0, seed) * 0.3f;
                bool groove = R(x, 1, seed) < (fine ? 0.3f : 0.22f);
                for (int y = 0; y < Tile; y++)
                {
                    float f = stripe * (0.92f + R(x, y, seed + 1) * 0.16f);
                    if (groove) f *= 0.72f;
                    if (R(x, y / 3, seed + 2) < 0.07f) f *= 0.8f;
                    t[y * Tile + x] = Mul(baseColor, f);
                }
            }
            return t;
        }

        static Color32[] LogTop(Color32 light, Color32 dark, Color32 bark, int seed)
        {
            var t = NewTile();
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                Color32 c;
                if (x == 0 || y == 0 || x == Tile - 1 || y == Tile - 1)
                {
                    c = Mul(bark, 0.9f + R(x, y, seed) * 0.2f);
                }
                else
                {
                    float dx = x - 7.5f, dy = y - 7.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) + (R(x, y, seed + 1) - 0.5f) * 0.6f;
                    bool ring = Mathf.Repeat(r, 2.4f) < 1.0f;
                    c = ring ? dark : light;
                    c = Mul(c, 0.95f + R(x, y, seed + 2) * 0.1f);
                }
                t[y * Tile + x] = c;
            }
            return t;
        }

        static Color32[] Leaves(Color32 baseColor, int seed, float holes)
        {
            var t = NewTile();
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float f = 0.72f + R(x, y, seed) * 0.5f;
                f *= 0.9f + Smooth(x, y, Tile, 4, seed + 1) * 0.2f;
                Color32 c = Mul(baseColor, f);
                c.a = (byte)(R(x, y, seed + 2) < holes ? 0 : 255);
                t[y * Tile + x] = c;
            }
            return t;
        }

        static Color32[] Planks(Color32 baseColor, int seed)
        {
            var t = NewTile();
            for (int y = 0; y < Tile; y++)
            {
                int plank = y / 4;
                float tint = 0.9f + R(plank, 0, seed) * 0.18f;
                int seam = (plank * 7 + 3) % Tile;
                for (int x = 0; x < Tile; x++)
                {
                    float f = tint * (0.95f + R(x / 2, y, seed + 1) * 0.1f);
                    if (R(y, 7, seed + 2) < 0.3f && R(x, y, seed + 3) < 0.4f) f *= 0.92f;
                    if (y % 4 == 0) f *= 0.62f;
                    else if (x == seam) f *= 0.66f;
                    t[y * Tile + x] = Mul(baseColor, f);
                }
            }
            return t;
        }

        static Color32[] Glass()
        {
            var t = NewTile();
            Color32 frame = C(214, 232, 238);
            Color32 frameDark = C(170, 196, 206);
            Color32 clear = C(0, 0, 0, 0);
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                bool edge = x == 0 || y == 0 || x == Tile - 1 || y == Tile - 1;
                t[y * Tile + x] = edge ? ((x + y) % 5 == 0 ? frameDark : frame) : clear;
            }
            Color32 shine = C(255, 255, 255, 255);
            int[,] streaks = { { 3, 12 }, { 4, 11 }, { 5, 10 }, { 3, 11 }, { 4, 10 }, { 10, 5 }, { 11, 4 }, { 12, 3 }, { 9, 6 } };
            for (int i = 0; i < streaks.GetLength(0); i++) Set(t, streaks[i, 0], streaks[i, 1], shine);
            return t;
        }

        static Color32[] Water()
        {
            var t = NewTile();
            Color32 w = C(44, 92, 206);
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float f = 0.84f + Smooth(x, y, Tile, 4, 101) * 0.26f + R(x, y, 102) * 0.06f;
                if (Smooth(x, y, Tile, 8, 103) > 0.74f) f *= 1.12f;
                t[y * Tile + x] = Mul(w, f);
            }
            return t;
        }

        static Color32[] Bedrock()
        {
            var t = NewTile();
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float v = Smooth(x, y, Tile, 8, 111) * 0.6f + R(x, y, 112) * 0.4f;
                Color32 c = v < 0.38f ? C(34, 34, 34) : (v < 0.62f ? C(78, 78, 78) : C(128, 128, 128));
                t[y * Tile + x] = c;
            }
            return t;
        }

        static Color32[] Snow()
        {
            var t = NewTile();
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float f = 0.95f + R(x, y, 113) * 0.05f;
                Color32 c = Mul(C(240, 246, 252), f);
                if (Smooth(x, y, Tile, 4, 114) > 0.7f) c = Mul(C(222, 232, 246), f);
                t[y * Tile + x] = c;
            }
            return t;
        }

        static Color32[] Ore(Color32 ore, Color32 oreEdge, int seed)
        {
            var t = Stone(21);
            for (int k = 0; k < 5; k++)
            {
                float cx = 2f + R(k, 0, seed) * 12f, cy = 2f + R(k, 1, seed) * 12f;
                for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float d = dx * dx + dy * dy;
                    if (d < 2.2f && R(x, y, seed + k + 2) < 0.85f) t[y * Tile + x] = Mul(ore, 0.9f + R(x, y, seed + 9) * 0.2f);
                    else if (d < 3.4f && R(x, y, seed + k + 3) < 0.35f) t[y * Tile + x] = oreEdge;
                }
            }
            return t;
        }

        static Color32[] BirchBark()
        {
            var t = NewTile();
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float f = 0.92f + R(x, y, 81) * 0.1f;
                t[y * Tile + x] = Mul(C(216, 214, 204), f);
            }
            for (int y = 0; y < Tile; y++)
            {
                if (R(0, y, 82) > 0.32f) continue;
                int start = (int)(R(1, y, 82) * Tile);
                int len = 2 + (int)(R(2, y, 82) * 4);
                for (int k = 0; k < len; k++) Set(t, (start + k) % Tile, y, Mul(C(46, 44, 40), 0.9f + R(k, y, 83) * 0.3f));
            }
            return t;
        }

        static Color32[] StoneBricks()
        {
            var t = Stone(131);
            Color32 mortar = C(84, 84, 84);
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                int row = y / 8;
                int seamX = row == 0 ? 0 : 8;
                bool mortarPx = y % 8 == 0 || x == seamX;
                if (mortarPx) t[y * Tile + x] = mortar;
                else if (y % 8 == 7 || x == (seamX + 1) % Tile) t[y * Tile + x] = Mul(Get(t, x, y), 1.1f);
            }
            return t;
        }

        static Color32[] Bricks()
        {
            var t = NewTile();
            Color32 mortar = C(186, 176, 168);
            for (int y = 0; y < Tile; y++)
            {
                int row = y / 4;
                int offset = (row % 2) * 4;
                for (int x = 0; x < Tile; x++)
                {
                    int brick = (x + offset) / 8 + row * 3;
                    Color32 c;
                    if (y % 4 == 0 || (x + offset) % 8 == 0)
                    {
                        c = Mul(mortar, 0.94f + R(x, y, 141) * 0.1f);
                    }
                    else
                    {
                        float f = (0.84f + R(brick, 0, 142) * 0.26f) * (0.94f + R(x, y, 143) * 0.12f);
                        c = Mul(C(150, 72, 56), f);
                    }
                    t[y * Tile + x] = c;
                }
            }
            return t;
        }

        static Color32[] CraftingTop()
        {
            var t = Planks(C(176, 140, 88), 55);
            Color32 frame = C(96, 64, 32);
            Color32 grid = C(124, 90, 52);
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                if (x == 0 || y == 0 || x == Tile - 1 || y == Tile - 1) t[y * Tile + x] = frame;
                else if (x == 5 || x == 10 || y == 5 || y == 10) t[y * Tile + x] = grid;
            }
            return t;
        }

        static Color32[] CraftingSide()
        {
            var t = Planks(C(160, 128, 78), 57);
            Color32 dark = C(92, 62, 32);
            for (int y = 12; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
                t[y * Tile + x] = Mul(dark, 0.9f + R(x, y, 58) * 0.2f);
            // saw
            Color32 metal = C(170, 170, 176);
            for (int x = 2; x <= 7; x++)
            {
                Set(t, x, 8, metal);
                Set(t, x, 7, Mul(metal, 0.85f));
                if (x % 2 == 0) Set(t, x, 6, Mul(metal, 0.7f));
            }
            Set(t, 1, 8, C(110, 70, 40));
            Set(t, 1, 7, C(110, 70, 40));
            // hammer
            Color32 handle = C(120, 78, 42);
            for (int y = 2; y <= 8; y++) Set(t, 11, y, handle);
            for (int x = 9; x <= 13; x++)
            {
                Set(t, x, 9, metal);
                Set(t, x, 10, Mul(metal, 0.85f));
            }
            return t;
        }

        static Color32[] SandstoneSide()
        {
            var t = NewTile();
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                Color32 c;
                if (y >= 12) c = C(228, 216, 172);
                else if (y >= 4) c = C(214, 200, 150);
                else c = C(204, 188, 138);
                float f = 0.96f + R(x, y, 125) * 0.08f;
                if (y == 12 || y == 4 || y == 8) f *= 0.9f;
                t[y * Tile + x] = Mul(c, f);
            }
            return t;
        }

        static Color32[] SandstoneBottom()
        {
            var t = Noisy(C(208, 194, 144), 0.05f, 127, 0.06f);
            for (int i = 0; i < 10; i++)
            {
                int x = (int)(R(i, 0, 128) * Tile), y = (int)(R(i, 1, 128) * Tile);
                Set(t, x, y, C(180, 166, 120));
                Set(t, x + 1, y, C(186, 172, 126));
            }
            return t;
        }

        static Color32[] Lamp()
        {
            var t = NewTile();
            Color32 frame = C(118, 82, 44);
            Color32 bar = C(160, 112, 52);
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                Color32 c;
                if (x == 0 || y == 0 || x == Tile - 1 || y == Tile - 1) c = frame;
                else if (x == 7 || x == 8 || y == 7 || y == 8) c = bar;
                else
                {
                    float px = (x < 8 ? x - 3.5f : x - 11.5f), py = (y < 8 ? y - 3.5f : y - 11.5f);
                    float d = Mathf.Sqrt(px * px + py * py);
                    c = Mix(C(255, 248, 210), C(250, 196, 96), Mathf.Clamp01(d / 4f));
                    c = Mul(c, 0.97f + R(x, y, 131) * 0.06f);
                }
                t[y * Tile + x] = c;
            }
            return t;
        }

        static Color32[] Wool()
        {
            var t = NewTile();
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                float f = 0.88f + Smooth(x, y, Tile, 8, 141) * 0.1f + R(x, y, 142) * 0.05f;
                if (((x + y * 3) % 7) == 0) f *= 0.94f;
                t[y * Tile + x] = Mul(C(236, 236, 236), f);
            }
            return t;
        }

        static Color32[] TallGrass()
        {
            var t = NewTile();
            for (int i = 0; i < 9; i++)
            {
                float bx = 1f + R(i, 0, 151) * 13f;
                int height = 6 + (int)(R(i, 1, 151) * 9f);
                float lean = (R(i, 2, 151) - 0.5f) * 0.5f;
                for (int y = 0; y < height; y++)
                {
                    int x = Mathf.RoundToInt(bx + lean * y);
                    float f = 0.7f + 0.45f * y / height;
                    Color32 c = Mul(C(76, 142, 44), f * (0.9f + R(i, y, 152) * 0.2f));
                    Set(t, x, y, c);
                    if (y < height / 3) Set(t, x + 1, y, Mul(c, 0.85f));
                }
            }
            return t;
        }

        static Color32[] Flower(Color32 petal, Color32 center, int seed)
        {
            var t = NewTile();
            Color32 stem = C(52, 118, 34);
            for (int y = 0; y <= 8; y++) Set(t, 7, y, stem);
            Set(t, 6, 3, stem);
            Set(t, 5, 4, stem);
            Set(t, 8, 5, stem);
            Set(t, 9, 6, stem);
            for (int y = 7; y <= 14; y++)
            for (int x = 3; x <= 12; x++)
            {
                float dx = x - 7.5f, dy = y - 11f;
                float d = dx * dx + dy * dy;
                if (d < 7.5f && R(x, y, seed) < 0.93f) Set(t, x, y, Mul(petal, 0.85f + R(x, y, seed + 1) * 0.3f));
            }
            Set(t, 7, 11, center);
            Set(t, 8, 11, center);
            Set(t, 7, 10, Mul(center, 0.85f));
            return t;
        }

        static Color32[] Crack(int stage)
        {
            var t = NewTile();
            // Thin cracks radiating from the centre; later stages extend the same paths and add branches.
            Color32 ink = C(46, 46, 46, 255);
            int branches = 3 + stage / 2;
            int length = 2 + stage;
            for (int w = 0; w < branches; w++)
            {
                float x = 7.5f, y = 7.5f;
                float ang = (w / (float)branches + R(w, 2, 171) * 0.15f) * Mathf.PI * 2f;
                for (int s = 0; s < length; s++)
                {
                    Set(t, (int)x, (int)y, ink);
                    ang += (R(w, s, 172) - 0.5f) * 0.9f;
                    x += Mathf.Cos(ang);
                    y += Mathf.Sin(ang);
                    if (x < 0 || y < 0 || x >= Tile || y >= Tile) break;
                }
            }
            return t;
        }

        // ------------------------------------------------------------------ derived assets

        static void BuildParticlePalettes()
        {
            ParticlePalettes = new Color32[256][];
            for (int b = 0; b < Blocks.Count; b++)
            {
                BlockDef def = Blocks.Defs[b];
                Color32[] tile = Tiles[(int)def.Side];
                var pal = new Color32[12];
                int found = 0;
                for (int k = 0; k < 64 && found < pal.Length; k++)
                {
                    int i = (int)(R(k, b, 181) * tile.Length) % tile.Length;
                    if (tile[i].a > 128) pal[found++] = tile[i];
                }
                if (found == 0) pal[found++] = C(200, 200, 200);
                for (int i = found; i < pal.Length; i++) pal[i] = pal[i % found];
                ParticlePalettes[b] = pal;
            }
            for (int b = Blocks.Count; b < 256; b++) ParticlePalettes[b] = ParticlePalettes[(int)BlockId.Stone];
        }

        static void BuildIcons()
        {
            Icons = new Sprite[256];
            for (int b = 1; b < Blocks.Count; b++)
            {
                BlockDef def = Blocks.Defs[b];
                Texture2D tex = def.Shape == BlockShape.Cross
                    ? FlatIcon(Tiles[(int)def.Side])
                    : IsoIcon(Tiles[(int)def.Top], Tiles[(int)def.Side]);
                tex.name = "Icon_" + def.Id;
                Icons[b] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        static bool Solve(Vector2 p, Vector2 o, Vector2 a, Vector2 b, out float u, out float v)
        {
            Vector2 d = p - o;
            float det = a.x * b.y - a.y * b.x;
            u = (d.x * b.y - d.y * b.x) / det;
            v = (a.x * d.y - a.y * d.x) / det;
            return u >= 0f && u < 1f && v >= 0f && v < 1f;
        }

        static Color32 SampleTile(Color32[] tile, float u, float v, float shade)
        {
            int x = Mathf.Clamp((int)(u * Tile), 0, Tile - 1);
            int y = Mathf.Clamp((int)(v * Tile), 0, Tile - 1);
            Color32 c = tile[y * Tile + x];
            return new Color32(B(c.r * shade), B(c.g * shade), B(c.b * shade), c.a);
        }

        static Texture2D IsoIcon(Color32[] top, Color32[] side)
        {
            const int S = 32;
            var px = new Color32[S * S];
            var L = new Vector2(2f, 23.5f);
            var F = new Vector2(16f, 16.5f);
            var Bk = new Vector2(16f, 30.5f);
            var Lb = new Vector2(2f, 8.5f);
            var Fb = new Vector2(16f, 1.5f);
            var Rb = new Vector2(30f, 8.5f);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                Color32 c = new Color32(0, 0, 0, 0);
                if (Solve(p, L, F - L, Bk - L, out float u, out float v)) c = SampleTile(top, u, v, 1f);
                else if (Solve(p, Lb, Fb - Lb, L - Lb, out u, out v)) c = SampleTile(side, u, v, 0.8f);
                else if (Solve(p, Fb, Rb - Fb, F - Fb, out u, out v)) c = SampleTile(side, u, v, 0.62f);
                px[y * S + x] = c;
            }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Texture2D FlatIcon(Color32[] tile)
        {
            const int S = 32;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                px[y * S + x] = tile[(y / 2) * Tile + x / 2];
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        public static Texture2D TileTexture(TexId id, float brightness)
        {
            Color32[] tile = Tiles[(int)id];
            var px = new Color32[tile.Length];
            for (int i = 0; i < tile.Length; i++) px[i] = Mul(tile[i], brightness);
            var tex = new Texture2D(Tile, Tile, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Sprite MakeSprite(int w, int h, System.Func<int, int, Color32> paint, int border, float ppu = 100f)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = paint(x, y);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        static void BuildUISprites()
        {
            White = MakeSprite(4, 4, (x, y) => C(255, 255, 255), 1);

            HotbarSlot = MakeSprite(20, 20, (x, y) =>
            {
                bool outer = x == 0 || y == 0 || x == 19 || y == 19;
                bool mid = x == 1 || y == 1 || x == 18 || y == 18;
                if (outer) return C(20, 20, 20, 230);
                if (mid) return C(120, 120, 120, 230);
                if (x == 2 || y == 17) return C(60, 60, 60, 200);
                return C(24, 24, 24, 150);
            }, 3, 25f);

            InventorySlot = MakeSprite(18, 18, (x, y) =>
            {
                if (x == 0 || y == 17) return C(55, 55, 55);
                if (x == 17 || y == 0) return C(255, 255, 255);
                return C(139, 139, 139);
            }, 2, 25f);

            SlotSelected = MakeSprite(24, 24, (x, y) =>
            {
                int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(23 - x, 23 - y));
                if (d == 0) return C(0, 0, 0, 255);
                if (d <= 2) return C(236, 236, 236, 255);
                if (d == 3) return C(110, 110, 110, 255);
                return C(0, 0, 0, 0);
            }, 5, 25f);

            ButtonSprite = MakeSprite(20, 20, (x, y) =>
            {
                if (x == 0 || y == 0 || x == 19 || y == 19) return C(0, 0, 0);
                if (x == 1 || y == 18) return C(170, 170, 170);
                if (x == 18 || y == 1) return C(62, 62, 62);
                float n = R(x, y, 191) * 0.08f;
                return Mul(C(114, 114, 114), 0.96f + n);
            }, 3, 25f);

            PanelSprite = MakeSprite(16, 16, (x, y) =>
            {
                if (x == 0 || y == 0 || x == 15 || y == 15) return C(0, 0, 0);
                if (x == 1 || y == 14 || x == 2 || y == 13) return C(255, 255, 255);
                if (x == 14 || y == 1 || x == 13 || y == 2) return C(85, 85, 85);
                return C(198, 198, 198);
            }, 4, 25f);

            Texture2D dirt = TileTexture(TexId.Dirt, 0.32f);
            DirtBackground = Sprite.Create(dirt, new Rect(0, 0, Tile, Tile), new Vector2(0.5f, 0.5f), 25f);
        }

        static void BuildClouds()
        {
            const int S = 64;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = 0.65f * Smooth(x, y, S, 8, 201) + 0.35f * Smooth(x, y, S, 16, 202);
                px[y * S + x] = n > 0.55f ? C(255, 255, 255, 255) : C(255, 255, 255, 0);
            }
            CloudTexture = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                name = "Clouds",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            CloudTexture.SetPixels32(px);
            CloudTexture.Apply(false, true);
        }
    }
}
