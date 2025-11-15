using System;
using SPS.App.Models;

namespace SPS.App.Services;

public sealed class ParamsBus
{
    public event EventHandler<ParamsChangedEventArgs>? ParamsChanged;

    public void Publish(SensorType sensor) => Publish(ParamsChangedEventArgs.For(sensor));

    public void Publish(ParamsChangedEventArgs args) => ParamsChanged?.Invoke(this, args);
}

public sealed class ParamsChangedEventArgs : EventArgs
{
    private ParamsChangedEventArgs(SensorType sensor)
    {
        Sensor = sensor;
    }

    public SensorType Sensor { get; }

    public bool IsBroadcast => Sensor == SensorType.Any;

    public static ParamsChangedEventArgs For(SensorType sensor) => new(sensor);

    public static ParamsChangedEventArgs Broadcast { get; } = new(SensorType.Any);
}
