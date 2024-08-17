using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CapFrameX.MVVM
{
    public static class SliderValueChangedBehaviour
    {
        public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached("Command", typeof(ICommand),
            typeof(SliderValueChangedBehaviour), new PropertyMetadata(PropertyChangedCallback));

        public static void PropertyChangedCallback(DependencyObject depObj, DependencyPropertyChangedEventArgs args)
        {
            Slider slider = (Slider)depObj;
            if (slider != null)
            {
                slider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(ValueChanged);
            }
        }

        public static ICommand GetCommand(UIElement element)
        {
            return (ICommand)element.GetValue(CommandProperty);
        }

        public static void SetCommand(UIElement element, ICommand command)
        {
            element.SetValue(CommandProperty, command);
        }

        private static void ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Slider slider = (Slider)sender;
            if (slider != null)
            {
                if (slider.GetValue(CommandProperty) is ICommand command)
                {
                    command.Execute(slider.Value);
                }
            }
        }
    }
}
