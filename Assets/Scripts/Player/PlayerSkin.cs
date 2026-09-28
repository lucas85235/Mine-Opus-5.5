using System;
using UnityEngine;

namespace Mine
{
    /// <summary>
    /// Paints the original player character ("the sardine explorer": brown hair, teal hoodie with a sardine
    /// emblem, jeans, boots and a leather backpack) into a 64x64 texture and returns the UV rectangle of every
    /// box face, in MeshUtil.AddBox order (+X = character's right, -X, +Y, -Y, +Z = front, -Z = back).
    /// Faces are painted in "face space": i grows to the right and j grows upwards as seen from outside.
    /// </summary>
    public sealed class PlayerSkin
    {
        const int Size = 64;

        public Texture2D Texture { get; private set; }
        public Rect[] Head { get; private set; }
        public Rect[] Body { get; private set; }
        public Rect[] ArmRight { get; private set; }
        public Rect[] ArmLeft { get; private set; }
        public Rect[] LegRight { get; private set; }
        public Rect[] LegLeft { get; private set; }
        public Rect[] Backpack { get; private set; }

        readonly Color32[] px = new Color32[Size * Size];
        int shelfX;
        int shelfY;
        int shelfH;

        static readonly Color32 SkinC = new Color32(226, 176, 136, 255);
        static readonly Color32 SkinShade = new Color32(204, 152, 114, 255);
        static readonly Color32 Blush = new Color32(232, 150, 132, 255);
        static readonly Color32 HairC = new Color32(78, 50, 32, 255);
        static readonly Color32 HairLight = new Color32(102, 68, 44, 255);
        static readonly Color32 Brow = new Color32(58, 36, 22, 255);
        static readonly Color32 EyeWhite = new Color32(242, 242, 242, 255);
        static readonly Color32 Pupil = new Color32(58, 92, 168, 255);
        static readonly Color32 Mouth = new Color32(150, 78, 70, 255);
        static readonly Color32 HoodieC = new Color32(36, 146, 150, 255);
        static readonly Color32 HoodieDark = new Color32(24, 108, 112, 255);
        static readonly Color32 HoodieLight = new Color32(54, 170, 172, 255);
        static readonly Color32 Strings = new Color32(234, 234, 228, 255);
        static readonly Color32 StringsDark = new Color32(196, 196, 190, 255);
        static readonly Color32 Fish = new Color32(192, 208, 224, 255);
        static readonly Color32 FishBelly = new Color32(226, 234, 242, 255);
        static readonly Color32 FishDark = new Color32(118, 140, 164, 255);
        static readonly Color32 FishEye = new Color32(28, 30, 40, 255);
        static readonly Color32 JeansC = new Color32(52, 78, 142, 255);
        static readonly Color32 JeansDark = new Color32(38, 58, 110, 255);
        static readonly Color32 Boot = new Color32(86, 60, 42, 255);
        static readonly Color32 BootDark = new Color32(58, 40, 28, 255);
        static readonly Color32 Lace = new Color32(156, 124, 86, 255);
        static readonly Color32 Sole = new Color32(38, 30, 26, 255);
        static readonly Color32 PackC = new Color32(124, 84, 48, 255);
        static readonly Color32 PackDark = new Color32(94, 60, 32, 255);
        static readonly Color32 Buckle = new Color32(214, 188, 92, 255);

        public static PlayerSkin Create()
        {
            var skin = new PlayerSkin();
            skin.Paint();
            return skin;
        }

        // ------------------------------------------------------------------ packing helpers

