namespace fdis.Data
{
    public record Result
    {
        public enum ResultStatus { Error, Success }

        public string? Info;

        public ResultStatus Status;
    }
}