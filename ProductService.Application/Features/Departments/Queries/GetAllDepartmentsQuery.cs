using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Repositories;
using ProductService.Application.Mappings;

namespace ProductService.Application.Features.Departments.Queries
{
    public record GetAllDepartmentsQuery : IRequest<IEnumerable<DepartmentDto>>;

    public class GetAllDepartmentsQueryHandler : IRequestHandler<GetAllDepartmentsQuery, IEnumerable<DepartmentDto>>
    {
        private readonly IDepartmentRepository _departmentRepository;

        public GetAllDepartmentsQueryHandler(IDepartmentRepository departmentRepository)
        {
            _departmentRepository = departmentRepository;
        }

        public async Task<IEnumerable<DepartmentDto>> Handle(GetAllDepartmentsQuery request, CancellationToken cancellationToken)
        {
            var departments = await _departmentRepository.GetAllAsync(cancellationToken);

            return await departments.ToDtosAsync(_departmentRepository, cancellationToken);
        }
    }
}
