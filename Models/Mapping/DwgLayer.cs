using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoCADToRevitApplication.Models.Mapping
{
    public class DwgLayer : INotifyPropertyChanged
    {
        private string _elementType = "Bỏ qua";
        private bool _isAutoMapped;

        public string LayerName { get; set; } = string.Empty;

        public int EntityCount { get; set; }

        public string ElementType
        {
            get => _elementType;
            set
            {
                if (_elementType == value) return;
                _elementType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsIgnored));
            }
        }

        public bool IsAutoMapped
        {
            get => _isAutoMapped;
            set
            {
                if (_isAutoMapped == value) return;
                _isAutoMapped = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MappingSource));
            }
        }

        public string MappingSource => IsAutoMapped ? "Tự động" : "Thủ công";

        public bool IsIgnored => ElementType == "Bỏ qua";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
