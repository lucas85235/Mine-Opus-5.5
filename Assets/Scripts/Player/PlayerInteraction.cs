using UnityEngine;
using UnityEngine.InputSystem;

namespace Mine
{
    /// <summary>
    /// Block targeting (DDA ray from the camera), breaking with left click (timed in survival, instant in creative),
    /// placing with right click, pick-block with middle click and attacking mobs.
    /// </summary>
    public sealed class PlayerInteraction : MonoBehaviour
    {
        public const float Reach = 5f;

        public bool HasTarget { get; private set; }
        public BlockHit Target;
        public float SwingTime { get; private set; } = -1f;

        PlayerController player;
        BlockOutline outline;
        GameManager gm;

        Vector3Int breakingBlock;
        bool breaking;
        float breakProgress;
        float breakCooldown;
        float placeCooldown;
        float hitSoundTimer;

        public void Init(GameManager manager, PlayerController controller, BlockOutline blockOutline)
        {
            gm = manager;
            player = controller;
            outline = blockOutline;
        }

        void Update()
        {
            VoxelWorld world = VoxelWorld.Instance;
            if (world == null || player == null || !player.InputEnabled || !player.PhysicsEnabled)
            {
                HasTarget = false;
                outline?.Hide();
                breaking = false;
                return;
            }

            float dt = Time.deltaTime;
            breakCooldown -= dt;
            placeCooldown -= dt;

            // Rays start at the character's eye, so targeting works the same in first and third person.
            Vector3 origin = player.EyePosition, dir = player.LookDirection;
            HasTarget = VoxelPhysics.Raycast(world, origin, dir, Reach, out Target);
            Mob mob = null;
            float mobDist = float.MaxValue;
            if (MobManager.Instance != null) mob = MobManager.Instance.Raycast(origin, dir, Reach - 1.5f, out mobDist);
            bool mobFirst = mob != null && (!HasTarget || mobDist < Target.Distance);

            if (HasTarget && !mobFirst) outline.Show(Target.Block, Target.Id);
            else outline.Hide();

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // Left button: attack or break.
            if (mouse.leftButton.wasPressedThisFrame && mobFirst)
            {
                mob.Hit(dir);
                Swing();
                StopBreaking();
            }
            else if (mouse.leftButton.isPressed && HasTarget && !mobFirst)
            {
                BreakTick(world, dt);
            }
            else
            {
                StopBreaking();
            }

            // Right button: use / place (repeats while held).
            if (mouse.rightButton.wasPressedThisFrame) placeCooldown = 0f;
            if (mouse.rightButton.isPressed && placeCooldown <= 0f && HasTarget && !mobFirst)
            {
                placeCooldown = UseOrPlace(world) ? 0.22f : 0.1f;
            }

            if (mouse.middleButton.wasPressedThisFrame && HasTarget) PickBlock();
        }

        void Swing()
        {
            SwingTime = Time.time;
        }

        void StopBreaking()
        {
            if (breaking) outline.HideCrack();
            breaking = false;
            breakProgress = 0f;
        }

        void BreakTick(VoxelWorld world, float dt)
        {
            if (!breaking || Target.Block != breakingBlock)
            {
                if (breaking) outline.HideCrack();
                breaking = true;
                breakingBlock = Target.Block;
                breakProgress = 0f;
                hitSoundTimer = 0f;
            }

            BlockDef def = Blocks.Get(Target.Id);
            if (def.Hardness < 0f) return;
            if (breakCooldown > 0f) return;

            if (gm.Creative || def.Hardness <= 0.01f)
            {
                BreakBlock(world, breakingBlock);
                breakCooldown = gm.Creative ? 0.2f : 0.12f;
                breaking = false;
                return;
            }

            breakProgress += dt / def.Hardness;
            hitSoundTimer -= dt;
            if (hitSoundTimer <= 0f)
            {
                hitSoundTimer = 0.24f;
                AudioManager.Instance?.PlayHit(Target.Id, breakingBlock + new Vector3(0.5f, 0.5f, 0.5f));
                Swing();
            }
            outline.SetCrack(breakingBlock, breakProgress);
            if (breakProgress >= 1f)
            {
                BreakBlock(world, breakingBlock);
                outline.HideCrack();
                breaking = false;
                breakProgress = 0f;
                breakCooldown = 0.15f;
            }
        }

