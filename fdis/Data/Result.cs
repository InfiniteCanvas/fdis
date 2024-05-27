namespace fdis.Data
{
    public record Result
    {
        public enum ResultStatus { Error, Success }

        public string? Info;

        public Exception? Exception;

        public ResultStatus Status;

        public static Result Error(string info, Exception? exception = default)
        {
            return new Result() { Info = info, Status = ResultStatus.Error, Exception = exception };
        }

        public static Result Success(string info) { return new Result() { Info = info, Status = ResultStatus.Success }; }
    }
}
