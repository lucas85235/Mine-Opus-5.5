using NUnit.Framework;
using UnityEngine;

namespace Mine.Tests
{
    public class RleTests
    {
        [Test]
        public void RoundTrip_PreservesRandomData()
        {
            var rng = new System.Random(3);
            var data = new byte[Chunk.Volume];
            for (int i = 0; i < data.Length; i++) data[i] = (byte)(rng.Next(4) == 0 ? rng.Next(256) : 7);
            byte[] decoded = Rle.Decode(Rle.Encode(data), Chunk.Volume);
            CollectionAssert.AreEqual(data, decoded);
        }

        [Test]
        public void Encode_CompressesUniformChunk()
        {
            var data = new byte[Chunk.Volume];
            byte[] encoded = Rle.Encode(data);
            Assert.LessOrEqual(encoded.Length, 2 * (Chunk.Volume / 255 + 1));
        }
    }

    public class InventoryTests
    {
        [Test]
        public void Add_FillsStacksUpTo64_ThenNewSlots()
        {
            var inv = new Inventory();
            Assert.AreEqual(0, inv.Add(BlockId.Dirt, 100));
            Assert.AreEqual(64, inv.Slots[0].Count);
            Assert.AreEqual(36, inv.Slots[1].Count);
            Assert.AreEqual(100, inv.CountOf(BlockId.Dirt));
        }

        [Test]
        public void Add_ReturnsLeftover_WhenFull()
        {
            var inv = new Inventory();
            int left = inv.Add(BlockId.Stone, Inventory.Size * Inventory.MaxStack + 10);
            Assert.AreEqual(10, left);
            Assert.IsFalse(inv.CanAdd(BlockId.Sand, 1));
        }

        [Test]
        public void Remove_TakesAcrossStacks_AndClearsEmptySlots()
        {
            var inv = new Inventory();
            inv.Add(BlockId.Planks, 70);
            Assert.IsTrue(inv.Remove(BlockId.Planks, 68));
            Assert.AreEqual(2, inv.CountOf(BlockId.Planks));
            Assert.IsFalse(inv.Remove(BlockId.Planks, 3));
        }

        [Test]
        public void ConsumeSelected_EmptiesSlotAtZero()
        {
            var inv = new Inventory();
            inv.Slots[0] = new ItemStack(BlockId.Glass, 1);
            inv.ConsumeSelected(1);
            Assert.IsTrue(inv.Slots[0].IsEmpty);
        }

        [Test]
        public void Selected_WrapsAroundHotbar()
        {
            var inv = new Inventory();
            inv.Selected = -1;
            Assert.AreEqual(8, inv.Selected);
            inv.Selected = 9;
            Assert.AreEqual(0, inv.Selected);
        }
    }

    public class RecipeTests
    {
        static Recipe Find(BlockId result, BlockId ingredient)
        {
            foreach (Recipe r in Recipes.All)
            {
                if (r.Result == result && r.Ingredients[0].Id == ingredient) return r;
            }
            return null;
        }

        [Test]
        public void Craft_PlanksFromLog()
        {
            var inv = new Inventory();
            inv.Add(BlockId.OakLog, 1);
            Assert.IsTrue(Recipes.Craft(Find(BlockId.Planks, BlockId.OakLog), inv, false));
            Assert.AreEqual(0, inv.CountOf(BlockId.OakLog));
            Assert.AreEqual(4, inv.CountOf(BlockId.Planks));
        }

        [Test]
        public void Craft_TableRecipe_NeedsCraftingTable()
        {
            var inv = new Inventory();
            inv.Add(BlockId.Sand, 1);
            Recipe glass = Find(BlockId.Glass, BlockId.Sand);
            Assert.IsFalse(Recipes.Craft(glass, inv, false));
            Assert.IsTrue(Recipes.Craft(glass, inv, true));
            Assert.AreEqual(1, inv.CountOf(BlockId.Glass));
        }

        [Test]
        public void Craft_FailsWithoutIngredients()
        {
            var inv = new Inventory();
            Assert.IsFalse(Recipes.Craft(Find(BlockId.CraftingTable, BlockId.Planks), inv, true));
        }
    }

