using System.Collections.Generic;
using UnityEngine;

namespace Mine
{
    /// <summary>Spawns animals on grass around the player and despawns them when far away.</summary>
    public sealed class MobManager : MonoBehaviour
    {
        public static MobManager Instance { get; private set; }

        public int MaxMobs = 12;
        public bool Active;

        readonly List<Mob> mobs = new List<Mob>();
        float spawnTimer = 1f;

        public int Count => mobs.Count;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            mobs.RemoveAll(m => m == null);
            GameManager gm = GameManager.Instance;
            VoxelWorld world = VoxelWorld.Instance;
            if (!Active || gm == null || gm.Player == null || world == null) return;
            Vector3 p = gm.Player.transform.position;

            for (int i = mobs.Count - 1; i >= 0; i--)
            {
                Mob m = mobs[i];
                Vector3 d = m.Position - p;
                d.y = 0f;
                if (d.sqrMagnitude > 96f * 96f)
                {
                    Destroy(m.gameObject);
                    mobs.RemoveAt(i);
                }
            }

            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f || gm.State != GameState.Playing) return;
            spawnTimer = 1.5f;
            if (mobs.Count >= MaxMobs) return;
            TrySpawnGroup(world, p);
        }

        void TrySpawnGroup(VoxelWorld world, Vector3 center)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(18f, 44f);
            int x = MathUtil.FastFloor(center.x + Mathf.Cos(angle) * dist);
            int z = MathUtil.FastFloor(center.z + Mathf.Sin(angle) * dist);
            var kind = (MobKind)Random.Range(0, 3);
            int group = Random.Range(1, 4);
            for (int i = 0; i < group && mobs.Count < MaxMobs; i++)
            {
                int gx = x + Random.Range(-3, 4), gz = z + Random.Range(-3, 4);
                if (TryFindGround(world, gx, gz, out Vector3 feet)) Spawn(kind, feet);
            }
        }

        static bool TryFindGround(VoxelWorld world, int x, int z, out Vector3 feet)
        {
            feet = default;
            if (!world.IsMeshedAt(x, z)) return false;
            int y = world.GetTopBlockY(x, z, true);
            if (y < 0 || y >= Chunk.Height - 3) return false;
            if (world.GetBlock(x, y, z) != BlockId.Grass) return false;
            if (world.IsSolid(x, y + 1, z) || world.IsSolid(x, y + 2, z)) return false;
            if (world.GetBlock(x, y + 1, z) == BlockId.Water) return false;
            // Animals only spawn under the open sky (not in caves or under overhangs).
            if ((world.GetPackedLight(x, y + 1, z) >> 4) < 13) return false;
            feet = new Vector3(x + 0.5f, y + 1f, z + 0.5f);
            return true;
        }

        public Mob Spawn(MobKind kind, Vector3 feet)
        {
            Mob m = MobFactory.Create(kind, transform);
            m.Place(feet);
            mobs.Add(m);
            return m;
        }

        public Mob Raycast(Vector3 origin, Vector3 dir, float maxDistance, out float distance)
        {
            distance = float.MaxValue;
            Mob best = null;
            for (int i = 0; i < mobs.Count; i++)
            {
                Mob m = mobs[i];
                if (m == null || m.Dead) continue;
                m.GetBounds(out Vector3 min, out Vector3 max);
                if (VoxelPhysics.RayBox(origin, dir, min, max, out float d) && d <= maxDistance && d < distance)
                {
                    distance = d;
                    best = m;
                }
            }
            return best;
        }

        public bool AnyIntersects(Vector3 min, Vector3 max)
        {
            for (int i = 0; i < mobs.Count; i++)
            {
                Mob m = mobs[i];
                if (m == null || m.Dead) continue;
                m.GetBounds(out Vector3 a, out Vector3 b);
                if (VoxelPhysics.BoxesOverlap(min, max, a, b)) return true;
            }
            return false;
        }

        public void Clear()
        {
            foreach (Mob m in mobs)
            {
                if (m != null) Destroy(m.gameObject);
            }
            mobs.Clear();
        }
    }
}
