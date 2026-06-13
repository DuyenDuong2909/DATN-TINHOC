namespace AutoCADToRevitApplication.Models.Mapping
{
    public enum CreationStatus { Success, Skipped, Failed, Conflict }
    public class RevitCreationResult
    {
        public string       ElementType { get; set; } = string.Empty;
        public string       ElementName { get; set; } = string.Empty;
        public CreationStatus Status    { get; set; }
        public string       Message     { get; set; } = string.Empty;

        public static RevitCreationResult Ok(string type, string name) => new()
            { ElementType = type, ElementName = name, Status = CreationStatus.Success };

        public static RevitCreationResult Fail(string type, string name, string reason) => new()
            { ElementType = type, ElementName = name, Status = CreationStatus.Failed, Message = reason };

        public static RevitCreationResult Skip(string type, string name, string reason) => new()
            { ElementType = type, ElementName = name, Status = CreationStatus.Skipped, Message = reason };
    }
}
