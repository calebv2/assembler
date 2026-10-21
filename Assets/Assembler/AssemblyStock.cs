using System;

namespace Builder.Assembler
{
    public enum Ingredient { Shaft, Tip, Feather }

    // Inventory is independent of presentation so physical/VR input can reuse it.
    [Serializable]
    public sealed class AssemblyStock
    {
        public const int Capacity = 30;
        readonly int[] counts = new int[3];
        public int this[Ingredient ingredient] => counts[(int)ingredient];
        public bool Ready => counts[0] > 0 && counts[1] > 0 && counts[2] > 0;
        public bool TryLoad(Ingredient ingredient, int quantity)
        {
            int index = (int)ingredient;
            if (index < 0 || index >= 3 || quantity <= 0 || quantity > Capacity - counts[index]) return false;
            counts[index] += quantity;
            return true;
        }
        public bool TryConsumeRecipe()
        {
            if (!Ready) return false;
            for (int i = 0; i < 3; i++) counts[i]--;
            return true;
        }
    }
}
