using UnityEngine;

namespace Mine
{
    /// <summary>Simple passive animal: wanders, hops up single blocks, avoids cliffs/water, flees when hit.</summary>
    public sealed class Mob : MonoBehaviour
    {
        enum State { Idle, Wander, Panic, Dying }

        public MobKind Kind { get; private set; }
        public Vector3 Position;
        public float HalfWidth { get; private set; }
        public float Height { get; private set; }
        public bool Dead => state == State.Dying;

        static readonly int IdBrightness = Shader.PropertyToID("_Brightness");
        static readonly int IdTint = Shader.PropertyToID("_Tint");

        MobFactory.Spec spec;
        Transform head;
        Transform[] legs;
        Renderer[] renderers;
        MaterialPropertyBlock mpb;

        Vector3 velocity;
        float yaw;
        float targetYaw;
        State state = State.Idle;
        float stateTimer;
        float walkPhase;
        float hurtTimer;
        float soundTimer;
        float deathTimer;
        float headYaw;
        float headPitch;
        int health;
        bool grounded;

        public void Configure(MobKind kind, MobFactory.Spec s, Transform headPart, Transform[] legParts, Renderer[] parts)
        {
            Kind = kind;
            spec = s;
            HalfWidth = s.HalfWidth;
            Height = s.Height;
            health = s.Health;
            head = headPart;
            legs = legParts;
            renderers = parts;
            mpb = new MaterialPropertyBlock();
            yaw = targetYaw = Random.Range(0f, 360f);
            stateTimer = Random.Range(1f, 4f);
            soundTimer = Random.Range(5f, 20f);
        }

        public void Place(Vector3 feet)
        {
            Position = feet;
            transform.SetPositionAndRotation(Position, Quaternion.Euler(0f, yaw, 0f));
        }

        public void GetBounds(out Vector3 min, out Vector3 max)
        {
            min = new Vector3(Position.x - HalfWidth, Position.y, Position.z - HalfWidth);
            max = new Vector3(Position.x + HalfWidth, Position.y + Height, Position.z + HalfWidth);
        }

        void Update()
        {
            VoxelWorld world = VoxelWorld.Instance;
            GameManager gm = GameManager.Instance;
            if (world == null || gm == null) return;
            if (!world.IsMeshedAt(MathUtil.FastFloor(Position.x), MathUtil.FastFloor(Position.z))) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (gm.State != GameState.Playing && gm.State != GameState.Inventory) dt = 0f;

            if (state == State.Dying)
            {
                UpdateDeath(dt, world, gm);
                return;
            }

            Think(dt, world);
            Move(dt, world);
            Animate(dt, gm);
            UpdateLook(world, gm);
            UpdateSounds(dt, gm);
        }

        void Think(float dt, VoxelWorld world)
        {
            stateTimer -= dt;
            switch (state)
            {
                case State.Idle:
                    if (stateTimer <= 0f)
                    {
                        state = State.Wander;
                        stateTimer = Random.Range(2f, 5f);
                        targetYaw = yaw + Random.Range(-120f, 120f);
                    }
                    break;
                case State.Wander:
                    if (stateTimer <= 0f)
                    {
                        state = State.Idle;
                        stateTimer = Random.Range(2f, 6f);
                    }
                    else if (DangerAhead(world))
                    {
                        targetYaw = yaw + 180f + Random.Range(-40f, 40f);
                    }
                    break;
                case State.Panic:
                    if (stateTimer <= 0f)
                    {
                        state = State.Idle;
                        stateTimer = Random.Range(1f, 3f);
                    }
                    else if (Random.value < dt * 0.8f)
                    {
                        targetYaw += Random.Range(-70f, 70f);
                    }
                    break;
            }
        }

        Vector3 Forward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        bool DangerAhead(VoxelWorld world)
        {
            Vector3 ahead = Position + Forward * (HalfWidth + 0.6f);
            int x = MathUtil.FastFloor(ahead.x), z = MathUtil.FastFloor(ahead.z), y = MathUtil.FastFloor(Position.y + 0.01f);
            if (world.GetBlock(x, y, z) == BlockId.Water || world.GetBlock(x, y - 1, z) == BlockId.Water) return true;
            bool drop = !world.IsSolid(x, y - 1, z) && !world.IsSolid(x, y - 2, z) && !world.IsSolid(x, y - 3, z);
            return drop;
        }

        void Move(float dt, VoxelWorld world)
        {
            if (dt <= 0f) return;
            float speed = state == State.Wander ? spec.WalkSpeed : (state == State.Panic ? spec.WalkSpeed * 2.8f : 0f);
            Vector3 target = Forward * speed;
            float accel = grounded ? 10f : 3f;
            velocity.x = Mathf.MoveTowards(velocity.x, target.x, accel * dt);
            velocity.z = Mathf.MoveTowards(velocity.z, target.z, accel * dt);

            bool inWater = world.GetBlock(MathUtil.FastFloor(Position.x), MathUtil.FastFloor(Position.y + Height * 0.4f), MathUtil.FastFloor(Position.z)) == BlockId.Water;
            if (inWater)
            {
                velocity.y = Mathf.MoveTowards(velocity.y, 2.2f, 14f * dt);
            }
            else
            {
                velocity.y -= 28f * dt;
                if (velocity.y < -50f) velocity.y = -50f;
            }

            VoxelPhysics.Move(world, ref Position, ref velocity, HalfWidth, Height, dt, false, out grounded, out bool hitWall, out _);

            if (hitWall && speed > 0f)
            {
                if ((grounded || inWater) && CanStepUp(world)) velocity.y = 7.8f;
                else if (grounded) targetYaw = yaw + Random.Range(90f, 270f);
            }

            yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, (state == State.Panic ? 400f : 200f) * dt);
            if (Position.y < -30f) Destroy(gameObject);
            transform.SetPositionAndRotation(Position, Quaternion.Euler(0f, yaw, 0f));
        }

