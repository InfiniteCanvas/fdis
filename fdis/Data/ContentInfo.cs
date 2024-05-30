using fdis.Utilities;

namespace fdis.Data
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

                    fx.Position = fx.Length / _scans * i;
                    fy.Position = fy.Length / _scans * i;
                }

                return 0;
            }
        }

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
