using UnityEngine;
using UnityEngine.Rendering;

namespace Mine
{
    /// <summary>Black wireframe around the targeted block plus the crack overlay while breaking it.</summary>
    public sealed class BlockOutline : MonoBehaviour
    {
        Transform outline;
        Transform crack;
        MeshRenderer crackRenderer;
        Material crackMaterial;
        int crackStage = -1;

        public void Init()
        {
            outline = CreatePart("BlockOutline", MeshUtil.WireCube(0.014f), MaterialLibrary.Outline, out _);

            crackMaterial = new Material(MaterialLibrary.Crack);
            crack = CreatePart("BreakOverlay", MeshUtil.Box(new Vector3(0.5f, 0.5f, 0.5f), Vector3.one, null, "CrackCube"),
                crackMaterial, out crackRenderer);

            outline.gameObject.SetActive(false);
            crack.gameObject.SetActive(false);
        }

        Transform CreatePart(string name, Mesh mesh, Material material, out MeshRenderer renderer)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go.transform;
        }

        public void Show(Vector3Int block, BlockId id)
        {
            outline.gameObject.SetActive(true);
            const float grow = 0.004f;
            if (Blocks.Get(id).Shape == BlockShape.Cross)
            {
                outline.position = new Vector3(block.x + 0.15f, block.y, block.z + 0.15f);
                outline.localScale = new Vector3(0.7f, 0.8f, 0.7f);
            }
            else
            {
                outline.position = new Vector3(block.x - grow, block.y - grow, block.z - grow);
                outline.localScale = Vector3.one * (1f + grow * 2f);
            }
        }

        public void Hide()
        {
            if (outline != null) outline.gameObject.SetActive(false);
            HideCrack();
        }

        public void SetCrack(Vector3Int block, float progress)
        {
            int stage = Mathf.Clamp(Mathf.FloorToInt(progress * 10f), 0, 9);
            crack.gameObject.SetActive(true);
            const float grow = 0.002f;
            crack.position = new Vector3(block.x - grow, block.y - grow, block.z - grow);
            crack.localScale = Vector3.one * (1f + grow * 2f);
            if (stage != crackStage)
            {
                crackStage = stage;
                crackMaterial.SetFloat("_Slice", (float)TexId.Crack0 + stage);
            }
        }

        public void HideCrack()
        {
            if (crack != null) crack.gameObject.SetActive(false);
            crackStage = -1;
        }
    }
}
