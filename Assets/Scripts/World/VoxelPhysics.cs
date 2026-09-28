using UnityEngine;

namespace Mine
{
    public struct BlockHit
    {
        public Vector3Int Block;
        public Vector3Int Normal;
        public float Distance;
        public BlockId Id;
    }

    /// <summary>Axis-aligned box collision against the voxel grid and DDA ray casting (no Unity colliders needed).</summary>
    public static class VoxelPhysics
    {
        const float Eps = 0.001f;

        /// <summary>Amanatides-Woo voxel traversal. Ignores air and water.</summary>
        public static bool Raycast(VoxelWorld world, Vector3 origin, Vector3 dir, float maxDist, out BlockHit hit)
        {
            return Traverse(world, origin, dir, maxDist, false, out hit);
        }

        /// <summary>Same traversal, but only stops at solid (collidable) blocks; plants and water are ignored.</summary>
        public static bool RaycastSolid(VoxelWorld world, Vector3 origin, Vector3 dir, float maxDist, out float distance)
        {
            bool hit = Traverse(world, origin, dir, maxDist, true, out BlockHit h);
            distance = hit ? h.Distance : maxDist;
            return hit;
        }

        static bool Traverse(VoxelWorld world, Vector3 origin, Vector3 dir, float maxDist, bool solidOnly, out BlockHit hit)
        {
            hit = default;
            if (dir.sqrMagnitude < 1e-8f) return false;
            dir.Normalize();

            int x = MathUtil.FastFloor(origin.x);
            int y = MathUtil.FastFloor(origin.y);
            int z = MathUtil.FastFloor(origin.z);
            int stepX = dir.x > 0 ? 1 : (dir.x < 0 ? -1 : 0);
            int stepY = dir.y > 0 ? 1 : (dir.y < 0 ? -1 : 0);
            int stepZ = dir.z > 0 ? 1 : (dir.z < 0 ? -1 : 0);
            float tDeltaX = stepX != 0 ? Mathf.Abs(1f / dir.x) : float.PositiveInfinity;
            float tDeltaY = stepY != 0 ? Mathf.Abs(1f / dir.y) : float.PositiveInfinity;
            float tDeltaZ = stepZ != 0 ? Mathf.Abs(1f / dir.z) : float.PositiveInfinity;
            float tMaxX = stepX > 0 ? (x + 1 - origin.x) * tDeltaX : (stepX < 0 ? (origin.x - x) * tDeltaX : float.PositiveInfinity);
            float tMaxY = stepY > 0 ? (y + 1 - origin.y) * tDeltaY : (stepY < 0 ? (origin.y - y) * tDeltaY : float.PositiveInfinity);
            float tMaxZ = stepZ > 0 ? (z + 1 - origin.z) * tDeltaZ : (stepZ < 0 ? (origin.z - z) * tDeltaZ : float.PositiveInfinity);

            Vector3Int normal = Vector3Int.zero;
            float t = 0f;
            for (int i = 0; i < 512; i++)
            {
                BlockId id = world.GetBlock(x, y, z);
                if (solidOnly ? Blocks.Solid[(int)id] : Blocks.Get(id).Targetable)
                {
                    hit.Block = new Vector3Int(x, y, z);
                    hit.Normal = normal;
                    hit.Distance = t;
                    hit.Id = id;
                    return true;
                }

                if (tMaxX < tMaxY && tMaxX < tMaxZ)
                {
                    if (tMaxX > maxDist) break;
                    x += stepX;
                    t = tMaxX;
                    tMaxX += tDeltaX;
                    normal = new Vector3Int(-stepX, 0, 0);
                }
                else if (tMaxY < tMaxZ)
                {
                    if (tMaxY > maxDist) break;
                    y += stepY;
                    t = tMaxY;
                    tMaxY += tDeltaY;
                    normal = new Vector3Int(0, -stepY, 0);
                }
                else
                {
                    if (tMaxZ > maxDist) break;
                    z += stepZ;
                    t = tMaxZ;
                    tMaxZ += tDeltaZ;
                    normal = new Vector3Int(0, 0, -stepZ);
                }
            }
            return false;
        }

        public static bool BoxOverlapsSolid(VoxelWorld world, Vector3 min, Vector3 max)
        {
            int x0 = MathUtil.FastFloor(min.x + Eps), x1 = MathUtil.FastFloor(max.x - Eps);
            int y0 = MathUtil.FastFloor(min.y + Eps), y1 = MathUtil.FastFloor(max.y - Eps);
            int z0 = MathUtil.FastFloor(min.z + Eps), z1 = MathUtil.FastFloor(max.z - Eps);
            for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (world.IsSolid(x, y, z)) return true;
            }
            return false;
        }

