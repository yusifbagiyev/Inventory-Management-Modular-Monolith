namespace SharedServices.Exceptions
{
    /// <summary>The request conflicts with the current data, like deleting a department in use, and maps to 409.</summary>
    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }
    }
}
