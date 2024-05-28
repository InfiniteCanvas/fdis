using fdis.Utilities;

namespace fdis.Data
{
    public class AppSettings
    {
        public string   Source      { get; set; } = string.Empty;
        public string   SaveFolder  { get; set; } = Directory.GetCurrentDirectory().Combine("output");
        public string   Provider    { get; set; } = string.Empty;
        public string[] Consumers   { get; set; } = [];
        public int      Threads     { get; set; } = 1;
        public string[] Middlewares { get; set; } = [];
    }
}
