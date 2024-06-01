namespace fdis.Data.gofile
{
    public class CreateFolderResponse
    {
        public string Status { get; set; }
        public Data   Data   { get; set; }
    }

    public class Data
    {
        public string FolderId     { get; set; }
        public string Type         { get; set; }
        public string Name         { get; set; }
        public string ParentFolder { get; set; }
        public long   CreateTime   { get; set; }
        public string Code         { get; set; }
    }
}
