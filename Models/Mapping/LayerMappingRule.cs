namespace AutoCADToRevitApplication.Models.Mapping
{
    public class LayerMappingRule
    {
        public string Keyword { get; set; } = string.Empty;

        public string ElementType { get; set; } = string.Empty;

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
