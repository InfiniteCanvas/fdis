namespace fdis.Utilities
{
    public static class ListExtentions
    {
        public static void AddTo<T>(this T item, List<T> list) { list.Add(item); }
    }
}