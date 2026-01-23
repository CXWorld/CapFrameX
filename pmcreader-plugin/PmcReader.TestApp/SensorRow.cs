using System.ComponentModel;

namespace PmcReader.TestApp
{
    public class SensorRow : INotifyPropertyChanged
    {
        private string _name;
        private string _value;
        private string _unit;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;

                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Value
        {
            get => _value;
            set
            {
                if (_value == value)
                    return;

                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        public string Unit
        {
            get => _unit;
            set
            {
                if (_unit == value)
                    return;

                _unit = value;
                OnPropertyChanged(nameof(Unit));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