    public class TerrainTests
    {
        [Test]
        public void SameSeed_GeneratesIdenticalChunks()
        {
            var a = new Chunk(3, -2);
            var b = new Chunk(3, -2);
            new TerrainGenerator(99).Generate(a);
            new TerrainGenerator(99).Generate(b);
            CollectionAssert.AreEqual(a.Voxels, b.Voxels);
        }

        [Test]
        public void DifferentSeeds_GenerateDifferentChunks()
        {
            var a = new Chunk(0, 0);
            var b = new Chunk(0, 0);
            new TerrainGenerator(1).Generate(a);
            new TerrainGenerator(2).Generate(b);
            CollectionAssert.AreNotEqual(a.Voxels, b.Voxels);
        }

        [Test]
        public void Chunk_HasBedrockFloor_AndHeightsInRange()
        {
            var gen = new TerrainGenerator(1234);
            var c = new Chunk(5, 5);
            gen.Generate(c);
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
                Assert.AreEqual((byte)BlockId.Bedrock, c.Voxels[Chunk.Index(x, 0, z)]);
            for (int i = 0; i < 500; i++)
            {
                int h = gen.Height(i * 37 - 5000, i * 91 - 3000);
                Assert.That(h, Is.InRange(4, TerrainGenerator.MaxTerrain));
            }
        }

        [Test]
        public void Terrain_HasRelief()
        {
            var gen = new TerrainGenerator(777);
            int min = int.MaxValue, max = int.MinValue;
            for (int i = 0; i < 2000; i++)
            {
                int h = gen.Height((i % 50) * 40, (i / 50) * 40);
                min = Mathf.Min(min, h);
                max = Mathf.Max(max, h);
            }
            Assert.Greater(max - min, 30, "expected mountains and valleys");
        }
    }

    public class MesherTests
    {
        static Chunk[] EmptyNeighbourhood()
        {
            var n = new Chunk[9];
            for (int i = 0; i < 9; i++) n[i] = new Chunk(i % 3 - 1, i / 3 - 1) { Generated = true };
            return n;
        }

        static MeshData Mesh(Chunk[] n)
        {
            var md = new MeshData();
            new ChunkMesher().Build(n, md);
            return md;
        }

        [Test]
        public void SingleBlock_HasSixFaces()
        {
            Chunk[] n = EmptyNeighbourhood();
            n[4].Voxels[Chunk.Index(8, 64, 8)] = (byte)BlockId.Stone;
            n[4].MaxY = 64;
            MeshData md = Mesh(n);
            Assert.AreEqual(24, md.Vertices.Count);
            Assert.AreEqual(36, md.Indices[(int)RenderLayer.Opaque].Count);
        }

        [Test]
        public void AdjacentBlocks_HideSharedFaces()
        {
            Chunk[] n = EmptyNeighbourhood();
            n[4].Voxels[Chunk.Index(8, 64, 8)] = (byte)BlockId.Stone;
            n[4].Voxels[Chunk.Index(9, 64, 8)] = (byte)BlockId.Dirt;
            n[4].MaxY = 64;
            Assert.AreEqual(10 * 4, Mesh(n).Vertices.Count);
        }

        [Test]
        public void FaceAgainstNeighbourChunkBlock_IsCulled()
        {
            Chunk[] n = EmptyNeighbourhood();
            n[4].Voxels[Chunk.Index(15, 64, 8)] = (byte)BlockId.Stone;
            n[5].Voxels[Chunk.Index(0, 64, 8)] = (byte)BlockId.Stone; // east neighbour
            n[4].MaxY = 64;
            Assert.AreEqual(5 * 4, Mesh(n).Vertices.Count);
        }

        [Test]
        public void Water_GoesToWaterSubmesh_WithLoweredSurface()
        {
            Chunk[] n = EmptyNeighbourhood();
            n[4].Voxels[Chunk.Index(4, 10, 4)] = (byte)BlockId.Water;
            n[4].MaxY = 10;
            MeshData md = Mesh(n);
            Assert.AreEqual(0, md.Indices[(int)RenderLayer.Opaque].Count);
            Assert.AreEqual(36, md.Indices[(int)RenderLayer.Water].Count);
            Assert.AreEqual(10.875f, md.MaxY, 1e-4f);
        }

