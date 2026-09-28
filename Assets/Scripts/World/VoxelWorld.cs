using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mine
{
    /// <summary>
    /// Owns all chunks: streams them in/out around the viewer (infinite world), schedules generation and meshing
    /// on the worker pool, uploads finished meshes and exposes block get/set for gameplay.
    /// </summary>
    public sealed class VoxelWorld : MonoBehaviour
    {
        public static VoxelWorld Instance { get; private set; }

        public int Seed { get; private set; }
        public TerrainGenerator Generator { get; private set; }
        public int RenderDistance { get; private set; } = 8;

        /// <summary>Raised on the main thread after a block changes (x, y, z, old, new).</summary>
        public event Action<Vector3Int, BlockId, BlockId> BlockChanged;

        struct MeshResult
        {
            public Chunk Chunk;
            public MeshData Data;
        }

        readonly Dictionary<long, Chunk> chunks = new Dictionary<long, Chunk>(1024);
        Dictionary<long, byte[]> stored = new Dictionary<long, byte[]>();
        WorkerPool pool;
        readonly ConcurrentQueue<Chunk> generatedQueue = new ConcurrentQueue<Chunk>();
        readonly ConcurrentQueue<MeshResult> meshedQueue = new ConcurrentQueue<MeshResult>();
        readonly ConcurrentBag<ChunkMesher> meshers = new ConcurrentBag<ChunkMesher>();
        readonly ConcurrentBag<MeshData> meshDataPool = new ConcurrentBag<MeshData>();
        readonly Stack<GameObject> goPool = new Stack<GameObject>();
        readonly List<long> removeKeys = new List<long>(256);
        readonly List<Chunk> scheduleList = new List<Chunk>(256);
        Material[] materials;
        Transform viewer;
        Transform chunkRoot;
        int viewerCX = int.MinValue;
        int viewerCZ = int.MinValue;
        bool streamingDirty = true;
        Chunk lastChunk;
        int meshJobsInFlight;
        int pooledMeshData;
        Comparison<Chunk> scoreComparison;

        public int LoadedChunks => chunks.Count;
        public int MeshedChunks { get; private set; }
        public int PendingJobs => pool != null ? pool.Pending : 0;
        public int WorkerThreads => pool != null ? pool.ThreadCount : 0;

        public static long Key(int cx, int cz)
        {
            return ((long)cx << 32) | (uint)cz;
        }

        public void Init(int seed, Dictionary<long, byte[]> storedChunks, int renderDistance, Material[] chunkMaterials)
        {
            Instance = this;
            Seed = seed;
            Generator = new TerrainGenerator(seed);
            stored = storedChunks ?? new Dictionary<long, byte[]>();
            RenderDistance = renderDistance;
            materials = chunkMaterials;
            int workers = Mathf.Clamp(SystemInfo.processorCount - 2, 1, 6);
            pool = new WorkerPool(workers);
            chunkRoot = new GameObject("Chunks").transform;
            chunkRoot.SetParent(transform, false);
        }

        public void SetViewer(Transform t)
        {
            viewer = t;
            streamingDirty = true;
        }

        public void SetRenderDistance(int distance)
        {
            RenderDistance = Mathf.Clamp(distance, 2, 16);
            streamingDirty = true;
            foreach (Chunk c in chunks.Values)
            {
                if (c.Generated && !c.HasMesh) c.NeedsMesh = true;
            }
        }

        void Update()
        {
            if (viewer == null || pool == null) return;
            Vector3 vp = viewer.position;
            int vcx = MathUtil.FastFloor(vp.x) >> 4;
            int vcz = MathUtil.FastFloor(vp.z) >> 4;
            pool.SetViewer(vcx, vcz);
            if (vcx != viewerCX || vcz != viewerCZ || streamingDirty)
            {
                viewerCX = vcx;
                viewerCZ = vcz;
                streamingDirty = false;
                UpdateStreaming();
            }
            DrainGenerated();
            ScheduleMeshes();
            ApplyMeshes();
        }

        // ------------------------------------------------------------------ streaming

        void UpdateStreaming()
        {
            int r = RenderDistance + 1;
            int r2 = r * r + r;
            for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dz * dz > r2) continue;
                int cx = viewerCX + dx, cz = viewerCZ + dz;
                long key = Key(cx, cz);
                if (chunks.ContainsKey(key)) continue;

                var chunk = new Chunk(cx, cz);
                chunks.Add(key, chunk);
                if (stored.TryGetValue(key, out byte[] data) && data != null && data.Length == Chunk.Volume)
                {
                    Buffer.BlockCopy(data, 0, chunk.Voxels, 0, Chunk.Volume);
                    chunk.RecountMeta();
                    chunk.Modified = true;
                    chunk.Generated = true;
                    chunk.NeedsMesh = true;
                }
                else
                {
                    TerrainGenerator gen = Generator;
                    pool.Enqueue(() =>
                    {
                        if (chunk.Disposed) return;
                        gen.Generate(chunk);
                        generatedQueue.Enqueue(chunk);
                    }, cx, cz, 0);
                }
            }

            int u = RenderDistance + 3;
            int u2 = u * u + u;
            removeKeys.Clear();
            foreach (KeyValuePair<long, Chunk> kv in chunks)
            {
                int dx = kv.Value.X - viewerCX, dz = kv.Value.Z - viewerCZ;
                if (dx * dx + dz * dz > u2) removeKeys.Add(kv.Key);
            }
            for (int i = 0; i < removeKeys.Count; i++)
            {
                long key = removeKeys[i];
                Unload(chunks[key]);
                chunks.Remove(key);
            }
        }

        void Unload(Chunk c)
        {
            c.Disposed = true;
            if (lastChunk == c) lastChunk = null;
            if (c.Modified && c.Generated) stored[c.Key] = c.Voxels;
            if (c.Go != null)
            {
                c.Filter.sharedMesh = null;
                c.Go.SetActive(false);
                goPool.Push(c.Go);
                c.Go = null;
            }
            if (c.Mesh != null)
            {
                Destroy(c.Mesh);
                c.Mesh = null;
            }
            if (c.HasMesh) MeshedChunks--;
            c.HasMesh = false;
        }

        void DrainGenerated()
        {
            while (generatedQueue.TryDequeue(out Chunk c))
            {
                if (c.Disposed) continue;
                c.Generated = true;
                c.NeedsMesh = true;
            }
        }

        int ChunkScore(Chunk c)
        {
            int dx = c.X - viewerCX, dz = c.Z - viewerCZ;
            return (c.UrgentMesh ? -100000 : 0) + dx * dx + dz * dz;
        }

        void ScheduleMeshes()
        {
            int r = RenderDistance;
            int r2 = r * r + r;
            scheduleList.Clear();
            foreach (Chunk c in chunks.Values)
            {
                if (!c.Generated || !c.NeedsMesh || c.MeshInFlight) continue;
                int dx = c.X - viewerCX, dz = c.Z - viewerCZ;
                if (dx * dx + dz * dz > r2) continue;
                scheduleList.Add(c);
            }
            if (scheduleList.Count == 0) return;
            if (scoreComparison == null) scoreComparison = (a, b) => ChunkScore(a).CompareTo(ChunkScore(b));
            scheduleList.Sort(scoreComparison);

            // Keep the backlog small so memory stays bounded and the nearest chunks always go first.
            int maxInFlight = Mathf.Max(4, pool.ThreadCount * 3);
            for (int s = 0; s < scheduleList.Count; s++)
            {
                Chunk c = scheduleList[s];
                if (meshJobsInFlight >= maxInFlight && !c.UrgentMesh) break;
                var n = new Chunk[9];
                bool ok = true;
                for (int oz = -1; oz <= 1 && ok; oz++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (!chunks.TryGetValue(Key(c.X + ox, c.Z + oz), out Chunk nc) || !nc.Generated)
                    {
                        ok = false;
                        break;
                    }
                    n[(oz + 1) * 3 + (ox + 1)] = nc;
                }
                if (!ok) continue;

                c.NeedsMesh = false;
                c.MeshInFlight = true;
                meshJobsInFlight++;
                bool urgent = c.UrgentMesh;
                c.UrgentMesh = false;
                pool.Enqueue(() => MeshJob(n), c.X, c.Z, urgent ? -1000000 : -1);
            }
        }

        void RecycleMeshData(MeshData md)
        {
            // Pool only a handful; extra buffers from load bursts are left to the GC.
            if (System.Threading.Interlocked.Increment(ref pooledMeshData) <= 12)
            {
                meshDataPool.Add(md);
            }
            else
            {
                System.Threading.Interlocked.Decrement(ref pooledMeshData);
            }
        }

        MeshData RentMeshData()
        {
            if (meshDataPool.TryTake(out MeshData md))
            {
                System.Threading.Interlocked.Decrement(ref pooledMeshData);
                return md;
            }
            return new MeshData();
        }

        void MeshJob(Chunk[] n)
        {
            Chunk c = n[4];
            if (c.Disposed)
            {
                meshedQueue.Enqueue(new MeshResult { Chunk = c, Data = null });
                return;
            }
            if (!meshers.TryTake(out ChunkMesher mesher)) mesher = new ChunkMesher();
            MeshData md = RentMeshData();
            try
            {
                mesher.Build(n, md);
                meshedQueue.Enqueue(new MeshResult { Chunk = c, Data = md });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                RecycleMeshData(md);
                meshedQueue.Enqueue(new MeshResult { Chunk = c, Data = null });
            }
            finally
            {
                meshers.Add(mesher);
            }
        }

        void ApplyMeshes()
        {
            float start = Time.realtimeSinceStartup;
            while (meshedQueue.TryDequeue(out MeshResult r))
            {
                Chunk c = r.Chunk;
                c.MeshInFlight = false;
                meshJobsInFlight--;
                if (r.Data != null)
                {
                    if (!c.Disposed) UploadMesh(c, r.Data);
                    RecycleMeshData(r.Data);
                }
                if (Time.realtimeSinceStartup - start > 0.005f) break;
            }
        }

        void UploadMesh(Chunk c, MeshData d)
        {
            if (c.Go == null) CreateChunkObject(c);
            Mesh old = c.Mesh;
            if (!c.HasMesh) MeshedChunks++;
            c.HasMesh = true;

            if (d.Vertices.Count == 0)
            {
                c.Filter.sharedMesh = null;
                c.Renderer.enabled = false;
                c.Mesh = null;
                if (old != null) Destroy(old);
                return;
            }

            var mesh = new Mesh { name = "Chunk" };
            mesh.indexFormat = d.Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(d.Vertices);
            mesh.SetUVs(0, d.UVs);
            mesh.SetColors(d.Colors);
            mesh.subMeshCount = 3;
            for (int s = 0; s < 3; s++) mesh.SetIndices(d.Indices[s], MeshTopology.Triangles, s, false);
            float minY = d.MinY, maxY = Mathf.Max(d.MaxY, d.MinY + 1f);
            mesh.bounds = new Bounds(new Vector3(8f, (minY + maxY) * 0.5f, 8f), new Vector3(16f, maxY - minY, 16f));
            mesh.UploadMeshData(true);

            c.Filter.sharedMesh = mesh;
            c.Renderer.enabled = true;
            c.Mesh = mesh;
            if (old != null) Destroy(old);
        }

        void CreateChunkObject(Chunk c)
        {
            GameObject go = goPool.Count > 0 ? goPool.Pop() : NewChunkObject();
            go.name = "Chunk " + c.X + "," + c.Z;
            go.transform.position = new Vector3(c.X * Chunk.Size, 0f, c.Z * Chunk.Size);
            go.SetActive(true);
            c.Go = go;
            c.Filter = go.GetComponent<MeshFilter>();
            c.Renderer = go.GetComponent<MeshRenderer>();
        }

        GameObject NewChunkObject()
        {
            var go = new GameObject("Chunk", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(chunkRoot, false);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterials = materials;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return go;
        }

        // ------------------------------------------------------------------ queries

        public Chunk GetChunk(int cx, int cz)
        {
            Chunk lc = lastChunk;
            if (lc != null && lc.X == cx && lc.Z == cz) return lc;
            if (chunks.TryGetValue(Key(cx, cz), out Chunk c))
            {
                lastChunk = c;
                return c;
            }
            return null;
        }

        public bool IsGeneratedAt(int x, int z)
        {
            Chunk c = GetChunk(x >> 4, z >> 4);
            return c != null && c.Generated;
        }

        public bool IsMeshedAt(int x, int z)
        {
            Chunk c = GetChunk(x >> 4, z >> 4);
            return c != null && c.HasMesh;
        }

        public BlockId GetBlock(int x, int y, int z)
        {
            if ((uint)y >= Chunk.Height) return BlockId.Air;
            Chunk c = GetChunk(x >> 4, z >> 4);
            if (c == null || !c.Generated) return BlockId.Air;
            return (BlockId)c.Voxels[Chunk.Index(x & 15, y, z & 15)];
        }

        public BlockId GetBlock(Vector3Int p)
        {
            return GetBlock(p.x, p.y, p.z);
        }

        /// <summary>Solidity test used by physics; unloaded chunks count as solid walls.</summary>
        public bool IsSolid(int x, int y, int z)
        {
            if (y < 0) return true;
            if (y >= Chunk.Height) return false;
            Chunk c = GetChunk(x >> 4, z >> 4);
            if (c == null || !c.Generated) return true;
            return Blocks.Solid[c.Voxels[Chunk.Index(x & 15, y, z & 15)]];
        }

        /// <summary>Packed light (sky &lt;&lt; 4 | block) at a cell; full sky light when unknown.</summary>
        public int GetPackedLight(int x, int y, int z)
        {
            if (y >= Chunk.Height) return 15 << 4;
            if (y < 0) return 0;
            Chunk c = GetChunk(x >> 4, z >> 4);
            if (c == null || c.Light == null) return 15 << 4;
            return c.Light[Chunk.Index(x & 15, y, z & 15)];
        }

        public float GetBrightness(Vector3 pos, float skyDarken)
        {
            int packed = GetPackedLight(MathUtil.FastFloor(pos.x), MathUtil.FastFloor(pos.y), MathUtil.FastFloor(pos.z));
            return Blocks.Brightness(packed >> 4, packed & 15, skyDarken);
        }

        /// <summary>Highest non-air block in a column of a generated chunk, or -1.</summary>
        public int GetTopBlockY(int x, int z, bool solidOnly)
        {
            Chunk c = GetChunk(x >> 4, z >> 4);
            if (c == null || !c.Generated) return -1;
            int lx = x & 15, lz = z & 15;
            for (int y = Math.Min(c.MaxY, Chunk.Height - 1); y >= 0; y--)
            {
                byte b = c.Voxels[Chunk.Index(lx, y, lz)];
                if (b == 0) continue;
                if (solidOnly && !Blocks.Solid[b]) continue;
                return y;
            }
            return -1;
        }

        /// <summary>True when the chunk under <paramref name="pos"/> and its ring of neighbours have meshes.</summary>
        public bool IsAreaReady(Vector3 pos, int radius)
        {
            int cx = MathUtil.FastFloor(pos.x) >> 4, cz = MathUtil.FastFloor(pos.z) >> 4;
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                Chunk c = GetChunk(cx + dx, cz + dz);
                if (c == null || !c.HasMesh) return false;
            }
            return true;
        }

        public float AreaProgress(Vector3 pos, int radius)
        {
            int cx = MathUtil.FastFloor(pos.x) >> 4, cz = MathUtil.FastFloor(pos.z) >> 4;
            int total = 0, ready = 0;
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                total++;
                Chunk c = GetChunk(cx + dx, cz + dz);
                if (c != null && c.HasMesh) ready++;
            }
            return total == 0 ? 1f : ready / (float)total;
        }

        // ------------------------------------------------------------------ edits

        public bool SetBlock(Vector3Int p, BlockId id)
        {
            return SetBlock(p.x, p.y, p.z, id);
        }

        public bool SetBlock(int x, int y, int z, BlockId id)
        {
            if ((uint)y >= Chunk.Height) return false;
            Chunk c = GetChunk(x >> 4, z >> 4);
            if (c == null || !c.Generated) return false;
            int lx = x & 15, lz = z & 15;
            int idx = Chunk.Index(lx, y, lz);
            byte old = c.Voxels[idx];
            if (old == (byte)id) return false;

            c.Voxels[idx] = (byte)id;
            c.Modified = true;
            if (Blocks.Emission[old] > 0) c.EmitterCount--;
            if (Blocks.Emission[(byte)id] > 0) c.EmitterCount++;
            if (id != BlockId.Air && y > c.MaxY) c.MaxY = y;

            // Light can travel up to 15 blocks, so the whole 3x3 neighbourhood may need new lighting.
            // Chunks sharing the touched border are remeshed with priority to avoid visible holes.
            for (int oz = -1; oz <= 1; oz++)
            for (int ox = -1; ox <= 1; ox++)
            {
                Chunk n = ox == 0 && oz == 0 ? c : GetChunk(c.X + ox, c.Z + oz);
                if (n == null || !n.Generated) continue;
                bool touchX = ox == 0 || (ox == -1 && lx == 0) || (ox == 1 && lx == 15);
                bool touchZ = oz == 0 || (oz == -1 && lz == 0) || (oz == 1 && lz == 15);
                n.NeedsMesh = true;
                if (touchX && touchZ) n.UrgentMesh = true;
            }

            BlockChanged?.Invoke(new Vector3Int(x, y, z), (BlockId)old, id);
            return true;
        }

        // ------------------------------------------------------------------ persistence

        /// <summary>Snapshot of every chunk that differs from the generator output.</summary>
        public Dictionary<long, byte[]> CollectModifiedChunks()
        {
            var result = new Dictionary<long, byte[]>(stored);
            foreach (Chunk c in chunks.Values)
            {
                if (c.Modified && c.Generated) result[c.Key] = (byte[])c.Voxels.Clone();
            }
            return result;
        }

        void OnDestroy()
        {
            pool?.Dispose();
            pool = null;
            foreach (Chunk c in chunks.Values)
            {
                c.Disposed = true;
                if (c.Mesh != null) Destroy(c.Mesh);
            }
            chunks.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