        public static bool BoxTouchesBlock(VoxelWorld world, Vector3 min, Vector3 max, BlockId id)
        {
            int x0 = MathUtil.FastFloor(min.x), x1 = MathUtil.FastFloor(max.x);
            int y0 = MathUtil.FastFloor(min.y), y1 = MathUtil.FastFloor(max.y);
            int z0 = MathUtil.FastFloor(min.z), z1 = MathUtil.FastFloor(max.z);
            for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (world.GetBlock(x, y, z) == id) return true;
            }
            return false;
        }

        static void Box(Vector3 feet, float halfW, float height, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(feet.x - halfW, feet.y, feet.z - halfW);
            max = new Vector3(feet.x + halfW, feet.y + height, feet.z + halfW);
        }

        static bool Blocked(VoxelWorld w, Vector3 feet, float halfW, float height)
        {
            Box(feet, halfW, height, out Vector3 min, out Vector3 max);
            return BoxOverlapsSolid(w, min, max);
        }

        static bool HasGroundBelow(VoxelWorld w, Vector3 feet, float halfW)
        {
            var min = new Vector3(feet.x - halfW, feet.y - 0.3f, feet.z - halfW);
            var max = new Vector3(feet.x + halfW, feet.y - 0.01f, feet.z + halfW);
            return BoxOverlapsSolid(w, min, max);
        }

        /// <summary>
        /// Moves a box (feet position = bottom centre) by velocity * dt, resolving collisions one axis at a time.
        /// Velocity components are zeroed on impact. <paramref name="keepOnEdges"/> implements sneaking.
        /// </summary>
        public static void Move(VoxelWorld w, ref Vector3 feet, ref Vector3 velocity, float halfW, float height, float dt,
            bool keepOnEdges, out bool grounded, out bool hitWall, out bool hitCeiling)
        {
            grounded = false;
            hitWall = false;
            hitCeiling = false;
            Vector3 delta = velocity * dt;
            float largest = Mathf.Max(Mathf.Abs(delta.x), Mathf.Max(Mathf.Abs(delta.y), Mathf.Abs(delta.z)));
            int steps = Mathf.Clamp(Mathf.CeilToInt(largest / 0.4f), 1, 32);
            Vector3 d = delta / steps;

            for (int s = 0; s < steps; s++)
            {
                // Y axis
                if (d.y != 0f)
                {
                    feet.y += d.y;
                    if (Blocked(w, feet, halfW, height))
                    {
                        if (d.y < 0f)
                        {
                            feet.y = Mathf.Floor(feet.y + Eps) + 1f;
                            grounded = true;
                        }
                        else
                        {
                            feet.y = Mathf.Floor(feet.y + height - Eps) - height;
                            hitCeiling = true;
                        }
                        velocity.y = 0f;
                        d.y = 0f;
                    }
                }

                bool onGround = keepOnEdges && HasGroundBelow(w, feet, halfW);

                // X axis
                if (d.x != 0f)
                {
                    float oldX = feet.x;
                    feet.x += d.x;
                    if (Blocked(w, feet, halfW, height))
                    {
                        feet.x = d.x > 0f ? Mathf.Floor(feet.x + halfW - Eps) - halfW : Mathf.Floor(feet.x - halfW + Eps) + 1f + halfW;
                        velocity.x = 0f;
                        d.x = 0f;
                        hitWall = true;
                    }
                    else if (onGround && !HasGroundBelow(w, feet, halfW))
                    {
                        feet.x = oldX;
                        velocity.x = 0f;
                        d.x = 0f;
                    }
                }

                // Z axis
                if (d.z != 0f)
                {
                    float oldZ = feet.z;
                    feet.z += d.z;
                    if (Blocked(w, feet, halfW, height))
                    {
                        feet.z = d.z > 0f ? Mathf.Floor(feet.z + halfW - Eps) - halfW : Mathf.Floor(feet.z - halfW + Eps) + 1f + halfW;
                        velocity.z = 0f;
                        d.z = 0f;
                        hitWall = true;
                    }
                    else if (onGround && !HasGroundBelow(w, feet, halfW))
                    {
                        feet.z = oldZ;
                        velocity.z = 0f;
                        d.z = 0f;
                    }
                }
            }
        }

        public static bool BoxesOverlap(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax)
        {
            const float e = 0.0005f;
            return aMin.x < bMax.x - e && aMax.x > bMin.x + e &&
                   aMin.y < bMax.y - e && aMax.y > bMin.y + e &&
                   aMin.z < bMax.z - e && aMax.z > bMin.z + e;
        }

        /// <summary>Ray vs axis-aligned box (slab method). Returns entry distance.</summary>
        public static bool RayBox(Vector3 origin, Vector3 dir, Vector3 min, Vector3 max, out float distance)
        {
            distance = 0f;
            float tMin = 0f, tMax = float.MaxValue;
            for (int a = 0; a < 3; a++)
            {
                float o = origin[a], d = dir[a];
                if (Mathf.Abs(d) < 1e-7f)
                {
                    if (o < min[a] || o > max[a]) return false;
                    continue;
                }
                float inv = 1f / d;
                float t1 = (min[a] - o) * inv, t2 = (max[a] - o) * inv;
                if (t1 > t2)
                {
                    float tmp = t1;
                    t1 = t2;
                    t2 = tmp;
                }
                if (t1 > tMin) tMin = t1;
                if (t2 < tMax) tMax = t2;
                if (tMin > tMax) return false;
            }
            distance = tMin;
            return true;
        }
    }
}
