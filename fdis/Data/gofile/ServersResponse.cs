namespace fdis.Data.gofile
{
    public class ServersResponse
    {
        public string status { get; set; }
        public Data   data   { get; set; }

        public class Data
        {
            public Server[] servers { get; set; }
        }

        public class Server
        {
            public string name { get; set; }
            public string zone { get; set; }
        }
    }
}