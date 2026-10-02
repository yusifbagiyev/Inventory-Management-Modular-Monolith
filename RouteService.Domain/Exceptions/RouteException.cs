namespace RouteService.Domain.Exceptions
{
    /// <summary>A broken route business rule, answered with HTTP 400.</summary>
    public class RouteException : InvalidOperationException
    {
        public RouteException(string message) : base(message)
        {
        }
    }
}
