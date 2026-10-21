using UnityEngine;

namespace Builder.Assembler
{
    public sealed class AssemblerTarget : MonoBehaviour
    {
        public enum TargetType { Bag, Hopper, Output }
        public TargetType type;
        public Ingredient ingredient;
    }
}
