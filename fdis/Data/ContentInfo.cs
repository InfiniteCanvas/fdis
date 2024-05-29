namespace fdis
{
    public record ContentInfo
    {
        /// <summary>
        ///     When consuming, save the file with this file name
        /// </summary>
        public string FileName;

        /// <summary>
        ///     Location of the file
        /// </summary>
        public string FilePath;

        /// <summary>
        ///     When consuming, consider recreating this folder structure
        /// </summary>
        public string FolderRelativeToSource;

        /// <summary>
        ///     File size of file at <see cref="FilePath" /> should be, check for errors
        /// </summary>
        public long Size;
    }
}