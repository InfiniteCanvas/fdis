namespace fdis.Utilities
{
    public class TimedRateLimiter(double delay = 10)
    {
        private DateTime _lastRequest = DateTime.MinValue;

        public bool ShouldAllow()
        {
            var now = DateTime.UtcNow;
            var difference = now.Subtract(_lastRequest);

            if (difference.TotalSeconds >= delay)
            {
                _lastRequest = now;
                return true;
            }

            return false;
        }

        public async Task WaitUntilAllowed()
        {
            while (true)
            {
                var now = DateTime.UtcNow;
                var difference = now.Subtract(_lastRequest);
                if (difference.TotalSeconds >= delay)
                {
                    _lastRequest = now;
                    return;
                }

                var waitTime = delay - difference.TotalSeconds;
                await Task.Delay(TimeSpan.FromSeconds(waitTime));
            }
        }
    }
}
