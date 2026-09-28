using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mine
{
    /// <summary>
    /// Blocky player character (head, body, backpack, arms, legs) built from boxes with the procedural
    /// <see cref="PlayerSkin"/>. Only visible in third-person views. Animation is driven by the controller:
    /// walk cycle, body turning after the head, head looking up/down, sneaking lean, arm swing when
    /// breaking/placing and the selected block held in the right hand.
    /// Model units: 1 pixel = 1/16 block, 32 px tall, scaled by 0.9 to match the 1.8 block collision box.
    /// </summary>
    public sealed class PlayerModel : MonoBehaviour
    {
        const float Px = 1f / 16f;
        const float ModelScale = 0.9f;

        static readonly int IdBrightness = Shader.PropertyToID("_Brightness");
        static readonly int IdTint = Shader.PropertyToID("_Tint");

        PlayerController player;
        PlayerInteraction interaction;
        Inventory inventory;

        Transform root;
        Transform upper;
        Transform head;
        Transform armRight;
        Transform armLeft;
        Transform legRight;
        Transform legLeft;
        Transform heldPivot;
        readonly List<Renderer> renderers = new List<Renderer>();
        MaterialPropertyBlock mpb;
        MeshRenderer heldRenderer;
        BlockItemMesh heldItem;

        bool visible = true;
        float bodyYaw;
        float walkPhase;
        float walkAmount;
        float sneakAmount;

        public bool Visible => visible;

        public void Init(PlayerController p, PlayerInteraction i, Inventory inv)
        {
            player = p;
            interaction = i;
            inventory = inv;
            mpb = new MaterialPropertyBlock();

            PlayerSkin skin = PlayerSkin.Create();
            var mat = new Material(MaterialLibrary.Entity) { name = "PlayerSkin (Runtime)" };
            mat.SetTexture("_MainTex", skin.Texture);

            root = new GameObject("PlayerModel").transform;
            root.SetParent(p.transform, false);
            root.localScale = Vector3.one * ModelScale;

            legRight = Part(root, "LegRight", new Vector3(2f, 12f, 0f), new Vector3(0f, -6f, 0f), new Vector3(4f, 12f, 4f), skin.LegRight, mat);
            legLeft = Part(root, "LegLeft", new Vector3(-2f, 12f, 0f), new Vector3(0f, -6f, 0f), new Vector3(4f, 12f, 4f), skin.LegLeft, mat);

            upper = new GameObject("Upper").transform;
            upper.SetParent(root, false);
            upper.localPosition = new Vector3(0f, 12f, 0f) * Px;

            Part(upper, "Body", Vector3.zero, new Vector3(0f, 6f, 0f), new Vector3(8f, 12f, 4f), skin.Body, mat);
            Part(upper, "Backpack", Vector3.zero, new Vector3(0f, 6.5f, -3.5f), new Vector3(6f, 8f, 3f), skin.Backpack, mat);
            armRight = Part(upper, "ArmRight", new Vector3(6f, 10f, 0f), new Vector3(0f, -4f, 0f), new Vector3(4f, 12f, 4f), skin.ArmRight, mat);
            armLeft = Part(upper, "ArmLeft", new Vector3(-6f, 10f, 0f), new Vector3(0f, -4f, 0f), new Vector3(4f, 12f, 4f), skin.ArmLeft, mat);
            head = Part(upper, "Head", new Vector3(0f, 12f, 0f), new Vector3(0f, 4f, 0f), new Vector3(8f, 8f, 8f), skin.Head, mat);

            var held = new GameObject("HeldBlock", typeof(MeshFilter), typeof(MeshRenderer));
            heldPivot = held.transform;
            heldPivot.SetParent(armRight, false);
            heldPivot.localPosition = new Vector3(0f, -10.5f, 2.5f) * Px;
            heldPivot.localRotation = Quaternion.Euler(-10f, 45f, 0f);
            heldItem = new BlockItemMesh("HeldBlock3P");
            held.GetComponent<MeshFilter>().sharedMesh = heldItem.Mesh;
            heldRenderer = held.GetComponent<MeshRenderer>();
            ConfigureRenderer(heldRenderer);

            bodyYaw = p.Yaw;
            SetVisible(false);
        }

        static void ConfigureRenderer(Renderer r)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        Transform Part(Transform parent, string name, Vector3 pivotPx, Vector3 centerPx, Vector3 sizePx, Rect[] uvs, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pivotPx * Px;
            go.GetComponent<MeshFilter>().sharedMesh = MeshUtil.Box(centerPx * Px, sizePx * Px, uvs, "Player" + name);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            ConfigureRenderer(mr);
            renderers.Add(mr);
            return go.transform;
        }

        public void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            for (int i = 0; i < renderers.Count; i++) renderers[i].enabled = value;
            if (!value) heldRenderer.enabled = false;
            if (value) bodyYaw = player.Yaw;
        }

        void LateUpdate()
        {
            if (!visible || player == null) return;
            float dt = Time.deltaTime;

            // Body follows the head: immediately while walking, lazily (50 degree dead zone) while standing.
            float speed = player.HorizontalSpeed;
            bool moving = speed > 0.3f;
            if (moving)
            {
                bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, player.Yaw, 420f * dt);
            }
            else
            {
                float d = Mathf.DeltaAngle(bodyYaw, player.Yaw);
                if (Mathf.Abs(d) > 50f) bodyYaw += d - Mathf.Sign(d) * 50f;
            }

            sneakAmount = Mathf.MoveTowards(sneakAmount, player.Sneaking && !player.Flying ? 1f : 0f, 6f * dt);
            root.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
            root.localPosition = new Vector3(0f, -0.12f * sneakAmount, 0f);

            // Walk cycle: legs swing, arms swing the other way.
            bool walking = moving && !player.Flying && (player.Grounded || player.InWater);
            walkAmount = Mathf.MoveTowards(walkAmount, walking ? Mathf.Clamp01(speed / 4.4f) : 0f, 5f * dt);
            walkPhase += speed * dt * 2.6f;
            float swing = Mathf.Sin(walkPhase) * 42f * walkAmount;
            legRight.localRotation = Quaternion.Euler(swing, 0f, 0f);
            legLeft.localRotation = Quaternion.Euler(-swing, 0f, 0f);

            float breathe = Mathf.Sin(Time.time * 1.6f) * 2.5f;
            bool holding = !inventory.SelectedStack.IsEmpty;
            float rightX = -swing * 0.8f - (holding ? 12f : 0f);
            float rightY = 0f;
            float swingT = interaction != null && interaction.SwingTime > 0f ? (Time.time - interaction.SwingTime) / 0.3f : 1f;
            if (swingT >= 0f && swingT < 1f)
            {
                float s = Mathf.Sin(swingT * Mathf.PI);
                rightX -= 75f * s;
                rightY = -25f * s;
            }
            armRight.localRotation = Quaternion.Euler(rightX, rightY, 4f + breathe);
            armLeft.localRotation = Quaternion.Euler(swing * 0.8f, 0f, -4f - breathe);

            // Sneaking leans the upper body forward; the head compensates so it keeps looking where the camera looks.
            float lean = 26f * sneakAmount;
            upper.localRotation = Quaternion.Euler(lean, 0f, 0f);
            float headYaw = Mathf.Clamp(Mathf.DeltaAngle(bodyYaw, player.Yaw), -75f, 75f);
            float headPitch = Mathf.Clamp(player.Pitch, -80f, 80f) - lean;
            head.localRotation = Quaternion.Euler(headPitch, headYaw, 0f);

            UpdateHeldBlock();
            UpdateLighting();
        }

        int LightAtChest()
        {
            VoxelWorld w = VoxelWorld.Instance;
            if (w == null) return 15 << 4;
            Vector3 p = player.Position + Vector3.up * 1.2f;
            return w.GetPackedLight(MathUtil.FastFloor(p.x), MathUtil.FastFloor(p.y), MathUtil.FastFloor(p.z));
        }

        void UpdateHeldBlock()
        {
            ItemStack stack = inventory.SelectedStack;
            if (stack.IsEmpty)
            {
                heldRenderer.enabled = false;
                return;
            }
            if (heldItem.Update(stack.Id, LightAtChest()))
            {
                heldRenderer.sharedMaterial = BlockItemMesh.MaterialFor(stack.Id, MaterialLibrary.ChunkOpaque, MaterialLibrary.ChunkCutout);
                bool plant = Blocks.Get(stack.Id).Shape == BlockShape.Cross;
                heldPivot.localScale = Vector3.one * (plant ? 0.42f : 0.3f);
            }
            heldRenderer.enabled = true;
        }

        void UpdateLighting()
        {
            VoxelWorld w = VoxelWorld.Instance;
            GameManager gm = GameManager.Instance;
            if (w == null || gm == null) return;
            float brightness = w.GetBrightness(player.Position + Vector3.up * 1.2f, gm.DayNight.SkyDarken);
            mpb.SetFloat(IdBrightness, brightness);
            mpb.SetColor(IdTint, new Color(0f, 0f, 0f, 0f));
            for (int i = 0; i < renderers.Count; i++) renderers[i].SetPropertyBlock(mpb);
        }
    }
}
