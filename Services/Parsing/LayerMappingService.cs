using AutoCADToRevitApplication.Models.Mapping;

namespace AutoCADToRevitApplication.Services.Parsing
{
    public class LayerMappingService
    {
        private static readonly List<LayerMappingRule> DefaultRules = new()
        {
            new("GRID", "Lưới trục", 10),
            new("TRUC", "Lưới trục", 10),
            new("LUOI", "Lưới trục", 10),
            new("LƯỚI", "Lưới trục", 10),
            new("AXIS", "Lưới trục", 10),
            new("TIM", "Lưới trục", 9),
            new("TÂM", "Lưới trục", 9),
            new("NET", "Lưới trục", 8),

            new("COLUMN", "Cột", 10),
            new("COT", "Cột", 10),
            new("CỘT", "Cột", 10),
            new("COL", "Cột", 9),
        };

        public List<DwgLayer> AutoMap(List<DwgLayer> layers)
        {
            foreach (var layer in layers)
            {
                var matched = FindBestMatch(layer.LayerName);
                if (matched != null)
                {
                    layer.ElementType = matched.ElementType;
                    layer.IsAutoMapped = true;
                }
                else
                {
                    layer.ElementType = "Bỏ qua";
                    layer.IsAutoMapped = false;
                }
            }

            return layers;
        }

        private static LayerMappingRule? FindBestMatch(string layerName)
        {
            var upper = layerName.ToUpperInvariant();

            return DefaultRules
                .Where(r => upper.Contains(r.Keyword.ToUpperInvariant()))
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(r => r.Keyword.Length)
                .FirstOrDefault();
        }

        public static List<LayerMappingRule> GetDefaultRules() => DefaultRules;
    }
}