        void BreakBlock(VoxelWorld world, Vector3Int p)
        {
            BlockId id = world.GetBlock(p);
            if (id == BlockId.Air || id == BlockId.Water) return;
            BlockDef def = Blocks.Get(id);

            // Water flows into holes next to it.
            bool water = world.GetBlock(p.x, p.y + 1, p.z) == BlockId.Water ||
                         world.GetBlock(p.x + 1, p.y, p.z) == BlockId.Water ||
                         world.GetBlock(p.x - 1, p.y, p.z) == BlockId.Water ||
                         world.GetBlock(p.x, p.y, p.z + 1) == BlockId.Water ||
                         world.GetBlock(p.x, p.y, p.z - 1) == BlockId.Water;
            if (!world.SetBlock(p, water ? BlockId.Water : BlockId.Air)) return;

            Vector3 center = p + new Vector3(0.5f, 0.5f, 0.5f);
            float brightness = world.GetBrightness(center + Target.Normal, gm.DayNight.SkyDarken);
            AudioManager.Instance?.PlayBreak(id, center);
            gm.Particles?.EmitBreak(p, id, Mathf.Max(brightness, 0.25f));
            Swing();

            if (!gm.Creative) Collect(def.Drop);

            // Plants lose their support.
            Vector3Int above = p + Vector3Int.up;
            BlockId top = world.GetBlock(above);
            if (Blocks.Get(top).Shape == BlockShape.Cross)
            {
                world.SetBlock(above, BlockId.Air);
                gm.Particles?.EmitBreak(above, top, Mathf.Max(brightness, 0.25f));
                if (!gm.Creative) Collect(Blocks.Get(top).Drop);
            }
        }

        void Collect(BlockId drop)
        {
            if (drop == BlockId.Air) return;
            int left = gm.Inventory.Add(drop, 1);
            if (left == 0) AudioManager.Instance?.PlayPop();
            else gm.UI?.Toast("Inventário cheio!");
        }

        bool UseOrPlace(VoxelWorld world)
        {
            BlockId targetId = Target.Id;
            if (targetId == BlockId.CraftingTable && !player.Sneaking)
            {
                gm.OpenInventory(true);
                return true;
            }

            ItemStack stack = gm.Inventory.SelectedStack;
            if (stack.IsEmpty) return false;
            BlockId place = stack.Id;
            BlockDef placeDef = Blocks.Get(place);

            Vector3Int pos = Blocks.Get(targetId).Replaceable ? Target.Block : Target.Block + Target.Normal;
            if (pos.y < 0 || pos.y >= Chunk.Height) return false;
            BlockId existing = world.GetBlock(pos);
            if (!Blocks.Get(existing).Replaceable || existing == place) return false;
            if (!world.IsGeneratedAt(pos.x, pos.z)) return false;

            if (placeDef.Shape == BlockShape.Cross)
            {
                BlockId below = world.GetBlock(pos.x, pos.y - 1, pos.z);
                if (below != BlockId.Grass && below != BlockId.Dirt) return false;
                if (existing == BlockId.Water) return false;
            }

            if (placeDef.Solid)
            {
                var bMin = new Vector3(pos.x, pos.y, pos.z);
                Vector3 bMax = bMin + Vector3.one;
                player.GetBounds(out Vector3 pMin, out Vector3 pMax);
                if (VoxelPhysics.BoxesOverlap(pMin, pMax, bMin, bMax)) return false;
                if (MobManager.Instance != null && MobManager.Instance.AnyIntersects(bMin, bMax)) return false;
            }

            if (!world.SetBlock(pos, place)) return false;
            if (!gm.Creative) gm.Inventory.ConsumeSelected(1);
            AudioManager.Instance?.PlayPlace(place, pos + new Vector3(0.5f, 0.5f, 0.5f));
            Swing();
            return true;
        }

        void PickBlock()
        {
            BlockId id = Target.Id;
            if (!Blocks.Get(id).Obtainable) return;
            Inventory inv = gm.Inventory;
            int slot = inv.FindInHotbar(id);
            if (slot >= 0)
            {
                inv.Selected = slot;
                return;
            }
            if (gm.Creative)
            {
                inv.Slots[inv.Selected] = new ItemStack(id, Inventory.MaxStack);
                inv.NotifyChanged();
            }
        }
    }
}
