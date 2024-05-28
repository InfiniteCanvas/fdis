namespace fdis
{
    public record ContentInfo
    {
        /// <summary>
        /// When consuming, save the file with this file name
        /// </summary>
        public string FileName;

        /// <summary>
        /// When consuming, consider recreating this folder structure
        /// </summary>
        public string FolderRelativeToSource;

        /// <summary>
        /// Location of the file
        /// </summary>
        public string FilePath;

        /// <summary>
        /// File size of file at <see cref="FilePath"/> should be, check for errors
        /// </summary>
        public long Size;
    }
}
