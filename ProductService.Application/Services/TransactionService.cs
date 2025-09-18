using ProductService.Application.Interfaces;

namespace ProductService.Application.Services
{
    public class TransactionService : ITransactionService
    {
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Func<Task> compensate)
        {
            try
            {
                return await operation();
            }
            catch
            {
                await compensate();
                throw;
            }
        }
    }
}