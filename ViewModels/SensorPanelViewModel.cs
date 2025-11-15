using System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using SPS.App.Models;

namespace SPS.App.ViewModels;

public sealed class SensorPanelViewModel : ObservableObject
{
    private PropertyChangedEventHandler? _modeChangedHandler;
    private INotifyPropertyChanged? _modeNotifier;

    public SensorPanelViewModel(ImuModel imu)
    {
        SensorType = SensorType.Imu;
        Title = "IMU";
        Imu = imu;
        ModeOptions.Add(CreateModeOption("Chirp", ImuExcitationMode.Chirp, () => Imu!.Mode, mode => Imu!.Mode = mode, "Sweep frekuensi kontinu"));
        ModeOptions.Add(CreateModeOption("Pulse", ImuExcitationMode.Pulse, () => Imu!.Mode, mode => Imu!.Mode = mode, "Pulsa tunggal singkat"));
        ModeOptions.Add(CreateModeOption("Step", ImuExcitationMode.Step, () => Imu!.Mode, mode => Imu!.Mode = mode, "Langkah cepat ke posisi baru"));
        ModeOptions.Add(CreateModeOption("Drift Spike", ImuExcitationMode.DriftSpike, () => Imu!.Mode, mode => Imu!.Mode = mode, "Drift lambat dengan lonjakan"));
        AttachModeChanged(imu, nameof(ImuModel.Mode));
        RefreshModeOptions();
    }

    public SensorPanelViewModel(FsrModel fsr)
    {
        SensorType = SensorType.Fsr;
        Title = "FSR";
        Fsr = fsr;
        ModeOptions.Add(CreateModeOption("Tap", FsrExcitationMode.Tap, () => Fsr!.Mode, mode => Fsr!.Mode = mode, "Ketukan cepat tunggal"));
        ModeOptions.Add(CreateModeOption("Press & Hold", FsrExcitationMode.PressHold, () => Fsr!.Mode, mode => Fsr!.Mode = mode, "Tekan dan tahan gaya konstan"));
        ModeOptions.Add(CreateModeOption("Repeated Taps", FsrExcitationMode.RepeatedTaps, () => Fsr!.Mode, mode => Fsr!.Mode = mode, "Serangkaian ketukan periodik"));
        AttachModeChanged(fsr, nameof(FsrModel.Mode));
        RefreshModeOptions();
    }

    public SensorPanelViewModel(StrainModel strain)
    {
        SensorType = SensorType.Strain;
        Title = "Strain";
        Strain = strain;
        ModeOptions.Add(CreateModeOption("Load Step", StrainExcitationMode.LoadStep, () => Strain!.Mode, mode => Strain!.Mode = mode, "Langkah beban tiba-tiba"));
        ModeOptions.Add(CreateModeOption("Ramp", StrainExcitationMode.Ramp, () => Strain!.Mode, mode => Strain!.Mode = mode, "Peningkatan linear lambat"));
        ModeOptions.Add(CreateModeOption("Vibration", StrainExcitationMode.Vibration, () => Strain!.Mode, mode => Strain!.Mode = mode, "Getaran periodik"));
        AttachModeChanged(strain, nameof(StrainModel.Mode));
        RefreshModeOptions();
    }

    public SensorPanelViewModel(EmgModel emg)
    {
        SensorType = SensorType.Emg;
        Title = "EMG";
        Emg = emg;
        ModeOptions.Add(CreateModeOption("Short Burst", EmgExcitationMode.ShortBurst, () => Emg!.Mode, mode => Emg!.Mode = mode, "Burst singkat tegangan otot"));
        ModeOptions.Add(CreateModeOption("Fatigue Train", EmgExcitationMode.FatigueTrain, () => Emg!.Mode, mode => Emg!.Mode = mode, "Rangkaian panjang untuk simulasi fatigue"));
        AttachModeChanged(emg, nameof(EmgModel.Mode));
        RefreshModeOptions();
    }

    public SensorType SensorType { get; }

    public string Title { get; }

    public ImuModel? Imu { get; }

    public FsrModel? Fsr { get; }

    public StrainModel? Strain { get; }

    public EmgModel? Emg { get; }

    public ObservableCollection<SensorModeOptionViewModel> ModeOptions { get; } = new();

    public IReadOnlyList<SensorActionViewModel> Actions { get; } = Array.Empty<SensorActionViewModel>();

    public void Reset()
    {
        switch (SensorType)
        {
            case SensorType.Imu:
                Imu?.Reset();
                break;
            case SensorType.Fsr:
                Fsr?.Reset();
                break;
            case SensorType.Strain:
                Strain?.Reset();
                break;
            case SensorType.Emg:
                Emg?.Reset();
                break;
        }
        RefreshModeOptions();
    }

    private static SensorActionViewModel CreateAction(string name, Action execute) =>
        new(name, new RelayCommand(execute));

    private SensorModeOptionViewModel CreateModeOption<TEnum>(
        string name,
        TEnum modeValue,
        Func<TEnum> getter,
        Action<TEnum> setter,
        string? toolTip = null)
        where TEnum : struct, Enum
    {
        return new SensorModeOptionViewModel(
            name,
            (Enum)(object)modeValue,
            () => (Enum)(object)getter(),
            enumValue => setter((TEnum)(object)enumValue),
            toolTip);
    }

    private void AttachModeChanged(INotifyPropertyChanged notifier, string propertyName)
    {
        _modeNotifier = notifier;
        _modeChangedHandler = (_, args) =>
        {
            if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == propertyName)
            {
                RefreshModeOptions();
            }
        };
        notifier.PropertyChanged += _modeChangedHandler;
    }

    private void RefreshModeOptions()
    {
        foreach (var option in ModeOptions)
        {
            option.Refresh();
        }
    }
}
