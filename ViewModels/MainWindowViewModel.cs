using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using SPS.App.DSP;
using SPS.App.Models;
using SPS.App.Services;
using SPS.App.ViewModels.Plots;
using ScottPlot;
using ScottPlot.TickGenerators;

namespace SPS.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    public sealed record PresetOptionViewModel(string DisplayName, RunPreset Preset);

	private readonly struct FftPeaks
	{
		public static FftPeaks Empty => new FftPeaks(Array.Empty<double>(), Array.Empty<double>(), double.NaN, double.NaN, hasPeaks: false);

		public double[] Frequencies { get; }

		public double[] Magnitudes { get; }

		public double Min { get; }

		public double Max { get; }

		public bool HasPeaks { get; }

		public FftPeaks(double[] frequencies, double[] magnitudes, double min, double max, bool hasPeaks)
		{
			Frequencies = frequencies;
			Magnitudes = magnitudes;
			Min = min;
			Max = max;
			HasPeaks = hasPeaks;
		}
	}

	private readonly struct FftRange
	{
		public double Min { get; }

		public double Max { get; }

		public bool HasData { get; }

		public FftRange(double min, double max, bool hasData)
		{
			Min = min;
			Max = max;
			HasData = hasData;
		}
	}


	private readonly SignalEngine _engine;

    private readonly TransformsService _transformsService;

    private readonly FftService _fftService;

    private readonly ParamsBus _paramsBus;

    private readonly Dispatcher _dispatcher;

    private readonly PlotLoop _plotLoop;
    private readonly int _windowSampleCount;
    private readonly double[] _combinedScratch;

    private readonly ImuModel _imu;

    private readonly FsrModel _fsr;

    private readonly StrainModel _strain;

    private readonly EmgModel _emg;

    private readonly ParametersStore _parametersStore;

    private readonly JsonStorage _jsonStorage;

    private static readonly SensorType[] SensorOrder = new SensorType[4]
    {
        SensorType.Imu,
        SensorType.Fsr,
        SensorType.Strain,
        SensorType.Emg
    };

    private static readonly Dictionary<SensorType, (double Min, double Max, string AnnotationFormat)> TimePlotDefaults = new()
    {
        { SensorType.Imu, (0.0, 90.0, "Theta = {0:0.0} deg") },
        { SensorType.Fsr, (2.5, 3.5, "Vout = {0:0.000} V") },
        { SensorType.Strain, (0.0, 0.004, "Vout = {0:0.0000} V") },
        { SensorType.Emg, (0.0, 2.5, "Level = {0:0.00}") }
    };

    private const double CombinedYAxisClamp = 2.5;

    private const double ImuTimeLimit = 120.0;

    private const double FsrTimeMin = -0.2;

    private const double FsrTimeMax = 3.5;

    private const double StrainTimeLimit = 0.01;

    private const string ImuColorX = "#4FC3F7";

    private const string ImuColorY = "#1E88E5";

    private const string ImuColorZ = "#0D47A1";

    private readonly PlotSeries _combinedImuSeries;

    private readonly PlotSeries _combinedFsrSeries;

    private readonly PlotSeries _combinedStrainSeries;

    private readonly PlotSeries _combinedEmgSeries;

    private readonly PlotSeries _imuTimeSeries;

    private readonly PlotSeries _fsrTimeSeries;

    private readonly PlotSeries _strainTimeSeries;

    private readonly PlotSeries _emgTimeSeries;

    private readonly PlotSeries _imuFftSeries;

    private readonly PlotSeries _fsrFftSeries;

    private readonly PlotSeries _strainFftSeries;

    private readonly PlotSeries _emgFftSeries;

    private readonly PlotSeries _emgSoloFftSeries;

    private readonly PlotSeries _imuFftPeaksSeries;

    private readonly PlotSeries _fsrFftPeaksSeries;

    private readonly PlotSeries _strainFftPeaksSeries;

    private readonly PlotSeries _emgFftPeaksSeries;

    private readonly PlotSeries _emgFftCombinedPeaksSeries;

    private readonly string _settingsPath;

    private bool _isDisposed;

    private const string CombinedColorHex = "#9FA8DA";

    private double[] _bodeFrequenciesHz;

    private double[] _bodeFrequenciesLog10 = Array.Empty<double>();

    private double[] _bodeTickPositions = Array.Empty<double>();

    private string[] _bodeTickLabels = Array.Empty<string>();

    private SensorPanelViewModel _selectedSensor;

    private double _fps;

    private double _bufferPercent;

    private double _sampleRate;

    private string _statusMessage = string.Empty;

    private DateTime _lastFrameStamp;

    private readonly DispatcherTimer _paramsIdleTimer;
    private readonly DispatcherTimer _presetBannerTimer;

    private bool _pendingTransferUpdate;

    private CancellationTokenSource? _transferCts;

    private StateSpaceSystem? _combinedDiscrete;

    private bool _isCombinedStable;

    private double _combinedMaxPole = double.NaN;

    private string _combinedStatus = "Stable";

    public IReadOnlyList<SensorPanelViewModel> Sensors { get; }

    public IReadOnlyList<PresetOptionViewModel> PresetOptions { get; }

    private object? _currentParams;

    private RunPreset _selectedPreset;

    private bool _isUpdatingPresetFromStore;

    private string _presetBannerText = string.Empty;
    private string _activePresetLabel = string.Empty;
    public object? CurrentParams
    {
        get => _currentParams;
        private set => SetProperty(ref _currentParams, value);
    }

    public RunPreset SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value, nameof(SelectedPreset)))
            {
                if (!_isUpdatingPresetFromStore)
                {
                    ApplyPresetAndRefreshAll(value);
                }
                UpdatePresetBannerText();
            }
        }
    }

    public string PresetBannerText
    {
        get => _presetBannerText;
        private set => SetProperty(ref _presetBannerText, value, nameof(PresetBannerText));
    }

    public SensorPanelViewModel SelectedSensor
    {
        get
        {
            return _selectedSensor;
        }
        set
        {
            if (SetProperty(ref _selectedSensor, value, "SelectedSensor"))
            {
                UpdateCurrentParams();
            }
        }
    }

    private void UpdateCurrentParams()
    {
        CurrentParams = _parametersStore.GetOrCreate(SelectedSensor.SensorType,
            () => DefaultsFactory.For(SelectedSensor.SensorType));
    }

    public MultiSeriesPlotViewModel CombinedTimePlot { get; }

    public MultiSeriesPlotViewModel ImuTimePlot { get; }

    public MultiSeriesPlotViewModel FsrTimePlot { get; }

    public MultiSeriesPlotViewModel StrainTimePlot { get; }

    public MultiSeriesPlotViewModel EmgTimePlot { get; }

    public MultiSeriesPlotViewModel ImuFftPlot { get; }

    public MultiSeriesPlotViewModel FsrFftPlot { get; }

    public MultiSeriesPlotViewModel EmgFftPlot { get; }

    public MultiSeriesPlotViewModel StrainEmgFftPlot { get; }

    public MultiSeriesPlotViewModel BodePlot { get; }

    public PoleZeroPlotViewModel PoleZeroPlot { get; }

    public IReadOnlyList<LegendEntry> Legend { get; }

    public string ScenarioText { get; }

    public Uri SchematicImageSource { get; } = new Uri("pack://application:,,,/SPS.App;component/Assets/SchematicPlaceholder.png", UriKind.Absolute);

    public RelayCommand StartCommand { get; }

    public RelayCommand StopCommand { get; }

    public RelayCommand ResetCommand { get; }

    public RelayCommand SelectSensorCommand { get; }

    public string ActivePresetLabel
    {
        get => _activePresetLabel;
        private set => SetProperty(ref _activePresetLabel, value, nameof(ActivePresetLabel));
    }

    public double FramesPerSecond
    {
        get
        {
            return _fps;
        }
        private set
        {
            SetProperty(ref _fps, value, "FramesPerSecond");
        }
    }

    public double BufferPercent
    {
        get
        {
            return _bufferPercent;
        }
        private set
        {
            if (SetProperty(ref _bufferPercent, value, "BufferPercent"))
            {
                OnPropertyChanged("BufferStatus");
                OnPropertyChanged("BufferStatusBrush");
            }
        }
    }

    public double SampleRate => _sampleRate;

    public bool IsCombinedAvailable => _combinedDiscrete != null;

    public bool IsCombinedStable
    {
        get
        {
            return _isCombinedStable;
        }
        private set
        {
            SetProperty(ref _isCombinedStable, value, "IsCombinedStable");
        }
    }

    public double CombinedMaxPole
    {
        get
        {
            return _combinedMaxPole;
        }
        private set
        {
            SetProperty(ref _combinedMaxPole, value, "CombinedMaxPole");
        }
    }

    public string CombinedStatus
    {
        get
        {
            return _combinedStatus;
        }
        private set
        {
            SetProperty(ref _combinedStatus, value, "CombinedStatus");
        }
    }

    public string CombinedFormulaText => "Combined output y (unity weights, normalized inputs)";

    public string StatusMessage
    {
        get
        {
            return _statusMessage;
        }
        private set
        {
            SetProperty(ref _statusMessage, value, "StatusMessage");
        }
    }

    public string BufferStatus
    {
        get
        {
            double bufferPercent = BufferPercent;
            if (bufferPercent < 80.0)
            {
                return "OK";
            }
            if (bufferPercent < 95.0)
            {
                return "Busy";
            }
            return "Saturated";
        }
    }

    public Brush BufferStatusBrush
    {
        get
        {
            double bufferPercent = BufferPercent;
            if (bufferPercent < 80.0)
            {
                return Brushes.MediumSeaGreen;
            }
            if (bufferPercent < 95.0)
            {
                return Brushes.Goldenrod;
            }
            return Brushes.IndianRed;
        }
    }

    public MainWindowViewModel(SignalEngine engine, ParametersStore parametersStore, JsonStorage jsonStorage, TransformsService transformsService, FftService fftService, ParamsBus paramsBus, Dispatcher dispatcher, ImuModel imu, FsrModel fsr, StrainModel strain, EmgModel emg)
    {
        //IL_0330: Unknown result type (might be due to invalid IL or missing references)
        //IL_0335: Unknown result type (might be due to invalid IL or missing references)
        //IL_034f: Expected O, but got Unknown
        _engine = engine;
        _parametersStore = parametersStore;
        _jsonStorage = jsonStorage;
        _transformsService = transformsService;
        _fftService = fftService;
        _paramsBus = paramsBus;
        _dispatcher = dispatcher;
        _imu = imu;
        _fsr = fsr;
        _strain = strain;
        _emg = emg;
        PresetOptions = new List<PresetOptionViewModel>
        {
            new("Baseline Stable", RunPreset.BaselineStable),
            new("Fast Exercise", RunPreset.FastExercise),
            new("Drift Bias", RunPreset.DriftBias),
            new("Custom", RunPreset.Custom)
        };
        _parametersStore.PresetChanged += OnPresetChanged;
        _isUpdatingPresetFromStore = true;
        SelectedPreset = _parametersStore.CurrentPreset;
        _isUpdatingPresetFromStore = false;
        Sensors = new List<SensorPanelViewModel>
        {
            new SensorPanelViewModel(_imu),
            new SensorPanelViewModel(_fsr),
            new SensorPanelViewModel(_strain),
            new SensorPanelViewModel(_emg)
        };
        _selectedSensor = Sensors.First();
        _currentParams = _parametersStore.GetOrCreate(SelectedSensor.SensorType, () => DefaultsFactory.For(SelectedSensor.SensorType));

        CombinedTimePlot = new MultiSeriesPlotViewModel("Combined Time (raw)", "Time (s)", "Amplitude");
        CombinedTimePlot.AnnotationText = "Raw IMU deg / FSR V / Strain V / EMG a.u.";
        ImuTimePlot = new MultiSeriesPlotViewModel("IMU - Time", "Time (s)", "Accel (m/s^2)");
        FsrTimePlot = new MultiSeriesPlotViewModel("FSR - Time", "Time (s)", "Voltage (V)");
        StrainTimePlot = new MultiSeriesPlotViewModel("Strain - Time", "Time (s)", "Voltage (V)");
        EmgTimePlot = new MultiSeriesPlotViewModel("EMG - Time", "Time (s)", "Activation (a.u.)");
        ImuFftPlot = new MultiSeriesPlotViewModel("IMU - FFT", "Frequency (Hz)", "Magnitude (dB)");
        FsrFftPlot = new MultiSeriesPlotViewModel("FSR - FFT", "Frequency (Hz)", "Magnitude (dB)");
        EmgFftPlot = new MultiSeriesPlotViewModel("EMG - FFT", "Frequency (Hz)", "Magnitude (dB)");
        StrainEmgFftPlot = new MultiSeriesPlotViewModel("Strain / EMG - FFT", "Frequency (Hz)", "Magnitude (dB)");
        BodePlot = new MultiSeriesPlotViewModel("S-Domain (Bode Magnitude)", "Frequency (Hz)", "dB");
        PoleZeroPlot = new PoleZeroPlotViewModel();
        ConfigureTimePlot(CombinedTimePlot, yFormat: "0.00");
        ConfigureTimePlot(ImuTimePlot, yFormat: "0.00");
        ConfigureTimePlot(FsrTimePlot, yFormat: "0.000");
        ConfigureTimePlot(StrainTimePlot, yFormat: "0.0000");
        ConfigureTimePlot(EmgTimePlot, yFormat: "0.00");
        ConfigureSpectrumPlot(ImuFftPlot);
        ConfigureSpectrumPlot(FsrFftPlot);
        ConfigureSpectrumPlot(EmgFftPlot);
        ConfigureSpectrumPlot(StrainEmgFftPlot);
        List<LegendEntry> list = SensorOrder.Select((SensorType sensor) => new LegendEntry(sensor.ToString(), PlotFactory.GetMediaColor(sensor))).ToList();
        list.Add(new LegendEntry("Combined", PlotFactory.MediaColorFromHex("#9FA8DA")));
        Legend = list;
        _isCombinedStable = true;
        _combinedStatus = "Stable (initialising…)";
        _paramsIdleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200.0)
        };
        _paramsIdleTimer.Tick += delegate
        {
            OnParamsIdleSettled();
        };
        _paramsBus.ParamsChanged += OnParamsChanged;
        _presetBannerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.0)
        };
        _presetBannerTimer.Tick += OnPresetBannerTimerTick;
        _presetBannerTimer.Start();
        ScenarioText = "IMU wrist bursts (+/-90 deg), palm force taps 0-60 N, brace strain 0-1000 microstrain, and EMG bursts 20-450 Hz give clinicians a snapshot of grip and motor recovery.";
        StartCommand = new RelayCommand(Start, () => !_engine.IsRunning);
        StopCommand = new RelayCommand(Stop, () => _engine.IsRunning);
        ResetCommand = new RelayCommand(Reset);
        SelectSensorCommand = new RelayCommand(parameter =>
        {
            if (parameter is SensorPanelViewModel sensor)
            {
                SelectedSensor = sensor;
            }
        });
        UpdatePresetBannerText();
        _sampleRate = _engine.SampleRate;
        double samplePeriod = _engine.SamplePeriod;
        _bodeFrequenciesHz = GenerateFrequencies(1.0, _sampleRate / 2.0, 256);
        _settingsPath = ResolveSettingsPath();
        _combinedImuSeries = new PlotSeries("IMU", Array.Empty<double>(), PlotFactory.GetHex(SensorType.Imu), samplePeriod, 0.0);
        _combinedFsrSeries = new PlotSeries("FSR", Array.Empty<double>(), PlotFactory.GetHex(SensorType.Fsr), samplePeriod, 0.0);
        _combinedStrainSeries = new PlotSeries("Strain", Array.Empty<double>(), PlotFactory.GetHex(SensorType.Strain), samplePeriod, 0.0);
        _combinedEmgSeries = new PlotSeries("EMG", Array.Empty<double>(), PlotFactory.GetHex(SensorType.Emg), samplePeriod, 0.0);
        CombinedTimePlot.Series = new PlotSeries[4] { _combinedImuSeries, _combinedFsrSeries, _combinedStrainSeries, _combinedEmgSeries };
        _imuTimeSeries = new PlotSeries("IMU", Array.Empty<double>(), PlotFactory.GetHex(SensorType.Imu), samplePeriod, 0.0);
        ImuTimePlot.Series = new PlotSeries[1] { _imuTimeSeries };
        _fsrTimeSeries = new PlotSeries("FSR", Array.Empty<double>(), PlotFactory.GetHex(SensorType.Fsr), samplePeriod, 0.0);
        FsrTimePlot.Series = new PlotSeries[1] { _fsrTimeSeries };
        _strainTimeSeries = new PlotSeries("Strain", Array.Empty<double>(), PlotFactory.GetHex(SensorType.Strain), samplePeriod, 0.0);
        StrainTimePlot.Series = new PlotSeries[1] { _strainTimeSeries };
        _emgTimeSeries = new PlotSeries("EMG", Array.Empty<double>(), PlotFactory.GetHex(SensorType.Emg), samplePeriod, 0.0);
        EmgTimePlot.Series = new PlotSeries[1] { _emgTimeSeries };
        _imuFftSeries = new PlotSeries("IMU", Array.Empty<double>(), Array.Empty<double>(), PlotFactory.GetHex(SensorType.Imu));
        _imuFftPeaksSeries = new PlotSeries("IMU Peaks", Array.Empty<double>(), Array.Empty<double>(), PlotFactory.GetHex(SensorType.Imu), marker: true);
        ImuFftPlot.Series = new PlotSeries[2] { _imuFftSeries, _imuFftPeaksSeries };
        _fsrFftSeries = new PlotSeries("FSR", Array.Empty<double>(), Array.Empty<double>(), PlotFactory.GetHex(SensorType.Fsr));
        _fsrFftPeaksSeries = new PlotSeries("FSR Peaks", Array.Empty<double>(), Array.Empty<double>(), PlotFactory.GetHex(SensorType.Fsr), marker: true);
        FsrFftPlot.Series = new PlotSeries[2] { _fsrFftSeries, _fsrFftPeaksSeries };
        _strainFftSeries = new PlotSeries("Strain", Array.Empty<double>(), Array.Empty<double>(), PlotFactory.GetHex(SensorType.Strain));
        _strainFftPeaksSeries = new PlotSeries("Strain Peaks", Array.Empty<double>(), Array.Empty<double>(), PlotFactory.GetHex(SensorType.Strain), marker: true);
        _emgFftSeries = new PlotSeries("EMG", Array.Empty<double>(), Array.Empty<double>(), PlotFactory.GetHex(SensorType.Emg));
        _emgFftCombinedPeaksSeries = new PlotSeries("EMG Peaks", Array.Empty<double>(), Array.Empty<double>(), PlotFactory.GetHex(SensorType.Emg), marker: true);
        StrainEmgFftPlot.Series = new PlotSeries[4] { _strainFftSeries, _strainFftPeaksSeries, _emgFftSeries, _emgFftCombinedPeaksSeries };
        _emgSoloFftSeries = new PlotSeries("EMG", Array.Empty<double>(), Array.Empty<double>(), PlotFactory.GetHex(SensorType.Emg));
        _emgFftPeaksSeries = new PlotSeries("EMG Peaks", Array.Empty<double>(), Array.Empty<double>(), PlotFactory.GetHex(SensorType.Emg), marker: true);
        EmgFftPlot.Series = new PlotSeries[2] { _emgSoloFftSeries, _emgFftPeaksSeries };
        _plotLoop = new PlotLoop(_engine, _fftService);
        _windowSampleCount = _plotLoop.WindowSampleCount;
        _combinedScratch = new double[_windowSampleCount * SensorOrder.Length];
        _plotLoop.FrameReady += OnPlotLoopFrame;
        _engine.Reset();
        _plotLoop.Reset();
        UpdateBodeAxisScaling();
        QueueTransferRefresh();
        OnParamsIdleSettled();
        _statusMessage = EvaluateStatus();
        LoadParameterStateSafe();
    }

	private void Start()
	{
		_engine.Start();
		_plotLoop.Start();
		StartCommand.RaiseCanExecuteChanged();
		StopCommand.RaiseCanExecuteChanged();
	}

	private void Stop()
	{
		_plotLoop.Stop();
		_engine.Stop();
		StartCommand.RaiseCanExecuteChanged();
		StopCommand.RaiseCanExecuteChanged();
	}

    private void Reset()
    {
        Stop();
        foreach (SensorPanelViewModel sensor in Sensors)
        {
			sensor.Reset();
		}
		_engine.Reset();
		_plotLoop.Reset();
		FramesPerSecond = 0.0;
        BufferPercent = 0.0;
        StatusMessage = EvaluateStatus();
        QueueTransferRefresh();
    }

    private void ApplyPresetAndRefreshAll(RunPreset preset)
    {
        _parametersStore.ApplyPreset(preset);
        ForceFullVisualizationRefresh();
    }

    private void ForceFullVisualizationRefresh()
    {
        _engine.RegenerateAllSensors();
        _plotLoop.RequestImmediateFrame();
        QueueTransferRefresh();
    }

	private void OnPlotLoopFrame(object? sender, PlotLoopFrame frame)
	{
		if (!_dispatcher.CheckAccess())
		{
			_dispatcher.Invoke((Action)delegate
			{
				OnPlotLoopFrame(sender, frame);
			});
			return;
		}
		FramesPerSecond = UpdateFps();
		BufferPercent = frame.BufferFill * 100.0;
		StatusMessage = EvaluateStatus();
		double samplePeriod = frame.SamplePeriod;
		int sampleCount = frame.SamplesInWindow;
		double windowStart = 0.0;
		double windowEnd = frame.WindowSeconds;
		double displayedSeconds = Math.Min(sampleCount * samplePeriod, frame.WindowSeconds);
		double offset = (sampleCount > 0) ? Math.Max(0.0, windowEnd - displayedSeconds) : 0.0;

		double[] imuSamples = frame.Signals[SensorType.Imu];
		double[] fsrSamples = frame.Signals[SensorType.Fsr];
		double[] strainSamples = frame.Signals[SensorType.Strain];
		double[] emgSamples = frame.Signals[SensorType.Emg];

		double imuLatest = GetLatestSample(imuSamples, sampleCount);
		double fsrLatest = GetLatestSample(fsrSamples, sampleCount);
		double strainLatest = GetLatestSample(strainSamples, sampleCount);
		double emgLatest = GetLatestSample(emgSamples, sampleCount);

		UpdateSignalSeries(_combinedImuSeries, imuSamples, samplePeriod, offset);
		UpdateSignalSeries(_combinedFsrSeries, fsrSamples, samplePeriod, offset);
		UpdateSignalSeries(_combinedStrainSeries, strainSamples, samplePeriod, offset);
		UpdateSignalSeries(_combinedEmgSeries, emgSamples, samplePeriod, offset);
		CombinedTimePlot.SetXAxisLimits(windowStart, windowEnd);
		(double combinedMin, double combinedMax) = ComputeCombinedAxisRange(sampleCount, imuSamples, fsrSamples, strainSamples, emgSamples);
		CombinedTimePlot.SetYAxisLimits(combinedMin, combinedMax);
		CombinedTimePlot.AnnotationText = "Raw IMU / FSR / Strain / EMG traces";
		CombinedTimePlot.Invalidate();

		UpdateSignalSeries(_imuTimeSeries, imuSamples, samplePeriod, offset);
		ImuTimePlot.SetXAxisLimits(windowStart, windowEnd);
		(double imuMin, double imuMax) = GetFixedTimeAxisRange(SensorType.Imu);
		ImuTimePlot.SetYAxisLimits(imuMin, imuMax);
		ImuTimePlot.AnnotationText = FormatSensorAnnotation(SensorType.Imu, imuLatest);
		ImuTimePlot.Invalidate();

		UpdateSignalSeries(_fsrTimeSeries, fsrSamples, samplePeriod, offset);
		FsrTimePlot.SetXAxisLimits(windowStart, windowEnd);
		(double fsrMin, double fsrMax) = GetFixedTimeAxisRange(SensorType.Fsr);
		FsrTimePlot.SetYAxisLimits(fsrMin, fsrMax);
		FsrTimePlot.AnnotationText = FormatSensorAnnotation(SensorType.Fsr, fsrLatest);
		FsrTimePlot.Invalidate();

		UpdateSignalSeries(_strainTimeSeries, strainSamples, samplePeriod, offset);
		StrainTimePlot.SetXAxisLimits(windowStart, windowEnd);
		(double strainMin, double strainMax) = GetFixedTimeAxisRange(SensorType.Strain);
		StrainTimePlot.SetYAxisLimits(strainMin, strainMax);
		StrainTimePlot.AnnotationText = FormatSensorAnnotation(SensorType.Strain, strainLatest);
		StrainTimePlot.Invalidate();

		UpdateSignalSeries(_emgTimeSeries, emgSamples, samplePeriod, offset);
		EmgTimePlot.SetXAxisLimits(windowStart, windowEnd);
		(double emgMin, double emgMax) = GetFixedTimeAxisRange(SensorType.Emg);
		EmgTimePlot.SetYAxisLimits(emgMin, emgMax);
		EmgTimePlot.AnnotationText = FormatSensorAnnotation(SensorType.Emg, emgLatest);
		EmgTimePlot.Invalidate();

		FftRange range = UpdateFftSeries(_imuFftSeries, _imuFftPeaksSeries, frame.Spectra[SensorType.Imu]);
		ApplyFftAxis(ImuFftPlot, frame.Spectra[SensorType.Imu], range);
		FftRange range2 = UpdateFftSeries(_fsrFftSeries, _fsrFftPeaksSeries, frame.Spectra[SensorType.Fsr]);
		ApplyFftAxis(FsrFftPlot, frame.Spectra[SensorType.Fsr], range2);
		FftBlock emgSpectrum = frame.Spectra[SensorType.Emg];
		FftRange range3 = UpdateFftSeries(_emgSoloFftSeries, _emgFftPeaksSeries, emgSpectrum);
		ApplyFftAxis(EmgFftPlot, emgSpectrum, range3);
		FftRange a = UpdateFftSeries(_strainFftSeries, _strainFftPeaksSeries, frame.Spectra[SensorType.Strain]);
		FftRange b = UpdateFftSeries(_emgFftSeries, _emgFftCombinedPeaksSeries, emgSpectrum);
		FftRange range4 = CombineFftRanges(a, b);
		ApplyFftAxis(StrainEmgFftPlot, frame.Spectra[SensorType.Strain], range4);
	}

	private double UpdateFps()
	{
		DateTime utcNow = DateTime.UtcNow;
		if (_lastFrameStamp == default(DateTime))
		{
			_lastFrameStamp = utcNow;
			return 0.0;
		}
		double totalSeconds = (utcNow - _lastFrameStamp).TotalSeconds;
		_lastFrameStamp = utcNow;
		if (totalSeconds <= 0.0)
		{
			return FramesPerSecond;
		}
		double num = 1.0 / totalSeconds;
		return (FramesPerSecond <= 0.0) ? num : (0.85 * FramesPerSecond + 0.15 * num);
	}

	private static void ConfigureTimePlot(MultiSeriesPlotViewModel plotViewModel, string? yFormat = null)
	{
		plotViewModel.ConfigurePlot(delegate(Plot plot)
		{
			plot.Axes.Bottom.TickGenerator = new NumericAutomatic();
			var leftTicks = new NumericAutomatic();
			if (!string.IsNullOrEmpty(yFormat))
			{
				leftTicks.LabelFormatter = (double value) => value.ToString(yFormat, CultureInfo.InvariantCulture);
			}
			plot.Axes.Left.TickGenerator = leftTicks;
		});
	}

	private static void ConfigureSpectrumPlot(MultiSeriesPlotViewModel plotViewModel)
	{
		plotViewModel.ConfigurePlot(delegate(Plot plot)
		{
			plot.Axes.Bottom.TickGenerator = new NumericAutomatic();
			plot.Axes.Left.TickGenerator = new NumericAutomatic();
			plot.Axes.Bottom.Label.Text = "Frequency (Hz)";
			plot.Axes.Left.Label.Text = "Magnitude (dB)";
		});
	}

	private static void ApplyFftAxis(MultiSeriesPlotViewModel plotViewModel, FftBlock block, FftRange range)
	{
		plotViewModel.SetXAxisLimits(0.0, block.SampleRate / 2.0);
		plotViewModel.SetYAxisLimits(range.Min, range.Max);
		plotViewModel.Invalidate();
	}

	private static void UpdateSignalSeries(PlotSeries series, double[] samples, double samplePeriod, double offset)
	{
		series.UpdateSignal(samples, samplePeriod, offset);
	}

    private static (double min, double max) GetFixedTimeAxisRange(SensorType sensor)
    {
        if (TimePlotDefaults.TryGetValue(sensor, out var tuple))
        {
            return (tuple.Min, tuple.Max);
        }

        return (-1.0, 1.0);
    }

    private static string? FormatSensorAnnotation(SensorType sensor, double value)
    {
        if (!double.IsFinite(value))
        {
            return null;
        }

        if (!TimePlotDefaults.TryGetValue(sensor, out var tuple))
        {
            return null;
        }

        return string.Format(CultureInfo.InvariantCulture, tuple.AnnotationFormat, value);
    }

    private static double GetLatestSample(double[] samples, int sampleCount)
    {
        if (samples == null || samples.Length == 0 || sampleCount <= 0)
        {
            return double.NaN;
        }

        int usable = Math.Min(sampleCount, samples.Length);
        if (usable <= 0)
        {
            return double.NaN;
        }

        int start = samples.Length - usable;
        return samples[start + usable - 1];
    }

	private (double min, double max) ComputeCombinedAxisRange(int sampleCount, params double[][] series)
	{
		if (sampleCount <= 0)
		{
			return (-1.0, 1.0);
		}

		int perSeriesCount = Math.Min(sampleCount, _windowSampleCount);
		if (perSeriesCount <= 0)
		{
			return (-1.0, 1.0);
		}

		int totalLength = 0;
		foreach (double[] array in series)
		{
			if (array.Length == 0)
			{
				continue;
			}

			totalLength += Math.Min(perSeriesCount, array.Length);
		}

		if (totalLength == 0)
		{
			return (-1.0, 1.0);
		}

		Span<double> scratch = _combinedScratch.AsSpan(0, totalLength);
		int offset = 0;
		foreach (double[] array in series)
		{
			if (array.Length == 0)
			{
				continue;
			}

			int count = Math.Min(perSeriesCount, array.Length);
			int start = array.Length - count;
			array.AsSpan(start, count).CopyTo(scratch.Slice(offset, count));
			offset += count;
		}

		scratch = scratch.Slice(0, offset);
		if (scratch.IsEmpty)
		{
			return (-1.0, 1.0);
		}

		double min = double.PositiveInfinity;
		double max = double.NegativeInfinity;
		foreach (double value in scratch)
		{
			if (!double.IsFinite(value))
			{
				continue;
			}

			if (value < min)
			{
				min = value;
			}
			if (value > max)
			{
				max = value;
			}
		}

		if (!double.IsFinite(min) || !double.IsFinite(max))
		{
			return (-1.0, 1.0);
		}

		return ExpandWithMargin(min, max);
	}

	private static (double min, double max) ExpandWithMargin(double low, double high, double marginFraction = 0.1)
	{
		if (double.IsNaN(low) || double.IsNaN(high))
		{
			return (min: -1.0, max: 1.0);
		}
		if (high < low)
		{
			double num = high;
			high = low;
			low = num;
		}
		double num2 = high - low;
		if (num2 < 1E-06)
		{
			double num3 = Math.Max(Math.Abs(high), 0.001) * marginFraction;
			return (min: low - num3, max: high + num3);
		}
		double num4 = num2 * marginFraction;
		return (min: low - num4, max: high + num4);
	}

	private static FftRange UpdateFftSeries(PlotSeries lineSeries, PlotSeries? peakSeries, FftBlock block)
	{
		lineSeries.UpdateLine(block.Frequency, block.MagnitudeDb);
		if (peakSeries != null)
		{
			FftPeaks peaks = ExtractFftPeaks(block);
			peakSeries.UpdateLine(peaks.Frequencies, peaks.Magnitudes);
			return ComputeFftRange(block, peaks);
		}
		return ComputeFftRange(block, FftPeaks.Empty);
	}

	private static FftPeaks ExtractFftPeaks(FftBlock block, int count = 5)
	{
		int num = Math.Min(block.MagnitudeDb.Length, block.Frequency.Length);
		if (num <= 1)
		{
			return FftPeaks.Empty;
		}
		int[] array = new int[count];
		double[] array2 = new double[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = -1;
			array2[i] = double.NegativeInfinity;
		}
		for (int j = 1; j < num; j++)
		{
			double num2 = block.MagnitudeDb[j];
			if (double.IsNaN(num2))
			{
				continue;
			}
			for (int k = 0; k < count; k++)
			{
				if (num2 > array2[k])
				{
					for (int num3 = count - 1; num3 > k; num3--)
					{
						array2[num3] = array2[num3 - 1];
						array[num3] = array[num3 - 1];
					}
					array2[k] = num2;
					array[k] = j;
					break;
				}
			}
		}
		int num4 = 0;
		for (int l = 0; l < count; l++)
		{
			if (array[l] >= 0)
			{
				num4++;
			}
		}
		if (num4 == 0)
		{
			return FftPeaks.Empty;
		}
		double[] array3 = new double[num4];
		double[] array4 = new double[num4];
		int num5 = 0;
		for (int m = 0; m < count; m++)
		{
			int num6 = array[m];
			if (num6 >= 0)
			{
				array3[num5] = block.Frequency[num6];
				array4[num5] = array2[m];
				num5++;
			}
		}
		Array.Sort(array3, array4);
		double num7 = array4[0];
		double num8 = array4[0];
		for (int n = 1; n < array4.Length; n++)
		{
			if (array4[n] < num7)
			{
				num7 = array4[n];
			}
			if (array4[n] > num8)
			{
				num8 = array4[n];
			}
		}
		return new FftPeaks(array3, array4, num7, num8, hasPeaks: true);
	}

	private static FftRange ComputeFftRange(FftBlock block, FftPeaks peaks)
	{
		bool flag = block.MagnitudeDb.Length != 0;
		double num = double.PositiveInfinity;
		double num2 = double.NegativeInfinity;
		double[] magnitudeDb = block.MagnitudeDb;
		foreach (double num3 in magnitudeDb)
		{
			if (!double.IsNaN(num3))
			{
				if (num3 < num)
				{
					num = num3;
				}
				if (num3 > num2)
				{
					num2 = num3;
				}
			}
		}
		if (double.IsPositiveInfinity(num) || double.IsNegativeInfinity(num2))
		{
			flag = false;
		}
		double value;
		double value2;
		if (peaks.HasPeaks)
		{
			value = peaks.Min - 20.0;
			value2 = peaks.Max + 6.0;
		}
		else if (flag)
		{
			value = num;
			value2 = num2;
		}
		else
		{
			value = -120.0;
			value2 = -80.0;
		}
		value = Math.Clamp(value, -140.0, 20.0);
		value2 = Math.Clamp(value2, -140.0, 20.0);
		if (value2 - value < 1.0)
		{
			double num4 = (value2 + value) / 2.0;
			value = num4 - 0.5;
			value2 = num4 + 0.5;
		}
		return new FftRange(value, value2, flag || peaks.HasPeaks);
	}

	private static FftRange CombineFftRanges(FftRange a, FftRange b)
	{
		if (!a.HasData)
		{
			return b;
		}
		if (!b.HasData)
		{
			return a;
		}
		double min = Math.Min(a.Min, b.Min);
		double max = Math.Max(a.Max, b.Max);
		return new FftRange(min, max, hasData: true);
	}

    private void LoadParameterStateSafe()
    {
        var snapshot = _jsonStorage.LoadOrDefault(_settingsPath);
        _parametersStore.Override(snapshot);
        UpdateCurrentParams();
    }

    private void SaveParameterStateSafe()
    {
        var snapshot = _parametersStore.Snapshot();
        _jsonStorage.Save(_settingsPath, snapshot);
    }



	private static string ResolveSettingsPath()
	{
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StrokeRecovery");
		return Path.Combine(path, "parameters.json");
	}

	public void Dispose()
	{
		if (!_isDisposed)
		{
			_isDisposed = true;
			_parametersStore.PresetChanged -= OnPresetChanged;
			_plotLoop.FrameReady -= OnPlotLoopFrame;
			_plotLoop.Dispose();
			_paramsIdleTimer.Stop();
			_presetBannerTimer.Tick -= OnPresetBannerTimerTick;
			_presetBannerTimer.Stop();
			_engine.Stop();
			SaveParameterStateSafe();
		}
	}

	private void UpdateBodeAxisScaling()
	{
		if (_bodeFrequenciesHz.Length == 0)
		{
			_bodeFrequenciesLog10 = Array.Empty<double>();
			_bodeTickPositions = Array.Empty<double>();
			_bodeTickLabels = Array.Empty<string>();
			return;
		}
		_bodeFrequenciesLog10 = Array.ConvertAll(_bodeFrequenciesHz, Math.Log10);
		double maxFrequency = _bodeFrequenciesHz[^1];
		(double[], string[]) tuple = BuildBodeTicks(maxFrequency);
		_bodeTickPositions = tuple.Item1;
		_bodeTickLabels = tuple.Item2;
		double value = _bodeFrequenciesLog10[0];
		double value2 = _bodeFrequenciesLog10[^1];
		BodePlot.SetXAxisLimits(value, value2);
		BodePlot.ConfigurePlot(delegate(Plot plot)
		{
			if (_bodeTickPositions.Length != 0)
			{
				plot.Axes.Bottom.TickGenerator = new NumericManual(_bodeTickPositions, _bodeTickLabels);
			}
			plot.Axes.Bottom.Label.Text = "Frequency (Hz)";
			plot.Axes.Left.Label.Text = "Magnitude (dB)";
		});
	}

	private static (double[] Positions, string[] Labels) BuildBodeTicks(double maxFrequency)
	{
		List<double> list = new List<double>();
		for (double num = 1.0; num <= maxFrequency * 1.01; num *= 10.0)
		{
			double[] array = new double[3] { 1.0, 2.0, 5.0 };
			foreach (double num2 in array)
			{
				double num3 = num2 * num;
				if (num3 >= 1.0 && num3 <= maxFrequency)
				{
					list.Add(num3);
				}
			}
		}
		if (list.Count == 0)
		{
			list.Add(maxFrequency);
		}
		list.Sort();
		double[] item = list.Select(Math.Log10).ToArray();
		string[] item2 = list.Select((double v) => (v >= 10.0) ? v.ToString("0") : v.ToString("0.##")).ToArray();
		return (Positions: item, Labels: item2);
	}

    private void QueueTransferRefresh()
    {
        _pendingTransferUpdate = true;
        _paramsIdleTimer.Stop();
        _paramsIdleTimer.Start();
        CombinedStatus = "Recomputing…";
        IsCombinedStable = false;
        CombinedMaxPole = double.NaN;
    }

	private void OnParamsIdleSettled()
	{
		_paramsIdleTimer.Stop();
		if (!_pendingTransferUpdate)
		{
			return;
		}
		_pendingTransferUpdate = false;
		CancellationTokenSource cancellationTokenSource = Interlocked.Exchange(ref _transferCts, new CancellationTokenSource());
		cancellationTokenSource?.Cancel();
		cancellationTokenSource?.Dispose();
		CancellationTokenSource current = _transferCts;
		Task.Run(async delegate
		{
			try
			{
				IReadOnlyList<TransferFunction> transfers = _engine.GetTransferFunctionsSnapshot();
				(List<PlotSeries> Bode, List<(Complex Point, bool IsPole, System.Windows.Media.Color Color)> PoleZero) visuals = BuildTransferVisuals(transfers);
				current.Token.ThrowIfCancellationRequested();
				await _dispatcher.InvokeAsync((Action)delegate
				{
					BodePlot.Series = visuals.Bode;
					PoleZeroPlot.Points = visuals.PoleZero;
				});
			}
			catch (OperationCanceledException)
			{
			}
			finally
			{
				if (Interlocked.CompareExchange(ref _transferCts, null, current) == current)
				{
					current.Dispose();
				}
			}
		}, current.Token);
	}

	private (List<PlotSeries> Bode, List<(Complex Point, bool IsPole, System.Windows.Media.Color Color)> PoleZero) BuildTransferVisuals(IReadOnlyList<TransferFunction> transfers)
	{
		List<PlotSeries> list = new List<PlotSeries>();
		List<(Complex, bool, System.Windows.Media.Color)> list2 = new List<(Complex, bool, System.Windows.Media.Color)>();
		foreach (TransferFunction transfer in transfers)
		{
			double[] y = _transformsService.ComputeBodeMagnitude(transfer, _bodeFrequenciesHz);
			string hex = PlotFactory.GetHex(transfer.Sensor);
			list.Add(new PlotSeries(transfer.Sensor.ToString(), _bodeFrequenciesLog10, y, hex));
			System.Windows.Media.Color mediaColor = PlotFactory.GetMediaColor(transfer.Sensor);
			Complex[] digitalZeros = transfer.DigitalZeros;
			foreach (Complex item in digitalZeros)
			{
				list2.Add((item, false, mediaColor));
			}
			Complex[] digitalPoles = transfer.DigitalPoles;
			foreach (Complex item2 in digitalPoles)
			{
				list2.Add((item2, true, mediaColor));
			}
		}
        double[] weights = _transformsService.GetDefaultNormalizedWeights(transfers);
        StateSpaceSystem stateSpaceSystem = (_combinedDiscrete = _transformsService.BuildCombinedDiscrete(transfers, weights, _sampleRate));
		OnPropertyChanged("IsCombinedAvailable");
		if (stateSpaceSystem == null)
		{
            CombinedStatus = "Combined response unavailable (fixed weights produced zero gain).";
			CombinedMaxPole = double.NaN;
			IsCombinedStable = false;
			return (Bode: list, PoleZero: list2);
		}
		TransformsService.CombinedStability combinedStability = _transformsService.CheckDiscreteStability(stateSpaceSystem.A);
		CombinedMaxPole = combinedStability.MaxMagnitude;
		IsCombinedStable = combinedStability.IsStable;
		if (!combinedStability.IsStable)
		{
			CombinedStatus = "Combined model is UNSTABLE (|λ|max = " + FormatMagnitude(combinedStability.MaxMagnitude) + ")";
			return (Bode: list, PoleZero: list2);
		}
		CombinedStatus = "Stable (max |λ| = " + FormatMagnitude(combinedStability.MaxMagnitude) + ")";
		Complex[] array = MathDsp.DiscreteStateSpaceFrequencyResponse(stateSpaceSystem.A, stateSpaceSystem.B, stateSpaceSystem.C, stateSpaceSystem.D, _sampleRate, _bodeFrequenciesHz);
		double[] array2 = new double[array.Length];
		double[] array3 = new double[array.Length];
		for (int k = 0; k < array.Length; k++)
		{
			array2[k] = MathDsp.ToDecibels(array[k].Magnitude);
			array3[k] = array[k].Phase * 180.0 / Math.PI;
		}
		list.Add(new PlotSeries("Combined (unity sum)", _bodeFrequenciesLog10, array2, "#9FA8DA"));
		System.Windows.Media.Color item3 = PlotFactory.MediaColorFromHex("#9FA8DA");
		Complex[] discreteZeros = _transformsService.GetDiscreteZeros(stateSpaceSystem);
		foreach (Complex item4 in discreteZeros)
		{
			list2.Add((item4, false, item3));
		}
		Complex[] poles = combinedStability.Poles;
		foreach (Complex item5 in poles)
		{
			list2.Add((item5, true, item3));
		}
		string text = ValidateCombinedResponse(stateSpaceSystem, array2, array3);
		if (!string.IsNullOrEmpty(text))
		{
			CombinedStatus = CombinedStatus + " | " + text;
		}
		return (Bode: list, PoleZero: list2);
	}

	private string ValidateCombinedResponse(StateSpaceSystem combinedState, double[] magnitudeDb, double[] phaseDeg)
	{
		try
		{
			var (array, array2) = _transformsService.GetDiscreteTransferFunction(combinedState);
			if (array.Length == 0 || array2.Length == 0)
			{
				return string.Empty;
			}
			IReadOnlyList<int> validationIndices = GetValidationIndices(10);
			double num = 0.0;
			double num2 = 0.0;
			foreach (int item in validationIndices)
			{
				Complex value = Complex.Exp(Complex.ImaginaryOne * 2.0 * Math.PI * _bodeFrequenciesHz[item] / _sampleRate);
				Complex complex = EvaluatePolynomial(array, value);
				Complex complex2 = EvaluatePolynomial(array2, value);
				if (!(complex2 == Complex.Zero))
				{
					Complex complex3 = complex / complex2;
					double num3 = MathDsp.ToDecibels(complex3.Magnitude);
					double num4 = complex3.Phase * 180.0 / Math.PI;
					num = Math.Max(num, Math.Abs(magnitudeDb[item] - num3));
					double value2 = NormalizePhase(num4 - phaseDeg[item]);
					num2 = Math.Max(num2, Math.Abs(value2));
				}
			}
			return (num > 0.5 || num2 > 5.0) ? $"Response check: Δ|H|={num:0.00} dB, Δ∠H={num2:0.0}°" : string.Empty;
		}
		catch
		{
			return "Combined response check failed.";
		}
	}

	private static string FormatMagnitude(double value)
	{
		if (double.IsFinite(value))
		{
			return value.ToString("F3", CultureInfo.InvariantCulture);
		}
		return "∞";
	}

	private IReadOnlyList<int> GetValidationIndices(int count)
	{
		if (_bodeFrequenciesHz.Length == 0)
		{
			return Array.Empty<int>();
		}
		if (count <= 1 || _bodeFrequenciesHz.Length == 1)
		{
			return new int[1];
		}
		count = Math.Min(count, _bodeFrequenciesHz.Length);
		int[] array = new int[count];
		double num = (double)(_bodeFrequenciesHz.Length - 1) / (double)(count - 1);
		for (int i = 0; i < count; i++)
		{
			array[i] = (int)Math.Round((double)i * num);
		}
		return array;
	}

	private static Complex EvaluatePolynomial(IReadOnlyList<double> coefficients, Complex value)
	{
		Complex complex = Complex.Zero;
		for (int i = 0; i < coefficients.Count; i++)
		{
			complex = complex * value + coefficients[i];
		}
		return complex;
	}

	private static double NormalizePhase(double degrees)
	{
		double num = degrees % 360.0;
		if (num > 180.0)
		{
			num -= 360.0;
		}
		else if (num < -180.0)
		{
			num += 360.0;
		}
		return num;
	}

	private void OnParamsChanged(object? sender, ParamsChangedEventArgs args)
	{
		if (!_dispatcher.CheckAccess())
		{
			_dispatcher.Invoke((Action)delegate
			{
				OnParamsChanged(sender, args);
			});
		}
		else
		{
			QueueTransferRefresh();
			UpdatePresetBannerText();
		}
	}

	private void OnPresetChanged(object? sender, EventArgs e)
	{
		if (!_dispatcher.CheckAccess())
		{
			_dispatcher.Invoke((Action)(() => OnPresetChanged(sender, e)));
			return;
		}

		_isUpdatingPresetFromStore = true;
		SelectedPreset = _parametersStore.CurrentPreset;
		_isUpdatingPresetFromStore = false;
		UpdatePresetBannerText();
		QueueTransferRefresh();
	}

	private void OnPresetBannerTimerTick(object? sender, EventArgs e)
	{
		UpdatePresetBannerText();
	}

    private void UpdatePresetBannerText()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(UpdatePresetBannerText);
            return;
        }

        var culture = CultureInfo.InvariantCulture;
        string presetName = GetPresetDisplayName(_parametersStore.CurrentPreset);
        ActivePresetLabel = string.IsNullOrEmpty(presetName) ? "Preset: -" : $"Preset: {presetName}";
        var imu = _parametersStore.GetOrCreate<ImuParams>(SensorType.Imu, DefaultsFactory.CreateImuParams);
        var fsr = _parametersStore.GetOrCreate<FsrParams>(SensorType.Fsr, DefaultsFactory.CreateFsrParams);
        var strain = _parametersStore.GetOrCreate<StrainParams>(SensorType.Strain, DefaultsFactory.CreateStrainParams);
        var emg = _parametersStore.GetOrCreate<EmgParams>(SensorType.Emg, DefaultsFactory.CreateEmgParams);

        double imuTheta = imu.AmplitudeDeg + imu.OffsetDeg;
        double fsrForce = Math.Max(fsr.ForceOffset + fsr.ForceAmplitude, 1e-6);
        double fsrA = Math.Max(fsr.FsrA, 1e-6);
        double fsrB = Math.Max(fsr.FsrB, 1e-6);
        double fsrRmin = Math.Max(fsr.FsrRmin, 0.0);
        double fsrResistance = 1.0 / (fsrA * Math.Pow(fsrForce, fsrB)) + fsrRmin;
        double fsrVoltage = 3.3 * 10_000.0 / (10_000.0 + fsrResistance);
        double strainMicro = strain.EpsilonOffsetMicro + strain.EpsilonAmplitudeMicro;
        double strainVoltage = 0.25 * strain.ExcitationVoltage * strain.GaugeFactor * strainMicro * 1e-6;
        double emgValue = emg.Amplitude * emg.ActivationLevel;

        string banner = string.Format(
            culture,
            "{0}: θ {1:0.0}° | FSR {2:0.00} V | Strain {3:0.000} V | EMG {4:0.00}",
            presetName,
            imuTheta,
            fsrVoltage,
            strainVoltage,
            emgValue);

        PresetBannerText = banner;
    }

	private string GetPresetDisplayName(RunPreset preset)
	{
		var option = PresetOptions.FirstOrDefault(o => o.Preset == preset);
		return option?.DisplayName ?? preset.ToString();
	}

	private static double[] GenerateFrequencies(double startHz, double stopHz, int count)
	{
		double[] array = new double[count];
		double num = Math.Log10(Math.Max(startHz, 0.001));
		double num2 = Math.Log10(Math.Max(stopHz, startHz + 1.0));
		for (int i = 0; i < count; i++)
		{
			double num3 = (double)i / (double)(count - 1);
			array[i] = Math.Pow(10.0, num + num3 * (num2 - num));
		}
		return array;
	}

    private string EvaluateStatus()
    {
        List<string> list = new List<string>();
        var imu = _parametersStore.GetOrCreate<ImuParams>(SensorType.Imu, DefaultsFactory.CreateImuParams);
        var fsr = _parametersStore.GetOrCreate<FsrParams>(SensorType.Fsr, DefaultsFactory.CreateFsrParams);
        var strain = _parametersStore.GetOrCreate<StrainParams>(SensorType.Strain, DefaultsFactory.CreateStrainParams);
        var emg = _parametersStore.GetOrCreate<EmgParams>(SensorType.Emg, DefaultsFactory.CreateEmgParams);

        CheckClamp(list, imu.AmplitudeDeg, 0.0, 180.0, "IMU amplitude at limit");
        CheckClamp(list, imu.OffsetDeg, -180.0, 180.0, "IMU offset at limit");

        CheckClamp(list, fsr.ForceAmplitude, 0.0, 200.0, "FSR amplitude at limit");
        CheckClamp(list, fsr.ForceOffset, 0.0, 200.0, "FSR offset at limit");
        CheckClamp(list, fsr.FsrA, 0.01, 2.0, "FSR model a at limit");
        CheckClamp(list, fsr.FsrB, 0.1, 2.5, "FSR model b at limit");
        CheckClamp(list, fsr.FsrRmin, 10.0, 5000.0, "FSR Rmin at limit");

        CheckClamp(list, strain.EpsilonOffsetMicro, 0.0, 1000.0, "Strain offset at limit");
        CheckClamp(list, strain.EpsilonAmplitudeMicro, 0.0, 1000.0, "Strain amplitude at limit");
        CheckClamp(list, strain.GaugeFactor, 1.0, 4.0, "Strain gauge factor at limit");
        CheckClamp(list, strain.ExcitationVoltage, 1.0, 10.0, "Strain excitation at limit");

        CheckClamp(list, emg.Amplitude, 0.0, 5.0, "EMG amplitude at limit");
        CheckClamp(list, emg.ActivationLevel, 0.0, 1.0, "EMG activation at limit");

        return (list.Count == 0) ? string.Empty : string.Join(" | ", list);
    }

	private static void CheckClamp(ICollection<string> warnings, double value, double min, double max, string message)
	{
		double num = Math.Max((max - min) * 0.001, 0.0001);
		if (value <= min + num || value >= max - num)
		{
			warnings.Add(message);
		}
	}
}

