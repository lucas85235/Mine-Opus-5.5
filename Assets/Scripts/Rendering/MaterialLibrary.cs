using UnityEngine;

namespace Mine
{
    /// <summary>
    /// Runtime material instances. Base materials live in Resources/Materials so their shaders are always
    /// included in player builds; we clone them so runtime changes never touch the assets.
    /// </summary>
    public static class MaterialLibrary
    {
        public static Material ChunkOpaque { get; private set; }
        public static Material ChunkCutout { get; private set; }
        public static Material Water { get; private set; }
        public static Material Outline { get; private set; }
        public static Material Crack { get; private set; }
        public static Material Sky { get; private set; }
        public static Material Clouds { get; private set; }
        public static Material Entity { get; private set; }
        public static Material Particle { get; private set; }
        public static Material[] ChunkMaterials { get; private set; }

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

            Texture2DArray blocks = TextureFactory.BlockArray;

            ChunkOpaque = Make("ChunkOpaque", "Mine/Voxel");
            ChunkOpaque.SetTexture("_BlockTextures", blocks);
            ChunkOpaque.DisableKeyword("_ALPHATEST_ON");

            ChunkCutout = Make("ChunkCutout", "Mine/Voxel");
            ChunkCutout.SetTexture("_BlockTextures", blocks);
            ChunkCutout.EnableKeyword("_ALPHATEST_ON");
            ChunkCutout.SetFloat("_AlphaTest", 1f);
            ChunkCutout.SetFloat("_Cutoff", 0.5f);
            ChunkCutout.renderQueue = 2450;

            Water = Make("Water", "Mine/Water");
            Water.SetTexture("_BlockTextures", blocks);

            Outline = Make("Outline", "Mine/UnlitColor");
            Outline.SetColor("_Color", new Color(0f, 0f, 0f, 0.65f));

            Crack = Make("Crack", "Mine/Overlay");
            Crack.SetTexture("_BlockTextures", blocks);

            Sky = Make("Sky", "Mine/Sky");

            Clouds = Make("Clouds", "Mine/Clouds");
            Clouds.SetTexture("_CloudTex", TextureFactory.CloudTexture);

            Entity = Make("Entity", "Mine/Entity");
            Particle = Make("Particle", "Mine/Particle");

            ChunkMaterials = new[] { ChunkOpaque, ChunkCutout, Water };
        }

        static Material Make(string resourceName, string shaderName)
        {
            var baseMaterial = Resources.Load<Material>("Materials/" + resourceName);
            Material m;
            if (baseMaterial != null)
            {
                m = new Material(baseMaterial);
            }
            else
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    Debug.LogError("Shader not found: " + shaderName);
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }
                m = new Material(shader);
            }
            m.name = resourceName + " (Runtime)";
            return m;
        }
    }
}
