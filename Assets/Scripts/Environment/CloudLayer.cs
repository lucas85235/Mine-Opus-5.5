using UnityEngine;
using UnityEngine.Rendering;

namespace Mine
{
    /// <summary>Flat, blocky cloud layer that drifts with the wind and follows the camera.</summary>
    public sealed class CloudLayer : MonoBehaviour
    {
        public float Height = 136f;
        public float Size = 1100f;
        public float WindSpeed = 1.3f;

        static readonly int IdParams = Shader.PropertyToID("_MineCloudParams");
        Transform plane;
        float offset;

        public void Init()
        {
            var mesh = new Mesh { name = "CloudPlane" };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f)
            });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 0.1f, 1f));

            var go = new GameObject("Clouds", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Clouds;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            plane = go.transform;
            plane.localScale = new Vector3(Size, 1f, Size);
        }

        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null || plane == null) return;
            offset += WindSpeed * Time.deltaTime;
            Vector3 p = cam.transform.position;
            plane.position = new Vector3(Mathf.Round(p.x), Height, Mathf.Round(p.z));
            // 64 texels * 12 blocks per texel, fade out at ~420 blocks.
            Shader.SetGlobalVector(IdParams, new Vector4(offset, 0f, 64f * 12f, 420f));
        }
    }
}
