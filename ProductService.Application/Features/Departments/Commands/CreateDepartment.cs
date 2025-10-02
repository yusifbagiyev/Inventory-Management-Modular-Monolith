using AutoMapper;
using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Departments.Commands
{
    public class CreateDepartment
    {
        public record Command(CreateDepartmentDto DepartmentDto) : IRequest<DepartmentDto>;

        public class CreateDepartmentCommandHandler : IRequestHandler<Command, DepartmentDto>
        {
            private readonly IDepartmentRepository _departmentRepository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IMapper _mapper;

            public CreateDepartmentCommandHandler(
                IDepartmentRepository departmentRepository,
                IUnitOfWork unitOfWork,
                IMapper mapper)
            {
                _departmentRepository = departmentRepository;
                _unitOfWork = unitOfWork;
                _mapper = mapper;
            }

            public async Task<DepartmentDto> Handle(Command request, CancellationToken cancellationToken)
            {
                var department = new Department(
                    request.DepartmentDto.Name, 
                    request.DepartmentDto.Description,
                    request.DepartmentDto.DepartmentHead,
                    request.DepartmentDto.IsActive);

                await _departmentRepository.AddAsync(department, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return _mapper.Map<DepartmentDto>(department);
            }
        }
    }
}
