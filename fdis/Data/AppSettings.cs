namespace fdis.Data
{
    public class AppSettings
    {
        public string             Source      { get; set; } = string.Empty;
        public ComponentOptions   Provider    { get; set; } = null;
        public ComponentOptions[] Consumers   { get; set; } = [];
        public int                Threads     { get; set; } = 1;
        public ComponentOptions[] Middlewares { get; set; } = [];
    }

    public class ComponentOptions
    {
        public string                     Type    { get; set; }
        public Dictionary<string, string> Options { get; set; }
    }
}