namespace SharedServices.Exceptions
{
    /// <summary>
    /// The request is well-formed but conflicts with the current state of the data,
    /// e.g. deleting a department that still has products assigned to it.
    /// Maps to HTTP 409.
    /// </summary>
    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }
    }
}
