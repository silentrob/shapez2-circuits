using Game.Content.Features.Signals;

namespace Circuits.Latch
{
    // What a latch holds next, shared by the latch building and latches inside packages so they cannot differ:
    // Reset clears it; otherwise a truthy Data replaces it, and anything else leaves it.
    public static class LatchRule
    {
        public static ISignal Next(ISignal held, ISignal data, ISignal reset)
        {
            if (reset.IsTruthy())
            {
                return NullSignal.Instance;
            }

            return data.IsTruthy() ? data : held;
        }
    }
}
