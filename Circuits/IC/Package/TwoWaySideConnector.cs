using Game.Content.Features.Signals.Connections;
using Game.Core.Coordinates;
using Game.Core.Map.Simulation;

namespace Circuits.IC.Package
{
    // One package side, both receiving and sending. The simulation graph pairs connectors with
    // a.TryConnect(b) || b.TryConnect(a). Vanilla connectors only accept plain providers or receivers, which
    // this is neither, so the graph falls through to this connector, which links whatever the other side
    // offers:
    //   a wire's end (provider and receiver): both ways, the side's output into the wire and the wire into
    //     the side's input;
    //   a building's output facing the side directly (provider only): into the side's input;
    //   a building's input facing the side directly (receiver only): from the side's output.
    //
    // UpdatePriority gives the side the vanilla ordering for its role in the loaded circuit: an input side
    // updates after what feeds it, an output side before what it feeds, so a package adds no delay.
    public class TwoWaySideConnector : ITileSimulationConnector
    {
        private readonly ISignalReceiver Input;
        private readonly ISignalProvider Output;
        private readonly SideRole Role;

        public GlobalTilePivot Pivot { get; }

        public SimulationConnectorSocket ConnectorSocket => SimulationConnectorSocket.From(Pivot);

        public SimulationConnectorSocket CounterpartSocket => SimulationConnectorSocket.From(Pivot.CounterpartConnector());

        public SimulationConnectorUpdatePriority UpdatePriority => Role switch
        {
            SideRole.Input => SimulationConnectorUpdatePriority.MeLast,
            SideRole.Output => SimulationConnectorUpdatePriority.MeFirst,
            _ => SimulationConnectorUpdatePriority.DontCare
        };

        public TwoWaySideConnector(ISignalReceiver input, ISignalProvider output, SideRole role, in GlobalTilePivot pivot)
        {
            Input = input;
            Output = output;
            Role = role;
            Pivot = pivot;
        }

        public bool CanConnect(ISimulationConnector other)
        {
            if (other is not ITileSimulationConnector tile || Pivot != tile.Pivot.CounterpartConnector())
            {
                return false;
            }

            var sender = other as ISignalProviderConnector;
            var receiver = other as ISignalReceiverConnector;
            bool canReceive = sender != null && sender.Provider.Next == null;
            bool canSend = receiver != null && Output.Next == null;
            return sender != null && receiver != null ? canReceive && canSend : canReceive || canSend;
        }

        public bool TryConnect(ISimulationConnector other)
        {
            if (!CanConnect(other))
            {
                return false;
            }

            if (other is ISignalProviderConnector sender)
            {
                sender.Provider.Next = Input;
            }

            if (other is ISignalReceiverConnector receiver)
            {
                Output.Next = receiver.Receiver;
            }

            return true;
        }

        public bool TryDisconnect(ISimulationConnector other)
        {
            var sender = other as ISignalProviderConnector;
            var receiver = other as ISignalReceiverConnector;
            bool received = sender != null && sender.Provider.Next == Input;
            bool sent = receiver != null && Output.Next == receiver.Receiver;
            if (!received && !sent)
            {
                return false;
            }

            if (received)
            {
                sender.Provider.Next = null;
            }

            if (sent)
            {
                Output.Next = null;
            }

            return true;
        }
    }
}
