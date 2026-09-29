using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Common;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Data;
using SharedServices.Services;

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
            return await _context.Departments
                .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        }

        public async Task<(int Active, int Inactive)> CountByActivityAsync(CancellationToken cancellationToken = default)
        {
            var active = await _context.Departments.CountAsync(x => x.IsActive, cancellationToken);
            var inactive = await _context.Departments.CountAsync(x => !x.IsActive, cancellationToken);
            return (active, inactive);
        }

        public async Task<IReadOnlyList<LookupItem>> GetLookupAsync(CancellationToken cancellationToken = default)
            => await _context.Departments
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new LookupItem(x.Id, x.Name))
                .ToListAsync(cancellationToken);

        public async Task<IEnumerable<Department>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Departments
                .ToListAsync(cancellationToken);
        }


        public async Task<PagedResult<Department>> GetPagedAsync(
            int pageNumber, 
            int pageSize, 
            string search, 
            CancellationToken cancellationToken = default)
        {
            var query = _context.Departments.AsNoTracking().AsQueryable();

            IEnumerable<Department> items;
            int totalCount;

            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();

                var broadQuery = query.Where(r =>
                    EF.Functions.ILike(r.Name, $"%{search}%") ||
                    (r.DepartmentHead != null && EF.Functions.ILike(r.DepartmentHead, $"%{search}%")) ||
                    (r.Description != null && EF.Functions.ILike(r.Description, $"%{search}%"))
                );

                var allFilteredItems = await broadQuery
                    .OrderBy(n => n.Name)
                    .ToListAsync(cancellationToken);

                items = allFilteredItems.Where(r =>
                    SearchHelper.ContainsAzerbaijani(r.Name, search) ||
                    SearchHelper.ContainsAzerbaijani(r.DepartmentHead, search) ||
                    SearchHelper.ContainsAzerbaijani(r.Description, search))
                    .ToList();

                totalCount = items.Count();

                items = items
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
            else
            {
                totalCount = await query.CountAsync(cancellationToken);

                items = await query
                    .OrderBy(n => n.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);
            }
            return new PagedResult<Department>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
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

        public async Task<Dictionary<int, (int Products, int Workers)>> GetUsageAsync(IEnumerable<int> departmentIds, CancellationToken cancellationToken = default)
        {
            var ids = departmentIds.ToList();
            var rows = await _context.Products
                .Where(p => ids.Contains(p.DepartmentId))
                .GroupBy(p => p.DepartmentId)
                .Select(g => new
                {
                    g.Key,
                    Products = g.Count(),
                    // Distinct, case-insensitive, non-empty worker names.
                    Workers = g.Where(p => p.Worker != null && p.Worker != "").Select(p => p.Worker!.ToLower()).Distinct().Count()
                })
                .ToListAsync(cancellationToken);
            return rows.ToDictionary(x => x.Key, x => (x.Products, x.Workers));
        }
    }
}
