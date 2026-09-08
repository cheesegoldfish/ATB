using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ATB.Controls
{
    public partial class ATBFloat
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double),
            typeof(ATBFloat), new PropertyMetadata(0.0));

        public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register("MaxValue",
            typeof(double), typeof(ATBFloat), new PropertyMetadata(100.0));

        public static readonly DependencyProperty MinValueProperty = DependencyProperty.Register("MinValue",
            typeof(double), typeof(ATBFloat), new PropertyMetadata(0.0));

        public static readonly DependencyProperty IncrementProperty = DependencyProperty.Register("Increment",
            typeof(double), typeof(ATBFloat), new PropertyMetadata(0.1));

        public static readonly DependencyProperty LargeIncrementProperty = DependencyProperty.Register(
            "LargeIncrement", typeof(double), typeof(ATBFloat), new PropertyMetadata(1.0));

        public static readonly DependencyProperty TextBlockValueProperty = DependencyProperty.Register(
            "TextBlockValue", typeof(string), typeof(ATBFloat), new UIPropertyMetadata(null));

        public static readonly DependencyProperty TagValueProperty = DependencyProperty.Register(
            "TagValue", typeof(string), typeof(ATBFloat), new UIPropertyMetadata(null));

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public string TextBlockValue
        {
            get { return (string)GetValue(TextBlockValueProperty); }
            set { SetValue(TextBlockValueProperty, value); }
        }

        public string TagValue
        {
            get { return (string)GetValue(TagValueProperty); }
            set { SetValue(TagValueProperty, value); }
        }

        public double MaxValue
        {
            get { return (double)GetValue(MaxValueProperty); }
            set { SetValue(MaxValueProperty, value); }
        }

        public double MinValue
        {
            get { return (double)GetValue(MinValueProperty); }
            set { SetValue(MinValueProperty, value); }
        }

        public double Increment
        {
            get { return (double)GetValue(IncrementProperty); }
            set { SetValue(IncrementProperty, value); }
        }

        public double LargeIncrement
        {
            get { return (double)GetValue(LargeIncrementProperty); }
            set { SetValue(LargeIncrementProperty, value); }
        }

        private double _previousValue;
        private static readonly int DelayRate = SystemParameters.KeyboardDelay;
        private static readonly int RepeatSpeed = Math.Max(1, SystemParameters.KeyboardSpeed);
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private bool _isIncrementing;

        public ATBFloat()
        {
            InitializeComponent();

            TextBox.PreviewTextInput += TextBoxPreviewTextInput;
            TextBox.GotFocus += TextBoxGotFocus;
            TextBox.LostFocus += TextBoxLostFocus;
            TextBox.PreviewKeyDown += TextBoxPreviewKeyDown;

            ButtonIncrement.PreviewMouseLeftButtonDown += buttonIncrement_PreviewMouseLeftButtonDown;
            ButtonIncrement.PreviewMouseLeftButtonUp += buttonIncrement_PreviewMouseLeftButtonUp;
            ButtonDecrement.PreviewMouseLeftButtonDown += buttonDecrement_PreviewMouseLeftButtonDown;
            ButtonDecrement.PreviewMouseLeftButtonUp += buttonDecrement_PreviewMouseLeftButtonUp;

            _timer.Tick += _timer_Tick;
        }

        #region TextBox

        private static void TextBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!IsNumericInput(e.Text))
            {
                e.Handled = true;
            }
        }

        private void TextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            _previousValue = Value;
        }

        private void TextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            double newValue;
            if (double.TryParse(TextBox.Text, out newValue))
            {
                if (newValue > MaxValue)
                {
                    newValue = MaxValue;
                }
                else if (newValue < MinValue)
                {
                    newValue = MinValue;
                }
            }
            else
            {
                newValue = _previousValue;
            }
            TextBox.Text = newValue.ToString("F1");
        }

        private void TextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    IncrementValue();
                    break;

                case Key.Down:
                    DecrementValue();
                    break;

                case Key.PageUp:
                    Value = Math.Min(Value + LargeIncrement, MaxValue);
                    break;

                case Key.PageDown:
                    Value = Math.Max(Value - LargeIncrement, MinValue);
                    break;
            }
        }

        #endregion TextBox

        #region Increment/Decrement

        private void IncrementValue()
        {
            Value = Math.Min(Value + Increment, MaxValue);
        }

        private void DecrementValue()
        {
            Value = Math.Max(Value - Increment, MinValue);
        }

        private static bool IsNumericInput(string text)
        {
            return text.All(c => char.IsDigit(c) || c == '.' || c == '-');
        }

        private void buttonIncrement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ButtonIncrement.CaptureMouse();
            _timer.Interval =
                TimeSpan.FromMilliseconds(DelayRate * 250);
            _timer.Start();

            _isIncrementing = true;
        }

        private void buttonIncrement_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _timer.Stop();
            ButtonIncrement.ReleaseMouseCapture();
            IncrementValue();
        }

        private void buttonDecrement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ButtonDecrement.CaptureMouse();
            _timer.Interval =
                TimeSpan.FromMilliseconds(DelayRate * 250);
            _timer.Start();

            _isIncrementing = false;
        }

        private void buttonDecrement_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _timer.Stop();
            ButtonDecrement.ReleaseMouseCapture();
            DecrementValue();
        }

        private void _timer_Tick(object sender, EventArgs e)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(RepeatSpeed * 30);

            if (_isIncrementing)
            {
                IncrementValue();
            }
            else
            {
                DecrementValue();
            }
        }

        #endregion Increment/Decrement
    }
}