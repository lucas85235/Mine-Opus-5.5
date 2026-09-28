using System;
using UnityEngine;

namespace Mine
{
    public enum BlockId : byte
    {
        Air = 0,
        Grass,
        Dirt,
        Stone,
        Cobblestone,
        Sand,
        Gravel,
        OakLog,
        OakLeaves,
        Planks,
        Glass,
        Water,
        Bedrock,
        Snow,
        CoalOre,
        IronOre,
        BirchLog,
        BirchLeaves,
        SpruceLog,
        SpruceLeaves,
        StoneBricks,
        Bricks,
        CraftingTable,
        Sandstone,
        Lamp,
        Wool,
        TallGrass,
        FlowerRed,
        FlowerYellow,
        Count
    }

    /// <summary>Slices of the procedural block texture array.</summary>
    public enum TexId : ushort
    {
        GrassTop,
        GrassSide,
        Dirt,
        Stone,
        Cobblestone,
        Sand,
        Gravel,
        OakLogSide,
        OakLogTop,
        OakLeaves,
        Planks,
        Glass,
        Water,
        Bedrock,
        Snow,
        CoalOre,
        IronOre,
        BirchLogSide,
        BirchLogTop,
        BirchLeaves,
        SpruceLogSide,
        SpruceLogTop,
        SpruceLeaves,
        StoneBricks,
        Bricks,
        CraftingTop,
        CraftingSide,
        SandstoneTop,
        SandstoneSide,
        SandstoneBottom,
        Lamp,
        Wool,
        TallGrass,
        FlowerRed,
        FlowerYellow,
        Crack0,
        Crack1,
        Crack2,
        Crack3,
        Crack4,
        Crack5,
        Crack6,
        Crack7,
        Crack8,
        Crack9,
        Count
    }

    public enum BlockShape : byte { Empty = 0, Cube = 1, Cross = 2, Liquid = 3 }

    /// <summary>Sub-mesh a block is written to: 0 opaque, 1 alpha-tested, 2 translucent water.</summary>
    public enum RenderLayer : byte { Opaque = 0, Cutout = 1, Water = 2 }

    public enum SoundMaterial : byte { Stone, Wood, Dirt, Grass, Sand, Gravel, Glass, Snow, Leaves, Wool }

    public sealed class BlockDef
    {
        public BlockId Id;
        public string Name;
        public BlockShape Shape = BlockShape.Cube;
        public RenderLayer Layer = RenderLayer.Opaque;
        /// <summary>Collides with the player and mobs.</summary>
        public bool Solid = true;
        /// <summary>Full opaque cube: hides neighbour faces and blocks light completely.</summary>
        public bool Opaque = true;
        /// <summary>Stops the direct vertical sky light (heightmap); light still diffuses through when not opaque.</summary>
        public bool StopsSkyLight = true;
        /// <summary>Darkens the corners of neighbouring faces (ambient occlusion).</summary>
        public bool CastsAO = true;
        public byte Emission;
        /// <summary>Seconds to break by hand in survival. Negative means unbreakable.</summary>
        public float Hardness = 0.5f;
        public SoundMaterial Sound = SoundMaterial.Stone;
        public TexId Top, Side, Bottom;
        public BlockId Drop;
        /// <summary>Placing a block into this cell simply overwrites it.</summary>
        public bool Replaceable;
        public bool Targetable = true;
        /// <summary>Appears in the creative palette / can exist as an item.</summary>
        public bool Obtainable = true;
    }

    /// <summary>Block registry plus flat lookup tables used by the mesher and generator threads.</summary>
    public static class Blocks
    {
        public const int Count = (int)BlockId.Count;

        public static readonly BlockDef[] Defs = new BlockDef[256];

        public static readonly bool[] Opaque = new bool[256];
        public static readonly bool[] Solid = new bool[256];
        public static readonly bool[] StopsSky = new bool[256];
        public static readonly bool[] CastsAO = new bool[256];
        public static readonly byte[] Shape = new byte[256];
        public static readonly byte[] Layer = new byte[256];
        public static readonly byte[] Emission = new byte[256];
        public static readonly ushort[] TexTop = new ushort[256];
        public static readonly ushort[] TexSide = new ushort[256];
        public static readonly ushort[] TexBottom = new ushort[256];

