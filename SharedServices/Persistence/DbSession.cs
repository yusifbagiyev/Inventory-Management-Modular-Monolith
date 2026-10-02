using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SharedServices.Background;

namespace SharedServices.Persistence
{
    /// <summary>One connection per scope shared by every module's DbContext, so one transaction can span modules.</summary>
    public sealed class DbSession : IAsyncDisposable
    {
        private readonly BackgroundWorkQueue _backgroundQueue;
        private readonly List<DbContext> _enlisted = new();
        private readonly List<Func<Task>> _onRollback = new();
        private readonly List<BackgroundWorkItem> _afterCommit = new();
        private readonly Dictionary<string, (int AfterCommit, int OnRollback, int Enlisted)> _savepoints = new();

        public DbSession(IConfiguration configuration, BackgroundWorkQueue backgroundQueue)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured");
            Connection = new NpgsqlConnection(connectionString);
            _backgroundQueue = backgroundQueue;
        }

        public NpgsqlConnection Connection { get; }

        public NpgsqlTransaction? Transaction { get; private set; }

        public bool InTransaction => Transaction != null;

        public async Task BeginAsync(CancellationToken cancellationToken = default)
        {
            if (Transaction != null)
                throw new InvalidOperationException("A transaction is already active on this session");

            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync(cancellationToken);

            Transaction = await Connection.BeginTransactionAsync(cancellationToken);
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            var transaction = Transaction ?? throw new InvalidOperationException("No active transaction");
            await transaction.CommitAsync(cancellationToken);
            await EndAsync();

            foreach (var work in _afterCommit)
                _backgroundQueue.Enqueue(work);
            _afterCommit.Clear();
            _onRollback.Clear();
        }

        public async Task RollbackAsync()
        {
            var transaction = Transaction;
            if (transaction == null) return;

            try
            {
                await transaction.RollbackAsync();
            }
            finally
            {
                await EndAsync();
                _afterCommit.Clear();

                // Undo side effects outside the database, such as uploaded files.
                foreach (var compensate in _onRollback)
                {
                    try { await compensate(); } catch { /* Best effort */ }
                }
                _onRollback.Clear();
            }
        }

        /// <summary>Marks a point in the active transaction that a failing sub-step can roll back to.</summary>
        public async Task SavepointAsync(string name, CancellationToken cancellationToken = default)
        {
            var transaction = Transaction ?? throw new InvalidOperationException("No active transaction");
            await transaction.SaveAsync(name, cancellationToken);
            _savepoints[name] = (_afterCommit.Count, _onRollback.Count, _enlisted.Count);
        }

        // Only contexts that first saved after the savepoint are reset. A context used on both sides keeps stale tracked state.
        /// <summary>Undoes the work since the savepoint, drops its after-commit work and runs its compensations.</summary>
        public async Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
        {
            var transaction = Transaction ?? throw new InvalidOperationException("No active transaction");
            await transaction.RollbackAsync(name, cancellationToken);

            if (!_savepoints.TryGetValue(name, out var marks)) return;

            _afterCommit.RemoveRange(marks.AfterCommit, _afterCommit.Count - marks.AfterCommit);

            // Their tracked rows no longer exist. Clearing them stops a later SaveChanges from bringing them back.
            foreach (var context in _enlisted.Skip(marks.Enlisted))
                context.ChangeTracker.Clear();

            var compensations = _onRollback.Skip(marks.OnRollback).ToList();
            _onRollback.RemoveRange(marks.OnRollback, compensations.Count);
            foreach (var compensate in compensations)
            {
                try { await compensate(); } catch { /* Best effort */ }
            }
        }

        /// <summary>Queues work for after the commit, or right away outside a transaction. A rollback discards it.</summary>
        public void AfterCommit(BackgroundWorkItem work)
        {
            if (Transaction == null)
                _backgroundQueue.Enqueue(work);
            else
                _afterCommit.Add(work);
        }

        /// <summary>Registers a compensation to run if the current transaction rolls back.</summary>
        public void OnRollback(Func<Task> compensate)
        {
            if (Transaction != null)
                _onRollback.Add(compensate);
        }

        internal void Enlist(DbContext context)
        {
            if (Transaction == null || context.Database.CurrentTransaction != null) return;
            context.Database.UseTransaction(Transaction);
            _enlisted.Add(context);
        }

        private async Task EndAsync()
        {
            // Otherwise a later SaveChanges in the same scope would try to reuse the finished transaction.
            foreach (var context in _enlisted)
            {
                try { context.Database.UseTransaction(null); } catch (ObjectDisposedException) { }
            }
            _enlisted.Clear();
            _savepoints.Clear();

            if (Transaction != null)
            {
                await Transaction.DisposeAsync();
                Transaction = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Transaction != null)
                await RollbackAsync();
            await Connection.DisposeAsync();
        }
    }
}
