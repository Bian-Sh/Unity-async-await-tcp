using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

internal enum ParserState
{
    PacketSize,
    PacketBody
}

public struct Packet
{
    public const int FlagIndex = 0;
    public const int OpcodeIndex = 1;

    public byte[] Bytes { get; }
    public int Length { get; set; }
    public Packet(uint length)
    {
        this.Length = 0;
        this.Bytes = new byte[length];
    }
    public Packet(byte[] bytes)
    {
        this.Bytes = bytes;
        this.Length = bytes.Length;
    }
    public byte Flag() => Bytes[0];
    public ushort Opcode => BitConverter.ToUInt16(Bytes, OpcodeIndex);
}

public class PacketParser
{
    private readonly CircularBuffer buffer;
    private ParserState state = ParserState.PacketSize;
    private Packet packet;
    private uint capacity; //声明 packet 的最大容量
    public PacketParser(CircularBuffer buffer, uint capacity = 60000)
    {
        this.capacity = capacity;
        this.buffer = buffer;
    }

    public List<Packet> Parse()
    {
        List<Packet> packets = new List<Packet>();
        while (state == ParserState.PacketSize && buffer.Length >= 2 || state == ParserState.PacketBody && buffer.Length >= packet.Length)
        {
            switch (state)
            {
                case ParserState.PacketSize:
                    packet = new Packet(capacity);
                    buffer.Read(packet.Bytes, 0, sizeof(int));
                    packet.Length = BytesHelper.ToUInt16(packet.Bytes, 0);
                    if (packet.Length > capacity)
                    {
                        throw new Exception($"packet too large, size: {packet.Length}");
                    }
                    state = ParserState.PacketBody;
                    break;
                case ParserState.PacketBody:
                    buffer.Read(packet.Bytes, 0, packet.Length);
                    packets.Add(packet);
                    state = ParserState.PacketSize;
                    break;
            }
        }
        return packets;
    }

    public async Task<List<Packet>> ParseAsync()
    {
        List<Packet> packets = new List<Packet>();
        while (state == ParserState.PacketSize && buffer.Length >= 2 || state == ParserState.PacketBody && buffer.Length >= packet.Length)
        {
            switch (state)
            {
                case ParserState.PacketSize:
                    packet = new Packet(capacity);
                    await buffer.ReadAsync(packet.Bytes, 0, sizeof(int));
                    packet.Length = BytesHelper.ToUInt16(packet.Bytes, 0);
                    if (packet.Length > capacity)
                    {
                        throw new Exception($"packet too large, size: {packet.Length}");
                    }
                    state = ParserState.PacketBody;
                    break;
                case ParserState.PacketBody:
                    await buffer.ReadAsync(packet.Bytes, 0, (int)packet.Length);
                    packets.Add(packet);
                    state = ParserState.PacketSize;
                    break;
            }
        }
        return packets;
    }
}
