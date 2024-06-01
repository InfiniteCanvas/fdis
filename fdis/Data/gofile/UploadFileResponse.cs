namespace fdis.Data.gofile
{
    public class UploadFileResponse
    {
        public Data   data   { get; set; }
        public string status { get; set; }

        public class Data
        {
            public string code         { get; set; }
            public string downloadPage { get; set; }
            public string fileId       { get; set; }
            public string fileName     { get; set; }
            public string guestToken   { get; set; }
            public string md5          { get; set; }
            public string parentFolder { get; set; }
        }
    }
}