using System;
using System.IO;
using System.Windows;
using SPS.App.Models;
using SPS.App.Services;
using SPS.App.ViewModels;

namespace SPS.App;

public partial class App : Application
{
    private readonly JsonStorage _jsonStorage = new();
    private readonly ParametersStore _parametersStore = new();
    private readonly string _paramsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StrokeRecovery", "params.json");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var loadedParameters = _jsonStorage.LoadOrDefault(_paramsPath);
        _parametersStore.Override(loadedParameters);

        var transformsService = new TransformsService();
        var fftService = new FftService();
        var paramsBus = new ParamsBus();

        var imu = new ImuModel();
        var fsr = new FsrModel();
        var strain = new StrainModel();
        var emg = new EmgModel();

        var engine = new SignalEngine(imu, fsr, strain, emg, _parametersStore, transformsService, paramsBus);
        _parametersStore.ApplyPreset(RunPreset.Custom);

        var mainViewModel = new MainWindowViewModel(engine, _parametersStore, _jsonStorage, transformsService, fftService, paramsBus, Dispatcher, imu, fsr, strain, emg);

        var window = new Views.MainWindow
        {
            DataContext = mainViewModel
        };

        window.Show();

        Exit += (_, _) =>
        {
            // Save parameters on exit
            _jsonStorage.Save(_paramsPath, _parametersStore.Snapshot());
            mainViewModel.Dispose();
            engine.Dispose();
        };
    }
}