        Rect Face(int w, int h, Func<int, int, Color32> paint)
        {
            if (shelfX + w > Size)
            {
                shelfX = 0;
                shelfY += shelfH + 1;
                shelfH = 0;
            }
            if (shelfY + h > Size) throw new InvalidOperationException("Player skin atlas is full");
            int x0 = shelfX, y0 = shelfY;
            shelfX += w + 1;
            shelfH = Math.Max(shelfH, h);
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
                px[(y0 + j) * Size + x0 + i] = paint(i, j);
            // Inset a hair so point sampling never picks a neighbouring region at the edges.
            const float e = 0.02f;
            return new Rect((x0 + e) / Size, (y0 + e) / Size, (w - 2f * e) / Size, (h - 2f * e) / Size);
        }

        Rect[] Box(int w, int h, int d,
            Func<int, int, Color32> right, Func<int, int, Color32> left,
            Func<int, int, Color32> top, Func<int, int, Color32> bottom,
            Func<int, int, Color32> front, Func<int, int, Color32> back)
        {
            return new[]
            {
                Face(d, h, right),   // +X: front edge is on the right (i = d - 1)
                Face(d, h, left),    // -X: front edge is on the left (i = 0)
                Face(w, d, top),     // +Y: front edge at the top (j = d - 1)
                Face(d, w, bottom),  // -Y
                Face(w, h, front),   // +Z: i = 0 is the character's right side
                Face(w, h, back),    // -Z
            };
        }

        static Color32 Vary(Color32 c, int i, int j, int salt, float amount)
        {
            float f = 1f + (MathUtil.Hash01(i, j, salt, 7331) - 0.5f) * 2f * amount;
            return new Color32((byte)Mathf.Clamp(c.r * f, 0f, 255f), (byte)Mathf.Clamp(c.g * f, 0f, 255f), (byte)Mathf.Clamp(c.b * f, 0f, 255f), 255);
        }

        static Color32 Skin(int i, int j) => Vary(SkinC, i, j, 1, 0.03f);
        static Color32 Hair(int i, int j) => MathUtil.Hash01(i, j, 2, 99) > 0.72f ? HairLight : Vary(HairC, i, j, 3, 0.05f);
        static Color32 Hoodie(int i, int j) => Vary(HoodieC, i, j, 4, 0.04f);
        static Color32 Jeans(int i, int j) => Vary(JeansC, i, j, 5, 0.05f);
        static Color32 Pack(int i, int j) => Vary(PackC, i, j, 6, 0.05f);

        // ------------------------------------------------------------------ painting

        void Paint()
        {
            Head = Box(8, 8, 8,
                (i, j) => HeadSide(i, j, true),
                (i, j) => HeadSide(i, j, false),
                Hair,
                (i, j) => SkinShade,
                HeadFront,
                (i, j) => j == 0 ? HairLight : Hair(i, j));

            Body = Box(8, 12, 4,
                (i, j) => j == 0 ? HoodieDark : Hoodie(i, j),
                (i, j) => j == 0 ? HoodieDark : Hoodie(i, j),
                Hoodie,
                (i, j) => JeansDark,
                BodyFront,
                BodyBack);

            ArmRight = Arm();
            ArmLeft = Arm();
            LegRight = Leg();
            LegLeft = Leg();

            Backpack = Box(6, 8, 3,
                (i, j) => j == 5 ? PackDark : Pack(i, j),
                (i, j) => j == 5 ? PackDark : Pack(i, j),
                (i, j) => PackDark,
                (i, j) => PackDark,
                Pack,
                PackBack);

            Texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "PlayerSkin",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Texture.SetPixels32(px);
            Texture.Apply(false, false);
        }

        Rect[] Arm()
        {
            Func<int, int, Color32> side = (i, j) =>
            {
                if (j >= 5) return Hoodie(i, j);
                if (j == 4) return HoodieDark;
                return j == 0 ? SkinShade : Skin(i, j);
            };
            return Box(4, 12, 4, side, side, Hoodie, (i, j) => SkinShade, side, side);
        }

