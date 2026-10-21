using System;
using System.Collections.Generic;
using System.Linq;
using Circuits.IC;
using Xunit;

namespace Circuits.Tests
{
    public class NetlistCodecTests
    {
        public static IEnumerable<object[]> Circuits => TestCircuits.ByName.Keys.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(Circuits))]
        public void EveryCircuitSurvivesARoundTrip(string name)
        {
            Netlist original = TestCircuits.ByName[name]();

            Assert.True(NetlistCodec.TryDecode(NetlistCodec.Encode(original), out Netlist decoded));
            Assert.Equal(original.Name, decoded.Name);
            Assert.Equal(original.NetCount, decoded.NetCount);
            Assert.Equal(original.SideRoles, decoded.SideRoles);
            Assert.Equal(original.SideNets, decoded.SideNets);
            Assert.Equal(original.Gates.Length, decoded.Gates.Length);
            for (int i = 0; i < original.Gates.Length; i++)
            {
                Assert.Equal(original.Gates[i].Kind, decoded.Gates[i].Kind);
                Assert.Equal(original.Gates[i].Output, decoded.Gates[i].Output);
                Assert.Equal(original.Gates[i].Inputs, decoded.Gates[i].Inputs);
            }
        }

        [Fact]
        public void TheEmptyNetlistSurvivesARoundTrip()
        {
            Assert.True(NetlistCodec.TryDecode(NetlistCodec.Encode(Netlist.Empty), out Netlist decoded));
            Assert.All(decoded.SideRoles, role => Assert.Equal(SideRole.Unused, role));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not base64!")]
        [InlineData("AAAA")]
        public void GarbageIsRejected(string encoded)
        {
            Assert.False(NetlistCodec.TryDecode(encoded, out _));
        }

        [Fact]
        public void TruncatedDataIsRejected()
        {
            byte[] bytes = Convert.FromBase64String(NetlistCodec.Encode(TestCircuits.HalfAdder()));
            string truncated = Convert.ToBase64String(bytes.Take(bytes.Length - 3).ToArray());

            Assert.False(NetlistCodec.TryDecode(truncated, out _));
        }

        [Fact]
        public void AGateReadingOutsideItsNetsIsRejected()
        {
            var broken = new Netlist(
                "Broken",
                netCount: 1,
                sideRoles: new[] { SideRole.Unused, SideRole.Output, SideRole.Unused, SideRole.Input },
                sideNets: new[] { -1, 0, -1, 0 },
                gates: new[] { new Gate(GateKind.Not, 0, 5) });

            Assert.False(NetlistCodec.TryDecode(NetlistCodec.Encode(broken), out _));
        }
    }
}
