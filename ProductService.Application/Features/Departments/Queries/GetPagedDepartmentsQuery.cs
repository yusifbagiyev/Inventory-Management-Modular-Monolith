using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Repositories;
using ProductService.Application.Mappings;

namespace ProductService.Application.Features.Departments.Queries
{
    public record GetPagedDepartmentsQuery(
        int? pageNumber=1,
        int? pageSize=20,
        string? search=null) : IRequest<PagedResultDto<DepartmentDto>>;
    public class  GetPagedDepartmentsQueryHandler : IRequestHandler<GetPagedDepartmentsQuery, PagedResultDto<DepartmentDto>>
    {
        private readonly IDepartmentRepository _departmentRepository;
        public GetPagedDepartmentsQueryHandler(IDepartmentRepository departmentRepository )
        {
            _departmentRepository= departmentRepository;
        }

        public async Task<PagedResultDto<DepartmentDto>> Handle(GetPagedDepartmentsQuery request, CancellationToken cancellationToken)
        {
            var departments = await _departmentRepository.GetPagedAsync(
                request.pageNumber ?? 1,
                request.pageSize ?? 20,
                request.search,
                cancellationToken);

            return new PagedResultDto<DepartmentDto>
            {
                Items = await departments.Items.ToDtosAsync(_departmentRepository, cancellationToken),
                TotalCount = departments.TotalCount,
                PageNumber = departments.PageNumber,
                PageSize = departments.PageSize
            };
        }
    }
}
