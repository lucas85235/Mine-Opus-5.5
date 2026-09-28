using UnityEngine;
using UnityEngine.InputSystem;

namespace Mine
{
    /// <summary>
    /// First-person movement with custom voxel AABB physics: walk, sprint, sneak (no falling off edges), jump,
    /// gravity, swimming and creative flight. Transform position = feet; the camera is a child at eye height.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        public const float HalfWidth = 0.3f;
        public const float Height = 1.8f;
        public const float EyeHeight = 1.62f;
        public const float SneakEyeHeight = 1.3f;

        public Camera Cam { get; private set; }
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw;
        public float Pitch;
        public float Sensitivity = 1f;
        public float BaseFov = 72f;
        public bool ViewBobbing = true;

        public bool InputEnabled;
        public bool PhysicsEnabled;
        /// <summary>Set by CameraModeController while a third-person view positions the camera itself.</summary>
        public bool CameraOverride;
        public bool Flying;
        public bool Grounded { get; private set; }
        public bool InWater { get; private set; }
        public bool HeadInWater { get; private set; }
        public bool Sneaking { get; private set; }
        public bool Sprinting { get; private set; }

        /// <summary>Eye of the character (independent of where the camera is in third-person views).</summary>
        public Vector3 EyePosition => Position + Vector3.up * eyeHeight;
        public Quaternion LookRotation => Quaternion.Euler(Pitch, Yaw, 0f);
        public Vector3 LookDirection => LookRotation * Vector3.forward;
        public float HorizontalSpeed => new Vector2(Velocity.x, Velocity.z).magnitude;

        VoxelWorld world;
        float eyeHeight = EyeHeight;
        float bobPhase;
        float bobAmount;
        float stepDistance;
        float lastSpaceTap = -10f;
        float lastWTap = -10f;
        bool sprintLatch;
        bool hitWallLastFrame;
        float fallStartY;
        bool wasGrounded = true;
        bool wasInWater;
        float jumpCooldown;

        public void Init(Camera cam)
        {
            Cam = cam;
            cam.transform.SetParent(transform, false);
            cam.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            cam.transform.localRotation = Quaternion.identity;
            cam.fieldOfView = BaseFov;
        }

        public void SetWorld(VoxelWorld w)
        {
            world = w;
        }

        public void Teleport(Vector3 feet, float yaw, float pitch)
        {
            Position = feet;
            Velocity = Vector3.zero;
            Yaw = yaw;
            Pitch = pitch;
            fallStartY = feet.y;
            ApplyTransform(0f);
        }

        public void GetBounds(out Vector3 min, out Vector3 max)
        {
            min = new Vector3(Position.x - HalfWidth, Position.y, Position.z - HalfWidth);
            max = new Vector3(Position.x + HalfWidth, Position.y + Height, Position.z + HalfWidth);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (InputEnabled && mouse != null)
            {
                Vector2 md = mouse.delta.ReadValue();
                Yaw += md.x * 0.1f * Sensitivity;
                Pitch -= md.y * 0.1f * Sensitivity;
                Pitch = Mathf.Clamp(Pitch, -89.9f, 89.9f);
                Yaw = Mathf.Repeat(Yaw, 360f);
            }

            if (world == null || !PhysicsEnabled)
            {
                ApplyTransform(dt);
                return;
            }

            Vector2 move = Vector2.zero;
            bool jump = false;
            Sneaking = false;
            bool sprintKey = false;
            if (InputEnabled && kb != null)
            {
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                jump = kb.spaceKey.isPressed;
                Sneaking = kb.leftShiftKey.isPressed;
                sprintKey = kb.leftCtrlKey.isPressed;

                if (kb.wKey.wasPressedThisFrame)
                {
                    if (Time.time - lastWTap < 0.3f) sprintLatch = true;
                    lastWTap = Time.time;
                }
                if (kb.spaceKey.wasPressedThisFrame)
                {
                    if (Time.time - lastSpaceTap < 0.3f && GameManager.Instance != null && GameManager.Instance.Creative)
                    {
                        Flying = !Flying;
                        Velocity.y = 0f;
                        lastSpaceTap = -10f;
                    }
                    else
                    {
                        lastSpaceTap = Time.time;
                    }
                }
            }
            if (move.y <= 0f) sprintLatch = false;
            Sprinting = (sprintKey || sprintLatch) && move.y > 0f && !Sneaking;

            UpdateWaterState();

            float yawRad = Yaw * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            var right = new Vector3(forward.z, 0f, -forward.x);
            Vector3 wish = forward * move.y + right * move.x;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            if (Flying) FlyMove(wish, jump, dt);
            else WalkMove(wish, jump, dt);

            VoxelPhysics.Move(world, ref Position, ref Velocity, HalfWidth, Height, dt,
                Sneaking && !Flying && Grounded && !InWater, out bool grounded, out bool hitWall, out _);
            hitWallLastFrame = hitWall;

            if (Flying && grounded && Velocity.y <= 0f && Sneaking) Flying = false;
            Grounded = grounded;

            HandleLandingAndSteps(dt);

            if (Position.y < -40f)
            {
                // Fell out of the world: put the player back on the surface.
                int top = world.GetTopBlockY(MathUtil.FastFloor(Position.x), MathUtil.FastFloor(Position.z), true);
                Position.y = Mathf.Max(top + 1, TerrainGenerator.SeaLevel + 1);
                Velocity = Vector3.zero;
            }

            ApplyTransform(dt);
        }

