using System;
using Game.Content.Features.Signals;
using Game.Core;
using Game.Core.Blueprint;
using Game.Core.Serialization;

namespace Circuits.IC
{
    // A signal of any kind (a shape, a colour, ...) as text for firmware: a Constant Signal's configuration
    // serialized by the game, as blueprints serialize it, after the game version that wrote it
    // ("<version>:<base64>"), so it reads back as the game reads an old blueprint.
    public static class SignalBlob
    {
        public static string Encode(ConstantSignalConfiguration configuration)
        {
            byte[] bytes = Converter().SerializeDataIntoByteArray(configuration.Sync);
            return $"{(int)GameVersionEnvironment.CurrentVersion}:{Convert.ToBase64String(bytes)}";
        }

        public static ISignal Decode(string blob)
        {
            int colon = blob.IndexOf(':');
            if (colon <= 0 || !int.TryParse(blob.Substring(0, colon), out int version))
            {
                return NullSignal.Instance;
            }

            try
            {
                var configuration = new ConstantSignalConfiguration();
                Converter().DeserializeByteArrayIntoData(
                    Convert.FromBase64String(blob.Substring(colon + 1)),
                    (GameVersion)version,
                    configuration.Sync);
                return configuration.Value ?? NullSignal.Instance;
            }
            catch (Exception e) when (e is FormatException or System.IO.IOException)
            {
                return NullSignal.Instance;
            }
        }

        private static BlueprintRawDataConverter Converter()
        {
#pragma warning disable CS0618 // Obsolete static access, as Firmware uses
            return new BlueprintRawDataConverter(StaticGameCoreAccessor.G.DataSerializers, ICSession.Logger);
#pragma warning restore CS0618
        }
    }
}
