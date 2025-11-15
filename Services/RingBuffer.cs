using System;

namespace SPS.App.Services;

public sealed class RingBuffer<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _count;
    private readonly object _syncRoot = new();

    public RingBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _buffer = new T[capacity];
    }

    public int Capacity => _buffer.Length;

    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _count;
            }
        }
    }

    public double FillRatio
    {
        get
        {
            lock (_syncRoot)
            {
                return _count / (double)Capacity;
            }
        }
    }

    public void Write(T value)
    {
        lock (_syncRoot)
        {
            _buffer[_head] = value;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length)
            {
                _count++;
            }
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _count = 0;
        }
    }

    public void Fill(T value)
    {
        lock (_syncRoot)
        {
            Array.Fill(_buffer, value);
        }
    }

    public T[] Snapshot(int count)
    {
        var destination = new T[count];
        Snapshot(destination);
        return destination;
    }

    public void Snapshot(Span<T> destination)
    {
        lock (_syncRoot)
        {
            int toCopy = Math.Min(destination.Length, _count);
            int start = (_head - toCopy + _buffer.Length) % _buffer.Length;

            for (int i = 0; i < toCopy; i++)
            {
                int index = (start + i) % _buffer.Length;
                destination[destination.Length - toCopy + i] = _buffer[index];
            }

            if (toCopy < destination.Length)
            {
                destination[..(destination.Length - toCopy)].Clear();
            }
        }
    }
}
