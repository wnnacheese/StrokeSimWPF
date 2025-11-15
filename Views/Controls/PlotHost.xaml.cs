using System;
using System.Windows;
using System.Windows.Controls;
using SPS.App.Services;
using SPS.App.ViewModels.Plots;

namespace SPS.App.Views.Controls;

public partial class PlotHost : UserControl
{
    private PlotViewModelBase? _viewModel;

    public PlotHost()
    {
        InitializeComponent();
        PlotTheme.Apply(PlotControl.Plot);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.DataChanged -= OnDataChanged;
        }

        _viewModel = DataContext as PlotViewModelBase;

        if (_viewModel != null)
        {
            _viewModel.DataChanged += OnDataChanged;
            Render();
        }
    }

    private void OnDataChanged(object? sender, EventArgs e) => Render();

    private void Render()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.RequestRender(PlotControl.Plot);
        PlotControl.Refresh();
    }
}
