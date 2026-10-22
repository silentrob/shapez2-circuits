using Game.Content.Features.Signals;

namespace Circuits.IntGates
{
    // Never reorder or remove: a gate's mode is saved by its position in its building's list of operations.
    public enum IntegerOp { And, Or, Xor, Not, ShiftLeft, ShiftRight, Add, Subtract }

    // What the integer gates compute, shared by the gate buildings and gates inside packages so they cannot
    // differ. A wire carries a 32-bit integer; anything that is not a number (nothing, a shape, a conflict)
    // counts as 0, and the result is always a number, as vanilla gates always output one. Shifting right fills
    // with zeros (for masks), a negative amount shifts the other way, and sums wrap around.
    public static class IntegerOps
    {
        public static int ValueOf(ISignal signal)
        {
            return signal is IntegerSignal integer ? integer.Value : 0;
        }

        public static ISignal Apply(IntegerOp op, ISignal a, ISignal b)
        {
            return IntegerSignal.Get(Compute(op, ValueOf(a), ValueOf(b)));
        }

        public static int Compute(IntegerOp op, int a, int b)
        {
            unchecked
            {
                return op switch
                {
                    IntegerOp.And => a & b,
                    IntegerOp.Or => a | b,
                    IntegerOp.Xor => a ^ b,
                    IntegerOp.Not => ~a,
                    IntegerOp.ShiftLeft => Shift(a, b),
                    IntegerOp.ShiftRight => Shift(a, -b),
                    IntegerOp.Add => a + b,
                    _ => a - b
                };
            }
        }

        public static bool TakesTwo(IntegerOp op)
        {
            return op != IntegerOp.Not;
        }

        // Left for a positive amount, right (filling with zeros) for a negative one; 32 or more clears it.
        private static int Shift(int value, int amount)
        {
            if (amount >= 32 || amount <= -32)
            {
                return 0;
            }

            return amount >= 0 ? value << amount : (int)((uint)value >> -amount);
        }
    }
}
