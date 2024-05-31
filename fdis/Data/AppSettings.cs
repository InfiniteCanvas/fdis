namespace fdis.Data
{
    public class AppSettings
    {
        public string             Logging     { get; set; } = "Debug";
        public int                Threads     { get; set; } = 1;
        public ComponentOptions[] Providers   { get; set; } = [];
        public ComponentOptions[] Consumers   { get; set; } = [];
        public ComponentOptions[] Middlewares { get; set; } = [];
    }

    public class ComponentOptions
    {
        public string                     Type    { get; set; }
        public Dictionary<string, string> Options { get; set; }
    }
}