using IdentityService.Domain.Constants;
using IdentityService.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Departments.Commands;
using ProductService.Application.Features.Departments.Queries;

namespace ProductService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DepartmentsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DepartmentsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetAll()
        {
            var departments = await _mediator.Send(new GetAllDepartmentsQuery());
            return Ok(departments);
        }

        [HttpGet("{id}")]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<DepartmentDto>> GetById(int id)
        {
            var department = await _mediator.Send(new GetDepartmentByIdQuery(id));
            if (department == null)
                return NotFound();
            return Ok(department);
        }

        [HttpPost]
        [Permission(AllPermissions.ProductCreate)]
        public async Task<ActionResult<DepartmentDto>> Create(CreateDepartmentDto dto)
        {
            var department = await _mediator.Send(new CreateDepartment.Command(dto));
            return CreatedAtAction(nameof(GetById), new { id = department.Id }, department);
        }

        [HttpPut("{id}")]
        [Permission(AllPermissions.ProductUpdate)]
        public async Task<IActionResult> Update(int id, UpdateDepartmentDto dto)
        {
            await _mediator.Send(new UpdateDepartment.Command(id, dto));
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Permission(AllPermissions.ProductDelete)]
        public async Task<IActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteDepartment.Command(id));
            return NoContent();
        }
    }
}
