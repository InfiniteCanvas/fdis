using fdis.Utilities;

namespace fdis.Data
{
    /// <summary>
    ///     Represents content metadata.
    /// </summary>
    public record ContentInfo(string FileName, string FolderRelativeToSource, FileInfo FileInfo)
    {
        /// <summary>
        ///     Location of the file
        /// </summary>
        public string FilePath => FileInfo.FullName;

        /// <summary>
        ///     File size of file at <see cref="FilePath" />
        /// </summary>
        public long Size => FileInfo.Length;

        /// <summary>
        ///     Struct for comparing between two ContentInfo based on the content of the file. This is used to identify duplicate files.
        /// </summary>
        public readonly struct DedupeComparer(int bufferSize = 64, int scans = 5) : IComparer<ContentInfo>
        {
            private readonly long _scans = scans;

            public int Compare(ContentInfo x, ContentInfo y)
            {
                if (ReferenceEquals(x, y))
                    return 0;
                if (ReferenceEquals(null, y))
                    return 1;
                if (ReferenceEquals(null, x))
                    return -1;
                if (x.Size != y.Size)
                    return x.Size.CompareTo(y.Size);

                return CompareFileContents(x, y);
            }

            private int CompareFileContents(ContentInfo x, ContentInfo y)
            {
                using var fx = File.OpenRead(x.FilePath);
                using var fy = File.OpenRead(y.FilePath);

                Span<byte> xBuffer = new byte[bufferSize];
                Span<byte> yBuffer = new byte[bufferSize];
                for (var i = 1; i < _scans; i++)
                {
                    var xbytes = fx.Read(xBuffer);
                    var ybytes = fy.Read(yBuffer);
                    var comp = xbytes.CompareTo(ybytes);
                    if (comp != 0)
                        return comp;

                    if (!xBuffer.SequenceEqual(yBuffer))
                        return xBuffer.SequenceCompareTo(yBuffer);

                    var position = fx.Length / _scans * i;
                    fx.Position = position;
                    fy.Position = position;
                }

                return 0;
            }
        }

        /// <summary>
        ///     Struct for comparing between two ContentInfo based on their paths.
        /// </summary>
        public readonly struct PathSorter : IComparer<ContentInfo>
        {
            public int Compare(ContentInfo x, ContentInfo y)
            {
                if (ReferenceEquals(x, y))
                    return 0;
                if (ReferenceEquals(null, y))
                    return 1;
                if (ReferenceEquals(null, x))
                    return -1;
                return string.Compare(x.FolderRelativeToSource.Combine(x.FileName),
                                      y.FolderRelativeToSource.Combine(y.FileName),
                                      StringComparison.Ordinal);
            }
        }
    }
}
