using AutoMapper;
using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Departments.Queries
{
    public record GetPagedDepartmentsQuery(
        int? pageNumber=1,
        int? pageSize=20,
        string? search=null) : IRequest<PagedResultDto<DepartmentDto>>;
    public class  GetPagedDepartmentsQueryHandler : IRequestHandler<GetPagedDepartmentsQuery, PagedResultDto<DepartmentDto>>
    {
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IMapper _mapper;
        public GetPagedDepartmentsQueryHandler(IDepartmentRepository departmentRepository ,IMapper mapper)
        {
            _departmentRepository= departmentRepository;
            _mapper= mapper;
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
                Items = _mapper.Map<IEnumerable<DepartmentDto>>(departments.Items),
                TotalCount = departments.TotalCount,
                PageNumber = departments.PageNumber,
                PageSize = departments.PageSize
            };
        }
    }
}