        static Blocks()
        {
            Register(new BlockDef
            {
                Id = BlockId.Air, Name = "Ar", Shape = BlockShape.Empty, Solid = false, Opaque = false,
                StopsSkyLight = false, CastsAO = false, Replaceable = true, Targetable = false, Obtainable = false,
                Hardness = 0f
            });

            Cube(BlockId.Grass, "Grama", TexId.GrassTop, TexId.GrassSide, TexId.Dirt, 0.35f, SoundMaterial.Grass, BlockId.Dirt);
            Cube(BlockId.Dirt, "Terra", TexId.Dirt, 0.3f, SoundMaterial.Dirt);
            Cube(BlockId.Stone, "Pedra", TexId.Stone, 0.75f, SoundMaterial.Stone, BlockId.Cobblestone);
            Cube(BlockId.Cobblestone, "Pedregulho", TexId.Cobblestone, 0.8f, SoundMaterial.Stone);
            Cube(BlockId.Sand, "Areia", TexId.Sand, 0.3f, SoundMaterial.Sand);
            Cube(BlockId.Gravel, "Cascalho", TexId.Gravel, 0.35f, SoundMaterial.Gravel);
            Cube(BlockId.OakLog, "Tronco de Carvalho", TexId.OakLogTop, TexId.OakLogSide, TexId.OakLogTop, 0.6f, SoundMaterial.Wood);
            Leaves(BlockId.OakLeaves, "Folhas de Carvalho", TexId.OakLeaves);
            Cube(BlockId.Planks, "Tábuas", TexId.Planks, 0.55f, SoundMaterial.Wood);

            Register(new BlockDef
            {
                Id = BlockId.Glass, Name = "Vidro", Layer = RenderLayer.Cutout, Opaque = false, StopsSkyLight = false,
                CastsAO = false, Hardness = 0.25f, Sound = SoundMaterial.Glass,
                Top = TexId.Glass, Side = TexId.Glass, Bottom = TexId.Glass, Drop = BlockId.Glass
            });

            Register(new BlockDef
            {
                Id = BlockId.Water, Name = "Água", Shape = BlockShape.Liquid, Layer = RenderLayer.Water, Solid = false,
                Opaque = false, StopsSkyLight = true, CastsAO = false, Hardness = -1f, Replaceable = true,
                Targetable = false, Obtainable = false, Sound = SoundMaterial.Dirt,
                Top = TexId.Water, Side = TexId.Water, Bottom = TexId.Water, Drop = BlockId.Air
            });

            Cube(BlockId.Bedrock, "Rocha Matriz", TexId.Bedrock, -1f, SoundMaterial.Stone);
            Defs[(int)BlockId.Bedrock].Obtainable = false;
            Cube(BlockId.Snow, "Neve", TexId.Snow, 0.2f, SoundMaterial.Snow);
            Cube(BlockId.CoalOre, "Minério de Carvão", TexId.CoalOre, 0.9f, SoundMaterial.Stone);
            Cube(BlockId.IronOre, "Minério de Ferro", TexId.IronOre, 1.0f, SoundMaterial.Stone);
            Cube(BlockId.BirchLog, "Tronco de Bétula", TexId.BirchLogTop, TexId.BirchLogSide, TexId.BirchLogTop, 0.6f, SoundMaterial.Wood);
            Leaves(BlockId.BirchLeaves, "Folhas de Bétula", TexId.BirchLeaves);
            Cube(BlockId.SpruceLog, "Tronco de Pinheiro", TexId.SpruceLogTop, TexId.SpruceLogSide, TexId.SpruceLogTop, 0.6f, SoundMaterial.Wood);
            Leaves(BlockId.SpruceLeaves, "Folhas de Pinheiro", TexId.SpruceLeaves);
            Cube(BlockId.StoneBricks, "Tijolos de Pedra", TexId.StoneBricks, 0.8f, SoundMaterial.Stone);
            Cube(BlockId.Bricks, "Tijolos", TexId.Bricks, 0.8f, SoundMaterial.Stone);
            Cube(BlockId.CraftingTable, "Bancada de Trabalho", TexId.CraftingTop, TexId.CraftingSide, TexId.Planks, 0.6f, SoundMaterial.Wood);
            Cube(BlockId.Sandstone, "Arenito", TexId.SandstoneTop, TexId.SandstoneSide, TexId.SandstoneBottom, 0.5f, SoundMaterial.Stone);
            Cube(BlockId.Lamp, "Lâmpada", TexId.Lamp, 0.25f, SoundMaterial.Glass);
            Defs[(int)BlockId.Lamp].Emission = 15;
            Register(Defs[(int)BlockId.Lamp]);
            Cube(BlockId.Wool, "Lã", TexId.Wool, 0.3f, SoundMaterial.Wool);

            Plant(BlockId.TallGrass, "Grama Alta", TexId.TallGrass, BlockId.Air);
            Plant(BlockId.FlowerRed, "Papoula", TexId.FlowerRed, BlockId.FlowerRed);
            Plant(BlockId.FlowerYellow, "Dente-de-leão", TexId.FlowerYellow, BlockId.FlowerYellow);

            for (int i = 0; i < Count; i++)
            {
                if (Defs[i] == null) throw new InvalidOperationException("Block not registered: " + (BlockId)i);
            }
        }

