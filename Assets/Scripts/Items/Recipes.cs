using System.Text;

namespace Mine
{
    public sealed class Recipe
    {
        public BlockId Result;
        public int ResultCount;
        public ItemStack[] Ingredients;
        /// <summary>Only available when the inventory was opened from a crafting table.</summary>
        public bool NeedsTable;

        public string IngredientText
        {
            get
            {
                var sb = new StringBuilder();
                for (int i = 0; i < Ingredients.Length; i++)
                {
                    if (i > 0) sb.Append(" + ");
                    sb.Append(Ingredients[i].Count).Append("x ").Append(Blocks.NameOf(Ingredients[i].Id));
                }
                return sb.ToString();
            }
        }
    }

    public static class Recipes
    {
        public static readonly Recipe[] All =
        {
            R(BlockId.Planks, 4, false, (BlockId.OakLog, 1)),
            R(BlockId.Planks, 4, false, (BlockId.BirchLog, 1)),
            R(BlockId.Planks, 4, false, (BlockId.SpruceLog, 1)),
            R(BlockId.CraftingTable, 1, false, (BlockId.Planks, 4)),
            R(BlockId.Sandstone, 1, false, (BlockId.Sand, 4)),
            R(BlockId.StoneBricks, 4, true, (BlockId.Cobblestone, 4)),
            R(BlockId.Stone, 1, true, (BlockId.Cobblestone, 1)),
            R(BlockId.Glass, 1, true, (BlockId.Sand, 1)),
            R(BlockId.Bricks, 2, true, (BlockId.Dirt, 2), (BlockId.Gravel, 2)),
            R(BlockId.Lamp, 2, true, (BlockId.Glass, 1), (BlockId.CoalOre, 1), (BlockId.IronOre, 1)),
        };

        static Recipe R(BlockId result, int count, bool table, params (BlockId id, int count)[] ingredients)
        {
            var list = new ItemStack[ingredients.Length];
            for (int i = 0; i < ingredients.Length; i++) list[i] = new ItemStack(ingredients[i].id, ingredients[i].count);
            return new Recipe { Result = result, ResultCount = count, NeedsTable = table, Ingredients = list };
        }

        public static bool CanCraft(Recipe r, Inventory inv, bool atTable)
        {
            if (r.NeedsTable && !atTable) return false;
            for (int i = 0; i < r.Ingredients.Length; i++)
            {
                if (inv.CountOf(r.Ingredients[i].Id) < r.Ingredients[i].Count) return false;
            }
            return true;
        }

        public static bool Craft(Recipe r, Inventory inv, bool atTable)
        {
            if (!CanCraft(r, inv, atTable) || !inv.CanAdd(r.Result, r.ResultCount)) return false;
            for (int i = 0; i < r.Ingredients.Length; i++) inv.Remove(r.Ingredients[i].Id, r.Ingredients[i].Count);
            inv.Add(r.Result, r.ResultCount);
            return true;
        }
    }
}
