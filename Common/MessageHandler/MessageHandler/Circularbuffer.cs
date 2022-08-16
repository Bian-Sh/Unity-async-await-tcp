using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class CircularBuffer : Stream
{
    public int ChunkSize = 8192;
    private readonly ConcurrentQueue<byte[]> bufferQueue = new ConcurrentQueue<byte[]>();
    private readonly ConcurrentQueue<byte[]> bufferCache = new ConcurrentQueue<byte[]>();
    public int LastIndex { get; set; }
    public int FirstIndex { get; set; }

    private byte[] lastBuffer;

    public CircularBuffer() : this(8192) { }
    public CircularBuffer(int chunkSize)
    {
        ChunkSize = chunkSize;
        AddLast();
    }
    public override long Length
    {
        get
        {
            int c = 0;
            if (bufferQueue.Count == 0)
            {
                c = 0;
            }
            else
            {
                c = (bufferQueue.Count - 1) * ChunkSize + LastIndex - FirstIndex;
            }
            if (c < 0)
            {
                Debug.LogError($"TBuffer count < 0: {bufferQueue.Count}, {LastIndex}, {FirstIndex}");
            }
            return c;
        }
    }

    public void AddLast()
    {
        if (!bufferCache.TryDequeue(out var buffer))
        {
            buffer = new byte[ChunkSize];
        }
        bufferQueue.Enqueue(buffer);
        lastBuffer = buffer;
    }

    public void RemoveFirst()
    {
        if (bufferQueue.TryDequeue(out var result))
        {
            bufferCache.Enqueue(result);
        }
    }

    public byte[] First
    {
        get
        {
            if (bufferQueue.Count == 0)
            {
                AddLast();
            }
            if (bufferQueue.TryPeek(out var result))
            {
                return result;
            }
            return null;
        }
    }

    public byte[] Last
    {
        get
        {
            if (bufferQueue.Count == 0)
            {
                AddLast();
            }
            return lastBuffer;
        }
    }

    /// <summary>
    /// 从CircularBuffer读取到stream流中
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public async Task ReadAsync(Stream stream)
    {
        long buffLength = Length;
        int sendSize = ChunkSize - FirstIndex;
        if (sendSize > buffLength)
        {
            sendSize = (int)buffLength;
        }

        await stream.WriteAsync(First, FirstIndex, sendSize);

        FirstIndex += sendSize;
        if (FirstIndex == ChunkSize)
        {
            FirstIndex = 0;
            RemoveFirst();
        }
    }

    /// <summary>
    /// 从stream流写到CircularBuffer中
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public async Task<int> WriteAsync(Stream stream)
    {
        int size = ChunkSize - LastIndex;

        int n = await stream.ReadAsync(Last, LastIndex, size);

        if (n == 0)
        {
            return 0;
        }

        LastIndex += n;

        if (LastIndex == ChunkSize)
        {
            AddLast();
            LastIndex = 0;
        }

        return n;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer.Length < offset + count)
        {
            throw new Exception($"bufferList length < coutn, buffer length: {buffer.Length} {offset} {count}");
        }
        // long length = Length;
        // if (length < count)
        // {
        //     count = (int)length;
        // }

        int alreadyCopyCount = 0;
        while (alreadyCopyCount < count)
        {
            int n = count - alreadyCopyCount;
            if (ChunkSize - FirstIndex > n)
            {
                Array.Copy(First, FirstIndex, buffer, alreadyCopyCount + offset, n);
                FirstIndex += n;
                alreadyCopyCount += n;
            }
            else
            {
                Array.Copy(First, FirstIndex, buffer, alreadyCopyCount + offset, ChunkSize - FirstIndex);
                alreadyCopyCount += ChunkSize - FirstIndex;
                FirstIndex = 0;
                RemoveFirst();
            }
        }

        return count;
    }

    public void Write(byte[] buffer)
    {
        int alreadyCopyCount = 0;
        while (alreadyCopyCount < buffer.Length)
        {
            if (LastIndex == ChunkSize)
            {
                AddLast();
                LastIndex = 0;
            }

            int n = buffer.Length - alreadyCopyCount;
            if (ChunkSize - LastIndex > n)
            {
                Array.Copy(buffer, alreadyCopyCount, lastBuffer, LastIndex, n);
                LastIndex += buffer.Length - alreadyCopyCount;
                alreadyCopyCount += n;
            }
            else
            {
                Array.Copy(buffer, alreadyCopyCount, lastBuffer, LastIndex, ChunkSize - LastIndex);
                alreadyCopyCount += ChunkSize - LastIndex;
                LastIndex = ChunkSize;
            }
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        int alreadyCopyCount = 0;
        while (alreadyCopyCount < count)
        {
            if (LastIndex == ChunkSize)
            {
                AddLast();
                LastIndex = 0;
            }

            int n = count - alreadyCopyCount;
            if (ChunkSize - LastIndex > n)
            {
                Array.Copy(buffer, alreadyCopyCount + offset, lastBuffer, LastIndex, n);
                LastIndex += count - alreadyCopyCount;
                alreadyCopyCount += n;
            }
            else
            {
                Array.Copy(buffer, alreadyCopyCount + offset, lastBuffer, LastIndex, ChunkSize - LastIndex);
                alreadyCopyCount += ChunkSize - LastIndex;
                LastIndex = ChunkSize;
            }
        }
    }

    public override void Flush() => throw new NotImplementedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
    public override void SetLength(long value) => throw new NotImplementedException();
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Position { get; set; }
}
