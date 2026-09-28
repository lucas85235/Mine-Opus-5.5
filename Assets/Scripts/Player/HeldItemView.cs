using UnityEngine;
using UnityEngine.Rendering;

namespace Mine
{
    /// <summary>
    /// Draws the selected block in the lower right corner of the view (drawn on top of everything, like a
    /// first-person hand) with a swing animation when the player breaks or places blocks.
    /// Hidden in the third-person camera modes, where the character model holds the block instead.
    /// </summary>
    public sealed class HeldItemView : MonoBehaviour
    {
        /// <summary>Set by the camera controller while a third-person view is active.</summary>
        public bool Hidden;

        PlayerController player;
        PlayerInteraction interaction;
        Inventory inventory;
        Transform pivot;
        MeshRenderer meshRenderer;
        BlockItemMesh item;
        Material opaque;
        Material cutout;
        float switchAnim;

        public void Init(PlayerController p, PlayerInteraction i, Inventory inv)
        {
            player = p;
            interaction = i;
            inventory = inv;

            opaque = new Material(MaterialLibrary.ChunkOpaque) { name = "HeldOpaque" };
            cutout = new Material(MaterialLibrary.ChunkCutout) { name = "HeldCutout" };
            foreach (Material m in new[] { opaque, cutout })
            {
                m.SetFloat("_ZTest", (float)CompareFunction.Always);
                m.SetFloat("_ZWrite", 0f);
                m.renderQueue = 3500;
            }

            var go = new GameObject("HeldItem", typeof(MeshFilter), typeof(MeshRenderer));
            pivot = go.transform;
            pivot.SetParent(p.Cam.transform, false);
            meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            item = new BlockItemMesh("HeldItemMesh");
            go.GetComponent<MeshFilter>().sharedMesh = item.Mesh;
        }

        void LateUpdate()
        {
            if (player == null) return;
            ItemStack stack = inventory.SelectedStack;
            BlockId id = stack.IsEmpty ? BlockId.Air : stack.Id;
            bool visible = id != BlockId.Air && !Hidden && player.PhysicsEnabled && GameManager.Instance != null &&
                           GameManager.Instance.State != GameState.Menu;
            meshRenderer.enabled = visible;
            if (!visible) return;

            Vector3 eye = player.EyePosition;
            int light = VoxelWorld.Instance != null
                ? VoxelWorld.Instance.GetPackedLight(MathUtil.FastFloor(eye.x), MathUtil.FastFloor(eye.y), MathUtil.FastFloor(eye.z))
                : 15 << 4;
            if (id != item.Shown) switchAnim = 1f;
            if (item.Update(id, light)) meshRenderer.sharedMaterial = BlockItemMesh.MaterialFor(id, opaque, cutout);

            switchAnim = Mathf.MoveTowards(switchAnim, 0f, Time.deltaTime * 5f);
            float swing = interaction != null && interaction.SwingTime > 0f ? (Time.time - interaction.SwingTime) / 0.25f : 1f;
            float s = swing < 1f ? Mathf.Sin(swing * Mathf.PI) : 0f;

            Vector3 basePos = new Vector3(0.58f, -0.5f - switchAnim * 0.4f, 1.0f);
            pivot.localPosition = basePos + new Vector3(-0.16f * s, 0.08f * s, 0.1f * s);
            pivot.localRotation = Quaternion.Euler(-35f * s, -10f * s, 0f) * Quaternion.Euler(22f, 40f, 0f);
            float size = Blocks.Get(id).Shape == BlockShape.Cross ? 0.32f : 0.24f;
            pivot.localScale = Vector3.one * size;
        }
    }
}
