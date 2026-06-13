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

            new("BEAM", "Dầm", 10),
            new("DAM", "Dầm", 10),
            new("DẦM", "Dầm", 10),

            new("SLAB", "Sàn", 10),
            new("SAN", "Sàn", 10),
            new("SÀN", "Sàn", 10),
            new("FLOOR", "Sàn", 9),
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
            var normalizedLayerName = NormalizeText(layerName);

            return DefaultRules
                .Where(r => normalizedLayerName.Contains(NormalizeText(r.Keyword)))
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(r => r.Keyword.Length)
                .FirstOrDefault();
        }

        private static string NormalizeText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Trim().Normalize(System.Text.NormalizationForm.FormD);
            var builder = new System.Text.StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    builder.Append(ch);
            }

            return builder
                .ToString()
                .Normalize(System.Text.NormalizationForm.FormC)
                .ToUpperInvariant();
        }

        public static List<LayerMappingRule> GetDefaultRules() => DefaultRules;
    }
}
