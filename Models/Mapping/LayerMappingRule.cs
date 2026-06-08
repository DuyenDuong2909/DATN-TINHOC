namespace AutoCADToRevitApplication.Models.Mapping
{
    /// <summary>
    /// Quy tắc tự động gán loại cấu kiện dựa trên từ khóa trong tên layer.
    /// </summary>
    public class LayerMappingRule
    {
        /// <summary>Từ khóa nhận diện trong tên layer (không phân biệt hoa/thường)</summary>
        public string Keyword { get; set; } = string.Empty;

        /// <summary>Loại cấu kiện tương ứng</summary>
        public string ElementType { get; set; } = string.Empty;

        /// <summary>Độ ưu tiên — keyword dài/cụ thể hơn sẽ có priority cao hơn</summary>
        public int Priority { get; set; }

        public LayerMappingRule() { }

        public LayerMappingRule(string keyword, string elementType, int priority = 0)
        {
            Keyword     = keyword;
            ElementType = elementType;
            Priority    = priority;
        }
    }
}