        bool CanStepUp(VoxelWorld world)
        {
            Vector3 ahead = Position + Forward * (HalfWidth + 0.4f);
            int x = MathUtil.FastFloor(ahead.x), z = MathUtil.FastFloor(ahead.z), y = MathUtil.FastFloor(Position.y + 0.01f);
            if (!world.IsSolid(x, y, z)) return false;
            int headRoom = Mathf.CeilToInt(Height) + 1;
            for (int k = 1; k <= headRoom; k++)
            {
                if (world.IsSolid(x, y + k, z)) return false;
            }
            return true;
        }

        void Animate(float dt, GameManager gm)
        {
            float hs = new Vector2(velocity.x, velocity.z).magnitude;
            walkPhase += hs * dt * 7f;
            float amount = Mathf.Clamp01(hs / spec.WalkSpeed);
            float swing = Mathf.Sin(walkPhase) * 38f * amount;
            if (legs != null)
            {
                legs[0].localRotation = Quaternion.Euler(swing, 0f, 0f);
                legs[3].localRotation = Quaternion.Euler(swing, 0f, 0f);
                legs[1].localRotation = Quaternion.Euler(-swing, 0f, 0f);
                legs[2].localRotation = Quaternion.Euler(-swing, 0f, 0f);
            }

            hurtTimer -= dt;
            float brightness = VoxelWorld.Instance.GetBrightness(Position + Vector3.up * (Height * 0.7f), gm.DayNight.SkyDarken);
            mpb.SetFloat(IdBrightness, brightness);
            mpb.SetColor(IdTint, hurtTimer > 0f ? new Color(0.9f, 0.05f, 0.05f, 0.5f) : new Color(0f, 0f, 0f, 0f));
            for (int i = 0; i < renderers.Length; i++) renderers[i].SetPropertyBlock(mpb);
        }

        void UpdateLook(VoxelWorld world, GameManager gm)
        {
            if (head == null) return;
            float wantYaw = 0f, wantPitch = 0f;
            if (gm.Player != null && state != State.Panic)
            {
                Vector3 eye = gm.Player.EyePosition;
                Vector3 to = eye - head.position;
                if (to.sqrMagnitude < 64f)
                {
                    Vector3 local = transform.InverseTransformDirection(to);
                    wantYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -60f, 60f);
                    wantPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -30f, 30f);
                }
            }
            if (state == State.Idle && wantYaw == 0f) wantPitch = 15f; // grazing
            headYaw = Mathf.MoveTowards(headYaw, wantYaw, 150f * Time.deltaTime);
            headPitch = Mathf.MoveTowards(headPitch, wantPitch, 100f * Time.deltaTime);
            head.localRotation = Quaternion.Euler(headPitch, headYaw, 0f);
        }

        void UpdateSounds(float dt, GameManager gm)
        {
            soundTimer -= dt;
            if (soundTimer > 0f) return;
            soundTimer = Random.Range(8f, 22f);
            if (gm.Player == null || (gm.Player.transform.position - Position).sqrMagnitude > 24f * 24f) return;
            AudioManager.Instance?.PlayAt(AudioManager.Instance.Library.MobIdle(Kind), Position + Vector3.up, 0.8f, Random.Range(0.9f, 1.1f));
        }

        public void Hit(Vector3 direction, int damage = 1)
        {
            if (state == State.Dying) return;
            health -= damage;
            hurtTimer = 0.35f;
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 1e-4f) flat = Forward;
            flat.Normalize();
            velocity += flat * 5f;
            velocity.y = 5.5f;
            AudioManager.Instance?.PlayAt(AudioManager.Instance.Library.MobHurt(Kind), Position + Vector3.up, 1f, Random.Range(0.9f, 1.1f));

            if (health <= 0)
            {
                state = State.Dying;
                deathTimer = 0f;
                return;
            }
            state = State.Panic;
            stateTimer = 5f;
            targetYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        }

        void UpdateDeath(float dt, VoxelWorld world, GameManager gm)
        {
            deathTimer += dt;
            float k = Mathf.Clamp01(deathTimer / 0.4f);
            transform.rotation = Quaternion.Euler(0f, yaw, 90f * k);
            mpb.SetColor(IdTint, new Color(0.9f, 0.05f, 0.05f, 0.5f));
            for (int i = 0; i < renderers.Length; i++) renderers[i].SetPropertyBlock(mpb);

            if (deathTimer < 0.9f) return;
            Vector3 center = Position + Vector3.up * (Height * 0.5f);
            float brightness = world.GetBrightness(center, gm.DayNight.SkyDarken);
            gm.Particles?.EmitPoof(center, brightness);
            AudioManager.Instance?.PlayAt(AudioManager.Instance.Library.Poof, center, 0.7f, 1f);
            if (Kind == MobKind.Sheep && !gm.Creative)
            {
                int n = Random.Range(1, 3);
                gm.Inventory.Add(BlockId.Wool, n);
                gm.UI?.Toast("+" + n + " " + Blocks.NameOf(BlockId.Wool));
            }
            Destroy(gameObject);
        }
    }
}
