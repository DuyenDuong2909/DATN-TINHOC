using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoCADToRevitApplication.Models.Mapping
{
    /// <summary>
    /// Đại diện cho một layer trong file DWG,
    /// bao gồm tên layer, loại cấu kiện được gán, và số lượng entity.
    /// Implement INotifyPropertyChanged để binding với DataGrid.
    /// </summary>
    public class DwgLayer : INotifyPropertyChanged
    {
        private string _elementType = "Bỏ qua";
        private bool   _isAutoMapped;

        /// <summary>Tên layer gốc trong file DWG</summary>
        public string LayerName { get; set; } = string.Empty;

        /// <summary>Số lượng entity thuộc layer này</summary>
        public int EntityCount { get; set; }

        /// <summary>
        /// Loại cấu kiện được gán cho layer này.
        /// Có thể là: Bỏ qua | Lưới trục | Tường | Cột | Dầm | Sàn
        /// </summary>
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

        /// <summary>True = được tự động gán bởi keyword chuẩn</summary>
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

        /// <summary>Hiển thị nguồn gán: "Tự động" hoặc "Thủ công"</summary>
        public string MappingSource => IsAutoMapped ? "Tự động" : "Thủ công";

        /// <summary>True nếu layer này bị bỏ qua (không tạo cấu kiện)</summary>
        public bool IsIgnored => ElementType == "Bỏ qua";

        // ── INotifyPropertyChanged ────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
