using UnityEngine;
using UnityEngine.Rendering;

namespace Mine
{
    /// <summary>Little cube debris when a block breaks and smoke puffs when a mob dies.</summary>
    public sealed class BlockParticles : MonoBehaviour
    {
        ParticleSystem debris;
        ParticleSystem smoke;
        ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[1024];
        VoxelWorld world;

        public void Init()
        {
            Mesh cube = MeshUtil.Box(Vector3.zero, Vector3.one, null, "ParticleCube");
            debris = CreateSystem("Debris", cube, 1.6f);
            smoke = CreateSystem("Smoke", cube, -0.03f);
        }

        ParticleSystem CreateSystem(string name, Mesh mesh, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.maxParticles = 1024;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startRotation3D = true;
            main.startLifetime = 0.8f;
            main.startSpeed = 0f;
            main.startSize = 0.1f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.sharedMaterial = MaterialLibrary.Particle;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.enableGPUInstancing = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ps.Play();
            return ps;
        }

        public void EmitBreak(Vector3Int block, BlockId id, float brightness)
        {
            Color32[] palette = TextureFactory.ParticlePalettes[(int)id];
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < 22; i++)
            {
                ep.position = new Vector3(block.x + Random.Range(0.15f, 0.85f), block.y + Random.Range(0.15f, 0.85f), block.z + Random.Range(0.15f, 0.85f));
                ep.velocity = new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(0.8f, 3.4f), Random.Range(-1.6f, 1.6f));
                ep.startSize = Random.Range(0.06f, 0.13f);
                ep.startLifetime = Random.Range(0.45f, 0.9f);
                ep.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                Color c = palette[Random.Range(0, palette.Length)];
                ep.startColor = new Color(c.r * brightness, c.g * brightness, c.b * brightness, 1f);
                debris.Emit(ep, 1);
            }
        }

        public void EmitPoof(Vector3 position, float brightness)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < 16; i++)
            {
                ep.position = position + Random.insideUnitSphere * 0.5f;
                ep.velocity = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.3f, 1.2f), Random.Range(-0.6f, 0.6f));
                ep.startSize = Random.Range(0.12f, 0.25f);
                ep.startLifetime = Random.Range(0.5f, 1.1f);
                ep.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                float g = Random.Range(0.75f, 1f) * brightness;
                ep.startColor = new Color(g, g, g, 1f);
                smoke.Emit(ep, 1);
            }
        }

        void LateUpdate()
        {
            world = VoxelWorld.Instance;
            if (world == null || debris == null) return;
            int count = debris.particleCount;
            if (count == 0) return;
            if (buffer.Length < count) buffer = new ParticleSystem.Particle[count];
            count = debris.GetParticles(buffer, count);
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = buffer[i].position;
                float half = buffer[i].startSize * 0.5f;
                int bx = MathUtil.FastFloor(p.x), by = MathUtil.FastFloor(p.y - half), bz = MathUtil.FastFloor(p.z);
                if (world.IsSolid(bx, by, bz))
                {
                    Vector3 v = buffer[i].velocity;
                    if (v.y < 0f)
                    {
                        p.y = by + 1f + half;
                        buffer[i].position = p;
                        buffer[i].velocity = new Vector3(v.x * 0.5f, 0f, v.z * 0.5f);
                        buffer[i].angularVelocity3D = Vector3.zero;
                        changed = true;
                    }
                }
            }
            if (changed) debris.SetParticles(buffer, count);
        }
    }
}
