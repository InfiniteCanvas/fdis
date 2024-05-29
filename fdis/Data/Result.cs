namespace fdis.Data
{
    public record Result
    {
        public enum ResultStatus { Failure, Success }

        public Exception? Exception;

        public string? Info;

        public ResultStatus Status;

        public static Result Failure(string info, Exception? exception = default)
        {
            return new Result { Info = info, Status = ResultStatus.Failure, Exception = exception };
        }

        public static Result Success(string info) { return new Result { Info = info, Status = ResultStatus.Success }; }
    }
}