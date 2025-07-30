using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Data;

namespace ProductService.Infrastructure.Repositories
{
    public class DepartmentRepository : IDepartmentRepository
    {
        private readonly ProductDbContext _context;

        public DepartmentRepository(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Department?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Departments.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<IEnumerable<Department>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Departments
                .Include(p=>p.Products)
                .ToListAsync(cancellationToken);
        }

        public async Task<Department> AddAsync(Department department, CancellationToken cancellationToken = default)
        {
            await _context.Departments.AddAsync(department, cancellationToken);
            return department;
        }

        public Task UpdateAsync(Department department, CancellationToken cancellationToken = default)
        {
            _context.Entry(department).State = EntityState.Modified;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Department department, CancellationToken cancellationToken = default)
        {
            _context.Departments.Remove(department);
            return Task.CompletedTask;
        }

        public async Task<bool> ExistsByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Departments.AnyAsync(d => d.Id == id, cancellationToken);
        }
    }
}