        public static BlockDef Get(BlockId id)
        {
            return Defs[(int)id] ?? Defs[0];
        }

        public static string NameOf(BlockId id)
        {
            return Get(id).Name;
        }

        static void Cube(BlockId id, string name, TexId all, float hardness, SoundMaterial sound, BlockId drop = BlockId.Air)
        {
            Cube(id, name, all, all, all, hardness, sound, drop);
        }

        static void Cube(BlockId id, string name, TexId top, TexId side, TexId bottom, float hardness, SoundMaterial sound, BlockId drop = BlockId.Air)
        {
            Register(new BlockDef
            {
                Id = id, Name = name, Top = top, Side = side, Bottom = bottom, Hardness = hardness, Sound = sound,
                Drop = drop == BlockId.Air ? id : drop
            });
        }

        static void Leaves(BlockId id, string name, TexId tex)
        {
            Register(new BlockDef
            {
                Id = id, Name = name, Layer = RenderLayer.Cutout, Opaque = false, StopsSkyLight = true, CastsAO = true,
                Hardness = 0.15f, Sound = SoundMaterial.Leaves, Top = tex, Side = tex, Bottom = tex, Drop = id
            });
        }

        static void Plant(BlockId id, string name, TexId tex, BlockId drop)
        {
            Register(new BlockDef
            {
                Id = id, Name = name, Shape = BlockShape.Cross, Layer = RenderLayer.Cutout, Solid = false, Opaque = false,
                StopsSkyLight = false, CastsAO = false, Hardness = 0f, Sound = SoundMaterial.Grass, Replaceable = true,
                Top = tex, Side = tex, Bottom = tex, Drop = drop
            });
        }

        static void Register(BlockDef d)
        {
            int i = (int)d.Id;
            Defs[i] = d;
            Opaque[i] = d.Opaque;
            Solid[i] = d.Solid;
            StopsSky[i] = d.StopsSkyLight;
            CastsAO[i] = d.CastsAO;
            Shape[i] = (byte)d.Shape;
            Layer[i] = (byte)d.Layer;
            Emission[i] = d.Emission;
            TexTop[i] = (ushort)d.Top;
            TexSide[i] = (ushort)d.Side;
            TexBottom[i] = (ushort)d.Bottom;
        }

        /// <summary>
        /// Converts a light level pair into a brightness factor. Mirrors MineLight() in MineCommon.hlsl so
        /// entities and particles match the terrain.
        /// </summary>
        public static float Brightness(int sky, int block, float skyDarken)
        {
            float s = Mathf.Max(sky - skyDarken, 0f);
            float sb = Mathf.Pow(0.8f, 15f - s);
            float bb = Mathf.Pow(0.8f, 15f - block);
            return Mathf.Max(Mathf.Max(sb, bb), 0.035f);
        }
    }
}