        void UpdateWaterState()
        {
            wasInWater = InWater;
            int x = MathUtil.FastFloor(Position.x), z = MathUtil.FastFloor(Position.z);
            BlockId feet = world.GetBlock(x, MathUtil.FastFloor(Position.y + 0.1f), z);
            BlockId body = world.GetBlock(x, MathUtil.FastFloor(Position.y + 0.9f), z);
            InWater = feet == BlockId.Water || body == BlockId.Water;
            Vector3 eye = Position + Vector3.up * eyeHeight;
            int ey = MathUtil.FastFloor(eye.y);
            HeadInWater = world.GetBlock(x, ey, z) == BlockId.Water &&
                          (world.GetBlock(x, ey + 1, z) == BlockId.Water || eye.y - ey < 0.86f);

            if (InWater && !wasInWater && Velocity.y < -3f)
                AudioManager.Instance?.PlayAt(AudioManager.Instance.Library.Splash, Position, 0.8f, Random.Range(0.9f, 1.1f));
        }

        void WalkMove(Vector3 wish, bool jump, float dt)
        {
            float speed = Sneaking ? 1.4f : (Sprinting ? 5.8f : 4.4f);
            if (InWater) speed *= 0.5f;
            Vector3 target = wish * speed;
            float accel = Grounded ? 45f : (InWater ? 14f : 11f);
            var hv = new Vector3(Velocity.x, 0f, Velocity.z);
            hv = Vector3.MoveTowards(hv, target, accel * dt);
            Velocity.x = hv.x;
            Velocity.z = hv.z;

            jumpCooldown -= dt;
            if (InWater)
            {
                Velocity.y -= 10f * dt;
                Velocity.y *= 1f - Mathf.Min(1f, 2.5f * dt);
                if (jump)
                {
                    Velocity.y = Mathf.MoveTowards(Velocity.y, 3.4f, 30f * dt);
                    if (hitWallLastFrame) Velocity.y = Mathf.Max(Velocity.y, 5.2f);
                }
                Velocity.y = Mathf.Clamp(Velocity.y, -5f, 6f);
            }
            else
            {
                Velocity.y -= 28f * dt;
                if (Velocity.y < -60f) Velocity.y = -60f;
                if (jump && Grounded && jumpCooldown <= 0f)
                {
                    Velocity.y = 8.6f;
                    jumpCooldown = 0.12f;
                    if (Sprinting)
                    {
                        Velocity.x += wish.x * 1.2f;
                        Velocity.z += wish.z * 1.2f;
                    }
                }
            }
        }

