using UnityEngine;

namespace Mine
{
    public enum CameraMode { FirstPerson = 0, ThirdPersonBack = 1, ThirdPersonFront = 2 }

    /// <summary>
    /// Camera perspectives like Minecraft's F5 cycle: first person, third person from behind and third person
    /// from the front. In third person the camera orbits the character's eye, is pulled in when a block is in the
    /// way (five rays around the view axis) and eases back out when the path clears.
    /// Runs after the player, model and held-item updates so it has the final pose of the frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CameraModeController : MonoBehaviour
    {
        const string PrefKey = "mine.cameraMode";

        public float Distance = 4f;
        public CameraMode Mode { get; private set; }
        public bool IsThirdPerson => Mode != CameraMode.FirstPerson;
        public float CurrentDistance => currentDistance;

        PlayerController player;
        PlayerModel model;
        HeldItemView heldItem;
        float currentDistance;

        public string ModeName
        {
            get
            {
                switch (Mode)
                {
                    case CameraMode.ThirdPersonBack: return "3ª pessoa (costas)";
                    case CameraMode.ThirdPersonFront: return "3ª pessoa (frente)";
                    default: return "1ª pessoa";
                }
            }
        }

        public void Init(PlayerController p, PlayerModel m, HeldItemView held)
        {
            player = p;
            model = m;
            heldItem = held;
            Mode = (CameraMode)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 2);
            currentDistance = 0.5f;
        }

        public void Cycle()
        {
            SetMode((CameraMode)(((int)Mode + 1) % 3));
        }

        public void SetMode(CameraMode mode)
        {
            Mode = mode;
            currentDistance = 0.5f; // ease out from the head when entering third person
            PlayerPrefs.SetInt(PrefKey, (int)mode);
        }

        void LateUpdate()
        {
            if (player == null || player.Cam == null) return;
            GameManager gm = GameManager.Instance;
            bool inWorld = gm != null && gm.State != GameState.Menu && gm.State != GameState.Loading && player.PhysicsEnabled;
            bool third = IsThirdPerson && inWorld;
            heldItem.Hidden = third;
            if (!third)
            {
                model.SetVisible(false);
                if (player.CameraOverride)
                {
                    player.CameraOverride = false;
                    player.SnapCameraToEye();
                }
                return;
            }
            player.CameraOverride = true;

            Vector3 eye = player.EyePosition;
            Quaternion look = player.LookRotation;
            Vector3 dir = Mode == CameraMode.ThirdPersonBack ? look * Vector3.back : look * Vector3.forward;

            float target = Distance;
            VoxelWorld world = VoxelWorld.Instance;
            if (world != null) target = ClearDistance(world, eye, dir, look);
            float dt = Time.unscaledDeltaTime;
            currentDistance = target < currentDistance ? target : Mathf.MoveTowards(currentDistance, target, 7f * dt);

            Transform ct = player.Cam.transform;
            ct.position = eye + dir * currentDistance;
            ct.rotation = Mode == CameraMode.ThirdPersonBack ? look : Quaternion.LookRotation(-dir, look * Vector3.up);

            // Too close (pressed against a wall): hide the body instead of filling the screen with the head.
            model.SetVisible(currentDistance > 0.9f);
        }

        float ClearDistance(VoxelWorld world, Vector3 eye, Vector3 dir, Quaternion look)
        {
            const float Spread = 0.15f;
            const float Margin = 0.3f;
            Vector3 right = look * Vector3.right * Spread;
            Vector3 up = look * Vector3.up * Spread;
            float best = Distance;
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = i == 1 ? right : i == 2 ? -right : i == 3 ? up : i == 4 ? -up : Vector3.zero;
                if (VoxelPhysics.RaycastSolid(world, eye + offset, dir, Distance + Margin, out float d))
                    best = Mathf.Min(best, d - Margin);
            }
            return Mathf.Clamp(best, 0.2f, Distance);
        }

        /// <summary>True when the camera itself (not the character) is below a water surface.</summary>
        public bool CameraUnderwater(VoxelWorld world)
        {
            if (world == null || player == null || player.Cam == null) return false;
            if (!IsThirdPerson) return player.HeadInWater;
            Vector3 p = player.Cam.transform.position;
            int x = MathUtil.FastFloor(p.x), y = MathUtil.FastFloor(p.y), z = MathUtil.FastFloor(p.z);
            if (world.GetBlock(x, y, z) != BlockId.Water) return false;
            return world.GetBlock(x, y + 1, z) == BlockId.Water || p.y - y < 0.86f;
        }
    }
}
