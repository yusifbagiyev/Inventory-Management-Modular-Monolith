using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Categories.Commands;
using ProductService.Application.Features.Categories.Queries;
using ProductService.Domain.Common;
using SharedServices.Authorization;
using SharedServices.Identity;

namespace ProductService.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CategoriesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CategoriesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAll()
        {
            var categories = await _mediator.Send(new GetAllCategoriesQuery());
            return Ok(categories);
        }


        [HttpGet("paged")]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<PagedResult<CategoryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
        {
            var categories = await _mediator.Send(new GetPagedCategoriesQuery(
                pageNumber, pageSize, search));
            return Ok(categories);
        }


        [HttpGet("{id}")]
        [Permission(AllPermissions.ProductView)]
        public async Task<ActionResult<CategoryDto>> GetById(int id)
        {
            var category = await _mediator.Send(new GetCategoryByIdQuery(id));
            if (category == null)
                return NotFound();
            return Ok(category);
        }


        [HttpPost]
        [Permission(AllPermissions.ProductCreate)]
        public async Task<ActionResult<CategoryDto>> Create(CreateCategoryDto dto)
        {
            var category = await _mediator.Send(new CreateCategory.Command(dto));
            return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
        }


        [HttpPut("{id}")]
        [Permission(AllPermissions.ProductUpdate)] 
        public async Task<IActionResult> Update(int id, UpdateCategoryDto dto)
        {
            await _mediator.Send(new UpdateCategory.Command(id, dto));
            return NoContent();
        }


        [HttpDelete("{id}")]
        [Permission(AllPermissions.ProductDelete)]
        public async Task<IActionResult> Delete(int id)
        {
            await _mediator.Send(new DeleteCategory.Command(id));
            return NoContent();
        }
    }
}