        void FlyMove(Vector3 wish, bool up, float dt)
        {
            float speed = Sprinting ? 22f : 11f;
            Vector3 target = wish * speed;
            float vertical = (up ? 1f : 0f) - (Sneaking ? 1f : 0f);
            target.y = vertical * 8f;
            Velocity = Vector3.MoveTowards(Velocity, target, 60f * dt);
        }

        void HandleLandingAndSteps(float dt)
        {
            if (Flying || InWater)
            {
                fallStartY = Position.y;
            }
            else if (!Grounded)
            {
                if (wasGrounded) fallStartY = Position.y;
                fallStartY = Mathf.Max(fallStartY, Position.y);
            }
            else if (!wasGrounded)
            {
                float fall = fallStartY - Position.y;
                if (fall > 1.2f)
                {
                    BlockId under = BlockUnderFeet();
                    AudioManager.Instance?.PlayStep(under, Position, Mathf.Clamp01(0.4f + fall * 0.1f));
                }
            }
            wasGrounded = Grounded;

            float hs = new Vector2(Velocity.x, Velocity.z).magnitude;
            if (Grounded && !Flying && hs > 0.3f)
            {
                stepDistance += hs * dt;
                float stride = Sprinting ? 2.1f : 1.7f;
                if (stepDistance > stride && !Sneaking)
                {
                    stepDistance = 0f;
                    BlockId under = BlockUnderFeet();
                    if (under != BlockId.Air) AudioManager.Instance?.PlayStep(under, Position, 0.35f);
                }
            }
        }

        BlockId BlockUnderFeet()
        {
            int x = MathUtil.FastFloor(Position.x), z = MathUtil.FastFloor(Position.z);
            int y = MathUtil.FastFloor(Position.y - 0.05f);
            BlockId b = world.GetBlock(x, y, z);
            if (b == BlockId.Air) b = world.GetBlock(MathUtil.FastFloor(Position.x + HalfWidth * 0.9f), y, z);
            if (b == BlockId.Air) b = world.GetBlock(MathUtil.FastFloor(Position.x - HalfWidth * 0.9f), y, z);
            if (b == BlockId.Air) b = world.GetBlock(x, y, MathUtil.FastFloor(Position.z + HalfWidth * 0.9f));
            if (b == BlockId.Air) b = world.GetBlock(x, y, MathUtil.FastFloor(Position.z - HalfWidth * 0.9f));
            return b;
        }

        void ApplyTransform(float dt)
        {
            float targetEye = Sneaking && !Flying ? SneakEyeHeight : EyeHeight;
            eyeHeight = Mathf.MoveTowards(eyeHeight, targetEye, 3f * Mathf.Max(dt, 0.001f));

            float hs = new Vector2(Velocity.x, Velocity.z).magnitude;
            bool bob = ViewBobbing && Grounded && !Flying && hs > 0.5f && PhysicsEnabled;
            bobAmount = Mathf.MoveTowards(bobAmount, bob ? Mathf.Clamp01(hs / 4.4f) : 0f, 4f * dt);
            if (bob) bobPhase += hs * dt * 1.9f;

            transform.position = Position;
            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
            if (Cam == null) return;

            float fovTarget = BaseFov * (Sprinting && hs > 1f ? 1.12f : 1f) * (Flying && Sprinting ? 1.08f : 1f);
            Cam.fieldOfView = Mathf.MoveTowards(Cam.fieldOfView, fovTarget, 60f * Mathf.Max(dt, 0.001f));

            if (!CameraOverride) SnapCameraToEye();
        }

        /// <summary>Puts the camera at the eye (first-person pose, with view bobbing).</summary>
        public void SnapCameraToEye()
        {
            if (Cam == null) return;
            var bobOffset = new Vector3(Mathf.Cos(bobPhase) * 0.035f, -Mathf.Abs(Mathf.Sin(bobPhase)) * 0.055f, 0f) * bobAmount;
            Cam.transform.localPosition = new Vector3(0f, eyeHeight, 0f) + bobOffset;
            Cam.transform.localRotation = Quaternion.Euler(Pitch, 0f, Mathf.Cos(bobPhase) * 0.6f * bobAmount);
        }
    }
}
