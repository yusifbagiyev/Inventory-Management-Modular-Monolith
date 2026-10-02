namespace SharedServices.Exceptions
{
    /// <summary>The request conflicts with the current data, such as deleting a department in use. Maps to 409.</summary>
    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }
    }
}
