using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Packet;

internal enum ParserState
{
    PacketSize,
    PacketBody
}
public struct Packet
{
    // 消息头长度
    public const int HeadSize = sizeof(int); 
    public int Length { get; set ; }
    byte[] arr;
    public byte[] Bytes { get => arr ??= new byte[Length]; }
}

public class PacketParser
{
    private readonly CircularBuffer buffer;
    private ParserState state = ParserState.PacketSize;
    private Packet packet;
    private int capacity; //声明 packet 的最大容量
    private readonly byte[] temp = new byte[8];
    public PacketParser(CircularBuffer buffer, int capacity = 60000)
    {
        this.capacity = capacity;
        this.buffer = buffer;
    }

    public List<Packet> Parse()
    {
        List<Packet> packets = new List<Packet>();
        while (state == ParserState.PacketSize && buffer.Length >= HeadSize || state == ParserState.PacketBody && buffer.Length >= packet.Length)
        {
            switch (state)
            {
                case ParserState.PacketSize:
                    packet = new Packet();
                    buffer.Read(temp, 0, HeadSize);
                    packet.Length = BytesHelper.ToInt32(temp, 0);
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
        while (state == ParserState.PacketSize && buffer.Length >= HeadSize || state == ParserState.PacketBody && buffer.Length >= packet.Length)
        {
            switch (state)
            {
                case ParserState.PacketSize:
                    packet = new Packet();
                    await buffer.ReadAsync(temp, 0,HeadSize);
                    packet.Length = BytesHelper.ToInt32(temp, 0);
                    if (packet.Length > capacity)
                    {
                        throw new Exception($"packet too large, size: {packet.Length}");
                    }
                    state = ParserState.PacketBody;
                    break;
                case ParserState.PacketBody:
                    await buffer.ReadAsync(packet.Bytes, 0, packet.Length);
                    packets.Add(packet);
                    state = ParserState.PacketSize;
                    break;
            }
        }
        return packets;
    }
}
