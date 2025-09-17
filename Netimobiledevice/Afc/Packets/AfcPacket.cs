namespace Netimobiledevice.Afc.Packets;

internal abstract class AfcPacket
{
    public AfcHeader Header { get; set; } = new AfcHeader();

    public int HeaderSize => Header.GetBytes().Length;
    public int PacketSize => HeaderSize + DataSize;

    public abstract int DataSize { get; }

    public abstract byte[] GetBytes();
}