        [Test]
        public void SealedRoom_IsDark_OpenAir_IsLit()
        {
            Chunk[] n = EmptyNeighbourhood();
            for (int x = 7; x <= 9; x++)
            for (int y = 63; y <= 65; y++)
            for (int z = 7; z <= 9; z++)
                n[4].Voxels[Chunk.Index(x, y, z)] = (byte)BlockId.Stone;
            n[4].Voxels[Chunk.Index(8, 64, 8)] = 0;
            n[4].MaxY = 65;
            Mesh(n);
            Assert.AreEqual(0, n[4].Light[Chunk.Index(8, 64, 8)] >> 4, "enclosed cell must not receive sky light");
            Assert.AreEqual(15, n[4].Light[Chunk.Index(2, 64, 2)] >> 4, "open air must be fully sky lit");
        }

        [Test]
        public void Lamp_EmitsBlockLight_FallingOffWithDistance()
        {
            Chunk[] n = EmptyNeighbourhood();
            n[4].Voxels[Chunk.Index(8, 30, 8)] = (byte)BlockId.Lamp;
            n[4].EmitterCount = 1;
            n[4].MaxY = 30;
            Mesh(n);
            Assert.AreEqual(14, n[4].Light[Chunk.Index(9, 30, 8)] & 15);
            Assert.AreEqual(12, n[4].Light[Chunk.Index(11, 30, 8)] & 15);
        }
    }

    public class PlayerSkinTests
    {
        [Test]
        public void SkinFaces_FitInAtlas_AndDoNotOverlap()
        {
            PlayerSkin skin = PlayerSkin.Create();
            Assert.AreEqual(64, skin.Texture.width);
            var all = new System.Collections.Generic.List<Rect>();
            foreach (Rect[] part in new[] { skin.Head, skin.Body, skin.ArmRight, skin.ArmLeft, skin.LegRight, skin.LegLeft, skin.Backpack })
            {
                Assert.AreEqual(6, part.Length, "one rect per box face");
                all.AddRange(part);
            }
            for (int i = 0; i < all.Count; i++)
            {
                Assert.That(all[i].xMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(all[i].yMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(all[i].xMax, Is.LessThanOrEqualTo(1f));
                Assert.That(all[i].yMax, Is.LessThanOrEqualTo(1f));
                for (int j = i + 1; j < all.Count; j++) Assert.IsFalse(all[i].Overlaps(all[j]), "faces " + i + " and " + j + " overlap");
            }
            Object.DestroyImmediate(skin.Texture);
        }

        [Test]
        public void FaceHasEyes()
        {
            PlayerSkin skin = PlayerSkin.Create();
            Rect front = skin.Head[4];
            // Eye row (j = 4) of the 8x8 face: white at i = 1, pupil at i = 2.
            int x0 = Mathf.RoundToInt(front.xMin * 64f), y0 = Mathf.RoundToInt(front.yMin * 64f);
            Color32 white = skin.Texture.GetPixel(x0 + 1, y0 + 4);
            Color32 pupil = skin.Texture.GetPixel(x0 + 2, y0 + 4);
            Assert.Greater(white.r, 230);
            Assert.Greater(pupil.b, pupil.r);
            Object.DestroyImmediate(skin.Texture);
        }
    }

    public class PhysicsMathTests
    {
        [Test]
        public void RayBox_ReportsEntryDistance()
        {
            bool hit = VoxelPhysics.RayBox(new Vector3(0, 0.5f, 0.5f), Vector3.right, new Vector3(2, 0, 0), new Vector3(3, 1, 1), out float d);
            Assert.IsTrue(hit);
            Assert.AreEqual(2f, d, 1e-4f);
        }

        [Test]
        public void RayBox_MissesWhenPointingAway()
        {
            Assert.IsFalse(VoxelPhysics.RayBox(new Vector3(0, 0.5f, 0.5f), Vector3.left, new Vector3(2, 0, 0), new Vector3(3, 1, 1), out _));
        }

        [Test]
        public void BoxesTouchingFaces_DoNotOverlap()
        {
            Assert.IsFalse(VoxelPhysics.BoxesOverlap(Vector3.zero, Vector3.one, new Vector3(1, 0, 0), new Vector3(2, 1, 1)));
            Assert.IsTrue(VoxelPhysics.BoxesOverlap(Vector3.zero, Vector3.one, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(2, 2, 2)));
        }
    }
}
