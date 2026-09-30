namespace RouteService.Domain.Exceptions
{
    /// <summary>A route business rule was violated. Maps to HTTP 400.</summary>
    public class RouteException : InvalidOperationException
    {
        public RouteException(string message) : base(message)
        {
        }
    }
}
