using Game.Content.Features.Signals;

namespace Circuits.IC
{
    // The vanilla Compare gate's rule (LogicGateCompareSimulation), for Compare gates inside packages: 1 or 0.
    // Equal and Not Equal compare any two signals (shapes and colours too); the orderings hold only between two
    // numbers, and are 0 otherwise.
    public static class CompareRule
    {
        public static ISignal Apply(GateKind kind, ISignal a, ISignal b)
        {
            return IntegerSignal.Get(Holds(kind, a, b));
        }

        private static bool Holds(GateKind kind, ISignal a, ISignal b)
        {
            switch (kind)
            {
                case GateKind.CompareEqual:
                    return a.Equals(b);
                case GateKind.CompareNotEqual:
                    return !a.Equals(b);
            }

            if (a is not IntegerSignal x || b is not IntegerSignal y)
            {
                return false;
            }

            return kind switch
            {
                GateKind.CompareGreaterEqual => x.Value >= y.Value,
                GateKind.CompareGreater => x.Value > y.Value,
                GateKind.CompareLess => x.Value < y.Value,
                GateKind.CompareLessEqual => x.Value <= y.Value,
                _ => false
            };
        }
    }
}
