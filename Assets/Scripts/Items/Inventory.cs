using System;

namespace Mine
{
    [Serializable]
    public struct ItemStack
    {
        public BlockId Id;
        public int Count;

        public ItemStack(BlockId id, int count)
        {
            Id = id;
            Count = count;
        }

        public bool IsEmpty => Count <= 0 || Id == BlockId.Air;
        public static ItemStack Empty => new ItemStack(BlockId.Air, 0);
    }

    /// <summary>36 slots: 0-8 are the hotbar, 9-35 the main inventory.</summary>
    public sealed class Inventory
    {
        public const int Size = 36;
        public const int HotbarSize = 9;
        public const int MaxStack = 64;

        public readonly ItemStack[] Slots = new ItemStack[Size];
        int selected;

        public event Action Changed;

        public int Selected
        {
            get => selected;
            set
            {
                int v = ((value % HotbarSize) + HotbarSize) % HotbarSize;
                if (v == selected) return;
                selected = v;
                Changed?.Invoke();
            }
        }

        public ItemStack SelectedStack => Slots[selected];

        public void NotifyChanged()
        {
            Changed?.Invoke();
        }

        public void Clear()
        {
            for (int i = 0; i < Size; i++) Slots[i] = ItemStack.Empty;
            selected = 0;
            Changed?.Invoke();
        }

        /// <summary>Adds items, merging into existing stacks first (hotbar before main). Returns leftover count.</summary>
        public int Add(BlockId id, int count)
        {
            if (id == BlockId.Air || count <= 0) return 0;
            for (int i = 0; i < Size && count > 0; i++)
            {
                if (Slots[i].Id == id && Slots[i].Count > 0 && Slots[i].Count < MaxStack)
                {
                    int move = Math.Min(MaxStack - Slots[i].Count, count);
                    Slots[i].Count += move;
                    count -= move;
                }
            }
            for (int i = 0; i < Size && count > 0; i++)
            {
                if (Slots[i].IsEmpty)
                {
                    int move = Math.Min(MaxStack, count);
                    Slots[i] = new ItemStack(id, move);
                    count -= move;
                }
            }
            Changed?.Invoke();
            return count;
        }

        public bool CanAdd(BlockId id, int count)
        {
            int space = 0;
            for (int i = 0; i < Size; i++)
            {
                if (Slots[i].IsEmpty) space += MaxStack;
                else if (Slots[i].Id == id) space += MaxStack - Slots[i].Count;
                if (space >= count) return true;
            }
            return space >= count;
        }

        public int CountOf(BlockId id)
        {
            int total = 0;
            for (int i = 0; i < Size; i++)
            {
                if (Slots[i].Id == id && Slots[i].Count > 0) total += Slots[i].Count;
            }
            return total;
        }

        public bool Remove(BlockId id, int count)
        {
            if (CountOf(id) < count) return false;
            for (int i = Size - 1; i >= 0 && count > 0; i--)
            {
                if (Slots[i].Id != id || Slots[i].Count <= 0) continue;
                int take = Math.Min(Slots[i].Count, count);
                Slots[i].Count -= take;
                count -= take;
                if (Slots[i].Count <= 0) Slots[i] = ItemStack.Empty;
            }
            Changed?.Invoke();
            return true;
        }

        public void ConsumeSelected(int count)
        {
            ItemStack s = Slots[selected];
            if (s.IsEmpty) return;
            s.Count -= count;
            Slots[selected] = s.Count > 0 ? s : ItemStack.Empty;
            Changed?.Invoke();
        }

        /// <summary>Finds a hotbar slot holding the block, or -1.</summary>
        public int FindInHotbar(BlockId id)
        {
            for (int i = 0; i < HotbarSize; i++)
            {
                if (Slots[i].Id == id && Slots[i].Count > 0) return i;
            }
            return -1;
        }
    }
}
