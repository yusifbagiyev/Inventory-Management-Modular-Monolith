namespace ProductService.Application.Interfaces
{
    public interface ITransactionService
    {
        Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Func<Task> compensate);
    }
}
