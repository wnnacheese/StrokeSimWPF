using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SPS.App.Views.Controls;

public partial class ParameterControl : UserControl
{
    private bool _isUpdating;
    private bool _isTextEditing;
    private CancellationTokenSource? _rampToken;

    public ParameterControl()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateDisplay();
        NumericTextBox.GotKeyboardFocus += (_, _) => _isTextEditing = true;
    }

    public string FormattedValue
    {
        get => (string)GetValue(FormattedValueProperty);
        private set => SetValue(FormattedValuePropertyKey, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(ParameterControl),
            new FrameworkPropertyMetadata(
                0d,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged,
                CoerceValue));

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(double),
            typeof(ParameterControl),
            new PropertyMetadata(0d, OnRangeChanged));

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(double),
            typeof(ParameterControl),
            new PropertyMetadata(1d, OnRangeChanged));

    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(
            nameof(Step),
            typeof(double),
            typeof(ParameterControl),
            new PropertyMetadata(0.1d, OnStepChanged));

    public double SmallStep
    {
        get => (double)GetValue(SmallStepProperty);
        set => SetValue(SmallStepProperty, value);
    }

    public static readonly DependencyProperty SmallStepProperty =
        DependencyProperty.Register(
            nameof(SmallStep),
            typeof(double),
            typeof(ParameterControl),
            new PropertyMetadata(0.01d));

    public double LargeStep
    {
        get => (double)GetValue(LargeStepProperty);
        set => SetValue(LargeStepProperty, value);
    }

    public static readonly DependencyProperty LargeStepProperty =
        DependencyProperty.Register(
            nameof(LargeStep),
            typeof(double),
            typeof(ParameterControl),
            new PropertyMetadata(1d));

    public string Format
    {
        get => (string)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public static readonly DependencyProperty FormatProperty =
        DependencyProperty.Register(
            nameof(Format),
            typeof(string),
            typeof(ParameterControl),
            new PropertyMetadata("0.00", OnFormatChanged));

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(
            nameof(Unit),
            typeof(string),
            typeof(ParameterControl),
            new PropertyMetadata(string.Empty));

    private static readonly DependencyPropertyKey FormattedValuePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(FormattedValue),
            typeof(string),
            typeof(ParameterControl),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty FormattedValueProperty = FormattedValuePropertyKey.DependencyProperty;

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ParameterControl)d;
        if (control._isUpdating)
        {
            return;
        }

        control.UpdateDisplay();
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        var control = (ParameterControl)d;
        double value = baseValue is double dValue ? dValue : control.Minimum;
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            value = control.Minimum;
        }

        double min = control.Minimum;
        double max = control.Maximum;
        if (min > max)
        {
            (min, max) = (max, min);
        }

        return Math.Clamp(value, min, max);
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ParameterControl)d;
        double min = control.Minimum;
        double max = control.Maximum;

        if (min > max)
        {
            if (ReferenceEquals(e.Property, MinimumProperty))
            {
                control.SetCurrentValue(MaximumProperty, min);
                max = min;
            }
            else
            {
                control.SetCurrentValue(MinimumProperty, max);
                min = max;
            }
        }

        control.CoerceValue(ValueProperty);
        control.UpdateDisplay();
    }

    private static void OnStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ParameterControl)d;
        control.UpdateDisplay();
    }

    private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ParameterControl)d;
        control.UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (!IsLoaded || _isUpdating)
        {
            return;
        }

        _isUpdating = true;
        try
        {
            ValueSlider.Minimum = Minimum;
            ValueSlider.Maximum = Maximum;
            ValueSlider.TickFrequency = Step;
            ValueSlider.SmallChange = SmallStep;
            ValueSlider.LargeChange = LargeStep;

            if (!ValueSlider.IsMouseCaptureWithin)
            {
                ValueSlider.Value = Value;
            }

            if (!_isTextEditing)
            {
                NumericTextBox.Text = Value.ToString(Format, CultureInfo.CurrentCulture);
            }
            FormattedValue = Value.ToString(Format, CultureInfo.CurrentCulture);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void CommitText()
    {
        if (!_isTextEditing)
        {
            return;
        }

        if (double.TryParse(NumericTextBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double parsed))
        {
            _ = ApplyDeltaAsync(parsed, absolute: true);
        }
        else
        {
            NumericTextBox.Text = Value.ToString(Format, CultureInfo.CurrentCulture);
        }

        FormattedValue = NumericTextBox.Text;
        _isTextEditing = false;
    }

    private void AdjustValue(bool increase, ModifierKeys modifiers)
    {
        double delta = GetStep(modifiers);
        if (!increase)
        {
            delta = -delta;
        }

        _ = ApplyDeltaAsync(delta, absolute: false);
    }

    private async Task ApplyDeltaAsync(double amount, bool absolute)
    {
        double current = Value;
        double target = absolute ? amount : current + amount;
        target = Math.Clamp(target, Minimum, Maximum);

        double basis = LargeStep > 0 ? LargeStep : Math.Max(Step, SmallStep);
        double threshold = Math.Abs(basis * 3);

        _rampToken?.Cancel();

        if (Math.Abs(target - current) > threshold)
        {
            _rampToken = new CancellationTokenSource();
            try
            {
                await RampToValueAsync(current, target, _rampToken.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
            }
        }
        else
        {
            await Dispatcher.InvokeAsync(() => SetValueInternal(target));
        }
    }

    private double GetStep(ModifierKeys modifiers)
    {
        if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            return SmallStep > 0 ? SmallStep : Step;
        }

        if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            return LargeStep > 0 ? LargeStep : Step * 5;
        }

        return Step > 0 ? Step : SmallStep;
    }

    private void OnIncreaseClicked(object sender, RoutedEventArgs e) =>
        AdjustValue(true, Keyboard.Modifiers);

    private void OnDecreaseClicked(object sender, RoutedEventArgs e) =>
        AdjustValue(false, Keyboard.Modifiers);

    private void NumericTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        _isTextEditing = true;

        switch (e.Key)
        {
            case Key.Enter:
                CommitText();
                e.Handled = true;
                break;
            case Key.Up:
                AdjustValue(true, Keyboard.Modifiers);
                e.Handled = true;
                break;
            case Key.Down:
                AdjustValue(false, Keyboard.Modifiers);
                e.Handled = true;
                break;
            case Key.Escape:
                NumericTextBox.Text = Value.ToString(Format, CultureInfo.CurrentCulture);
                _isTextEditing = false;
                e.Handled = true;
                break;
        }
    }

    private void NumericTextBox_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        AdjustValue(e.Delta > 0, Keyboard.Modifiers);
        e.Handled = true;
    }

    private void NumericTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        CommitText();
    }

    private void SetValueInternal(double newValue)
    {
        _isUpdating = true;
        Value = newValue;
        _isUpdating = false;
        UpdateDisplay();
    }

    private async Task RampToValueAsync(double start, double end, CancellationToken token)
    {
        const double durationMs = 200;
        const int segments = 10;
        double interval = durationMs / segments;

        for (int i = 1; i <= segments; i++)
        {
            token.ThrowIfCancellationRequested();
            double t = i / (double)segments;
            double value = start + (end - start) * t;
            await Dispatcher.InvokeAsync(() => SetValueInternal(value));
            await Task.Delay(TimeSpan.FromMilliseconds(interval), token).ConfigureAwait(false);
        }
    }
}
