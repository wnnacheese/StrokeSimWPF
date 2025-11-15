using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SPS.App.Views.Components
{
    public partial class ParameterRow : UserControl
    {
        private static readonly CultureInfo Culture = new("id-ID");

        public ParameterRow()
        {
            InitializeComponent();
        }

        #region Dependency Properties

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ParameterRow), new PropertyMetadata(string.Empty));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(ParameterRow),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(ParameterRow), new PropertyMetadata(0.0, OnBoundsChanged));

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ParameterRow), new PropertyMetadata(1.0, OnBoundsChanged));

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public static readonly DependencyProperty StepSmallProperty =
            DependencyProperty.Register(nameof(StepSmall), typeof(double), typeof(ParameterRow), new PropertyMetadata(0.1));

        public double StepSmall
        {
            get => (double)GetValue(StepSmallProperty);
            set => SetValue(StepSmallProperty, value);
        }

        public static readonly DependencyProperty StepLargeProperty =
            DependencyProperty.Register(nameof(StepLarge), typeof(double), typeof(ParameterRow), new PropertyMetadata(1.0));

        public double StepLarge
        {
            get => (double)GetValue(StepLargeProperty);
            set => SetValue(StepLargeProperty, value);
        }

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(nameof(Unit), typeof(string), typeof(ParameterRow), new PropertyMetadata(string.Empty));

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public static readonly DependencyProperty MinLabelProperty =
            DependencyProperty.Register(nameof(MinLabel), typeof(string), typeof(ParameterRow), new PropertyMetadata(string.Empty));

        public string MinLabel
        {
            get => (string)GetValue(MinLabelProperty);
            set => SetValue(MinLabelProperty, value);
        }

        #endregion

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ParameterRow row)
            {
                row.UpdateNumericBox();
            }
        }

        private static object CoerceValue(DependencyObject d, object baseValue)
        {
            var row = (ParameterRow)d;
            var value = (double)baseValue;
            return Math.Max(row.Minimum, Math.Min(row.Maximum, value));
        }

        private static void OnBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ParameterRow row)
            {
                row.CoerceValue(ValueProperty);
            }
        }

        private void UpdateNumericBox()
        {
            if (NumericTextBox != null && !NumericTextBox.IsKeyboardFocused)
            {
                NumericTextBox.Text = Value.ToString("N2", Culture);
            }
        }

        private void NumericTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(NumericTextBox.Text, NumberStyles.Any, Culture, out double result))
            {
                Value = result;
            }
            UpdateNumericBox();
        }

        private void NumericTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateNumericBox();
        }

        private void NumericTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (double.TryParse(NumericTextBox.Text, NumberStyles.Any, Culture, out double result))
                {
                    Value = result;
                }
                UpdateNumericBox();
                // Lose focus
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(NumericTextBox), null);
                Keyboard.ClearFocus();
            }
        }

    }
}
