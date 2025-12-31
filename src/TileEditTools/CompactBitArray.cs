using System.Collections;
using System.IO;

namespace TileEditTools;

internal readonly record struct CompactBitArray(
    byte[] Array,
    int BitLength
)
{
    public void Serialize(BinaryWriter w)
    {
        w.Write(BitLength);
        w.Write(Array);
    }

    public BitArray ToBitArray()
    {
        return new BitArray(Array)
        {
            Length = BitLength,
        };
    }
    
    public static CompactBitArray Deserialize(BinaryReader r)
    {
        var length = r.ReadInt32();
        var bytes = new byte[(length + 7) / 8];
        {
            _ = r.Read(bytes);
        }

        return new CompactBitArray(bytes, length);
    }
    
    public static CompactBitArray FromBitArray(BitArray array)
    {
        var bytes = new byte[(array.Length + 7) / 8];
        {
            array.CopyTo(bytes, 0);
        }

        return new CompactBitArray(bytes, array.Length);
    }
}