        Rect[] Leg()
        {
            Func<int, int, Color32> side = (i, j) =>
            {
                if (j >= 3) return j == 3 ? JeansDark : Jeans(i, j);
                return j == 0 ? BootDark : Boot;
            };
            Func<int, int, Color32> front = (i, j) =>
            {
                if (j >= 3) return j == 3 ? JeansDark : Jeans(i, j);
                if (j == 2) return (i == 1 || i == 2) ? Lace : Boot;
                return j == 0 ? BootDark : Boot;
            };
            return Box(4, 12, 4, side, side, Jeans, (i, j) => Sole, front, side);
        }

        static Color32 HeadFront(int i, int j)
        {
            if (j >= 6) return Hair(i, j);
            if (j == 5)
            {
                if (i == 0 || i == 7) return Hair(i, j);
                return (i == 1 || i == 2 || i == 5 || i == 6) ? Brow : Skin(i, j);
            }
            if (j == 4)
            {
                if (i == 1 || i == 6) return EyeWhite;
                if (i == 2 || i == 5) return Pupil;
                return Skin(i, j);
            }
            if (j == 3) return (i == 3 || i == 4) ? SkinShade : Skin(i, j);
            if (j == 2)
            {
                if (i == 2 || i == 5) return Mouth;
                if (i == 1 || i == 6) return Blush;
                return Skin(i, j);
            }
            if (j == 1) return (i == 3 || i == 4) ? Mouth : Skin(i, j);
            return Skin(i, j);
        }

        static Color32 HeadSide(int i, int j, bool frontAtRight)
        {
            int fromFront = frontAtRight ? 7 - i : i;
            if (j >= 6) return Hair(i, j);
            if (j == 5) return fromFront >= 2 ? Hair(i, j) : Skin(i, j);
            if (j >= 2 && fromFront >= 5) return Hair(i, j);
            if (fromFront == 3 && (j == 3 || j == 4)) return SkinShade; // ear
            return Skin(i, j);
        }

        static Color32 BodyFront(int i, int j)
        {
            if (j == 11)
            {
                if (i == 3 || i == 4) return Skin(i, j);
                return (i == 2 || i == 5) ? HoodieDark : Hoodie(i, j);
            }
            if (j == 10) return (i == 3 || i == 4) ? HoodieDark : Hoodie(i, j);
            if (j == 9 || j == 8)
            {
                if (i == 3 || i == 4) return j == 8 ? StringsDark : Strings;
                return Hoodie(i, j);
            }
            // Sardine emblem (head on the left of the picture, tail on the right).
            if (j == 7)
            {
                if (i >= 2 && i <= 4) return Fish;
                if (i == 7) return Fish;
            }
            if (j == 6)
            {
                if (i == 1) return FishDark;
                if (i == 2) return FishEye;
                if (i >= 3 && i <= 5) return Fish;
                if (i == 6) return FishDark;
                if (i == 7) return Fish;
            }
            if (j == 5)
            {
                if (i >= 2 && i <= 4) return FishBelly;
                if (i == 7) return Fish;
            }
            // Kangaroo pocket.
            if (j >= 1 && j <= 3 && i >= 1 && i <= 6) return (j == 3 || i == 1 || i == 6) ? HoodieDark : HoodieLight;
            if (j == 0) return HoodieDark;
            return Hoodie(i, j);
        }

        static Color32 BodyBack(int i, int j)
        {
            if (j >= 9 && i >= 1 && i <= 6) return (j == 9 || i == 1 || i == 6) ? HoodieDark : HoodieLight; // hood
            if (j == 0) return HoodieDark;
            return Hoodie(i, j);
        }

        static Color32 PackBack(int i, int j)
        {
            if (j >= 5) return (j == 5 || j == 7) ? PackDark : Pack(i, j); // flap
            if (j == 4 && (i == 2 || i == 3)) return Buckle;
            if (j >= 1 && j <= 3 && i >= 1 && i <= 4) return (j == 3 || i == 1 || i == 4) ? PackDark : Pack(i, j);
            return Pack(i, j);
        }
    }
}
