using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mine
{
    /// <summary>Builds blocky animal models (boxes with painted skins) at runtime.</summary>
    public static class MobFactory
    {
        public sealed class Spec
        {
            public float HalfWidth;
            public float Height;
            public int Health;
            public float WalkSpeed;
        }

        static readonly Dictionary<MobKind, Material> materials = new Dictionary<MobKind, Material>();

        // Regions of the 64x64 skin texture (normalised rects).
        static Rect Region(int i)
        {
            return new Rect((i % 2) * 0.5f, (i / 2) * 0.5f, 0.5f, 0.5f);
        }

        const int Skin = 0, Face = 1, Legs = 2, Extra = 3;

        public static Spec GetSpec(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Pig: return new Spec { HalfWidth = 0.42f, Height = 0.9f, Health = 4, WalkSpeed = 1.1f };
                case MobKind.Sheep: return new Spec { HalfWidth = 0.42f, Height = 1.3f, Health = 4, WalkSpeed = 1.0f };
                default: return new Spec { HalfWidth = 0.45f, Height = 1.4f, Health = 5, WalkSpeed = 0.95f };
            }
        }

        public static Material GetMaterial(MobKind kind)
        {
            if (materials.TryGetValue(kind, out Material m) && m != null) return m;
            m = new Material(MaterialLibrary.Entity) { name = kind + " (Runtime)" };
            m.SetTexture("_MainTex", PaintSkin(kind));
            materials[kind] = m;
            return m;
        }

        public static void ClearCache()
        {
            materials.Clear();
        }

        // ------------------------------------------------------------------ skins

        static Color32 Vary(Color32 c, float amount, int x, int y, int seed)
        {
            float f = 1f + (MathUtil.Hash01(x, y, 0, seed) - 0.5f) * 2f * amount;
            return new Color32((byte)Mathf.Clamp(c.r * f, 0, 255), (byte)Mathf.Clamp(c.g * f, 0, 255), (byte)Mathf.Clamp(c.b * f, 0, 255), 255);
        }

        static void FillRegion(Color32[] px, int region, System.Func<int, int, Color32> paint)
        {
            int ox = (region % 2) * 32, oy = (region / 2) * 32;
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                px[(oy + y) * 64 + ox + x] = paint(x, y);
        }

        static bool Eye(int x, int y, int ex, int ey)
        {
            return x >= ex && x < ex + 5 && y >= ey && y < ey + 4;
        }

        static Color32 EyeColor(int x, int ex)
        {
            return x < ex + 2 ? new Color32(245, 245, 245, 255) : new Color32(20, 20, 20, 255);
        }

        static Texture2D PaintSkin(MobKind kind)
        {
            var px = new Color32[64 * 64];
            switch (kind)
            {
                case MobKind.Pig:
                {
                    var pink = new Color32(238, 162, 160, 255);
                    FillRegion(px, Skin, (x, y) => Vary(pink, 0.06f, x, y, 1));
                    FillRegion(px, Face, (x, y) =>
                    {
                        if (Eye(x, y, 3, 18)) return EyeColor(x, 3);
                        if (Eye(x, y, 24, 18)) return EyeColor(x, 27) ;
                        return Vary(pink, 0.05f, x, y, 2);
                    });
                    FillRegion(px, Legs, (x, y) => y < 6 ? Vary(new Color32(150, 96, 92, 255), 0.05f, x, y, 3) : Vary(pink, 0.06f, x, y, 4));
                    FillRegion(px, Extra, (x, y) =>
                    {
                        bool nostril = y > 10 && y < 20 && ((x > 5 && x < 11) || (x > 20 && x < 26));
                        return nostril ? new Color32(140, 70, 70, 255) : Vary(new Color32(228, 140, 140, 255), 0.04f, x, y, 5);
                    });
                    break;
                }
                case MobKind.Sheep:
                {
                    var wool = new Color32(232, 232, 226, 255);
                    var skin = new Color32(214, 186, 156, 255);
                    FillRegion(px, Skin, (x, y) =>
                    {
                        float n = MathUtil.Hash01(x / 3, y / 3, 0, 6);
                        return Vary(n > 0.6f ? new Color32(214, 214, 208, 255) : wool, 0.04f, x, y, 7);
                    });
                    FillRegion(px, Face, (x, y) =>
                    {
                        if (Eye(x, y, 4, 17)) return EyeColor(x, 4);
                        if (Eye(x, y, 23, 17)) return EyeColor(x, 26);
                        if (y < 8 && x > 11 && x < 20) return new Color32(170, 120, 110, 255);
                        return Vary(skin, 0.05f, x, y, 8);
                    });
                    FillRegion(px, Legs, (x, y) => y < 5 ? new Color32(90, 70, 60, 255) : Vary(skin, 0.05f, x, y, 9));
                    FillRegion(px, Extra, (x, y) => Vary(wool, 0.05f, x, y, 10));
                    break;
                }
                default:
                {
                    var brown = new Color32(92, 62, 40, 255);
                    var white = new Color32(236, 236, 232, 255);
                    FillRegion(px, Skin, (x, y) =>
                    {
                        float n = MathUtil.Hash01(x / 6, y / 5, 0, 11);
                        return Vary(n > 0.62f ? white : brown, 0.05f, x, y, 12);
                    });
                    FillRegion(px, Face, (x, y) =>
                    {
                        if (Eye(x, y, 3, 18)) return EyeColor(x, 3);
                        if (Eye(x, y, 24, 18)) return EyeColor(x, 27);
                        if (y < 9) return Vary(new Color32(206, 160, 150, 255), 0.04f, x, y, 13);
                        if (x > 12 && x < 19 && y > 9) return Vary(white, 0.04f, x, y, 14);
                        return Vary(brown, 0.05f, x, y, 15);
                    });
                    FillRegion(px, Legs, (x, y) => y < 5 ? new Color32(40, 32, 28, 255) : Vary(brown, 0.05f, x, y, 16));
                    FillRegion(px, Extra, (x, y) => Vary(new Color32(222, 212, 190, 255), 0.05f, x, y, 17));
                    break;
                }
            }
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                name = kind + "Skin",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        // ------------------------------------------------------------------ models

        static Rect[] Faces(int front, int others)
        {
            Rect o = Region(others);
            return new[] { o, o, o, o, Region(front), o };
        }

        static Transform Part(Transform parent, string name, Vector3 pivot, Vector3 meshCenter, Vector3 size, Rect[] uvs,
            Material mat, List<Renderer> renderers)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pivot;
            go.GetComponent<MeshFilter>().sharedMesh = MeshUtil.Box(meshCenter, size, uvs, name);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderers.Add(mr);
            return go.transform;
        }

        public static Mob Create(MobKind kind, Transform parent)
        {
            var root = new GameObject(kind.ToString());
            root.transform.SetParent(parent, false);
            Material mat = GetMaterial(kind);
            var renderers = new List<Renderer>();
            var legs = new Transform[4];
            Transform head;
            Spec spec = GetSpec(kind);

            Rect[] skinAll = Faces(Skin, Skin);
            Rect[] legAll = Faces(Legs, Legs);

            switch (kind)
            {
                case MobKind.Pig:
                {
                    Part(root.transform, "Body", new Vector3(0, 0.62f, 0), Vector3.zero, new Vector3(0.62f, 0.5f, 0.9f), skinAll, mat, renderers);
                    head = Part(root.transform, "Head", new Vector3(0, 0.72f, 0.4f), new Vector3(0, 0, 0.2f), new Vector3(0.5f, 0.5f, 0.42f), Faces(Face, Skin), mat, renderers);
                    Part(head, "Snout", Vector3.zero, new Vector3(0, -0.08f, 0.45f), new Vector3(0.26f, 0.17f, 0.08f), Faces(Extra, Extra), mat, renderers);
                    Vector3 legSize = new Vector3(0.2f, 0.4f, 0.2f);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = (i % 2 == 0) ? -0.18f : 0.18f, z = (i < 2) ? 0.28f : -0.28f;
                        legs[i] = Part(root.transform, "Leg" + i, new Vector3(x, 0.4f, z), new Vector3(0, -0.2f, 0), legSize, legAll, mat, renderers);
                    }
                    break;
                }
                case MobKind.Sheep:
                {
                    Part(root.transform, "Body", new Vector3(0, 0.9f, 0), Vector3.zero, new Vector3(0.72f, 0.62f, 1.0f), skinAll, mat, renderers);
                    head = Part(root.transform, "Head", new Vector3(0, 1.12f, 0.46f), new Vector3(0, 0.02f, 0.2f), new Vector3(0.42f, 0.44f, 0.42f), Faces(Face, Legs), mat, renderers);
                    Part(head, "Wool", Vector3.zero, new Vector3(0, 0.2f, 0.12f), new Vector3(0.46f, 0.12f, 0.3f), Faces(Extra, Extra), mat, renderers);
                    Vector3 legSize = new Vector3(0.2f, 0.6f, 0.2f);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = (i % 2 == 0) ? -0.2f : 0.2f, z = (i < 2) ? 0.32f : -0.32f;
                        legs[i] = Part(root.transform, "Leg" + i, new Vector3(x, 0.6f, z), new Vector3(0, -0.3f, 0), legSize, legAll, mat, renderers);
                    }
                    break;
                }
                default:
                {
                    Part(root.transform, "Body", new Vector3(0, 1.0f, 0), Vector3.zero, new Vector3(0.8f, 0.72f, 1.15f), skinAll, mat, renderers);
                    head = Part(root.transform, "Head", new Vector3(0, 1.22f, 0.56f), new Vector3(0, 0, 0.17f), new Vector3(0.5f, 0.5f, 0.34f), Faces(Face, Skin), mat, renderers);
                    Rect[] horn = Faces(Extra, Extra);
                    Part(head, "HornL", Vector3.zero, new Vector3(-0.3f, 0.25f, 0.17f), new Vector3(0.1f, 0.14f, 0.1f), horn, mat, renderers);
                    Part(head, "HornR", Vector3.zero, new Vector3(0.3f, 0.25f, 0.17f), new Vector3(0.1f, 0.14f, 0.1f), horn, mat, renderers);
                    Vector3 legSize = new Vector3(0.24f, 0.66f, 0.24f);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = (i % 2 == 0) ? -0.25f : 0.25f, z = (i < 2) ? 0.4f : -0.4f;
                        legs[i] = Part(root.transform, "Leg" + i, new Vector3(x, 0.66f, z), new Vector3(0, -0.33f, 0), legSize, legAll, mat, renderers);
                    }
                    break;
                }
            }

            var mob = root.AddComponent<Mob>();
            mob.Configure(kind, spec, head, legs, renderers.ToArray());
            return mob;
        }
    }
}
