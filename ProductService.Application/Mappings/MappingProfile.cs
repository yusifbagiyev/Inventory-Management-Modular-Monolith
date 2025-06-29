using AutoMapper;
using ProductService.Application.DTOs;
using ProductService.Domain.Entities;

namespace ProductService.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Product mappings
            CreateMap<Product, ProductDto>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category!.Name))
                .ForMember(dest => dest.DepartmentName, opt => opt.MapFrom(src => src.Department!.Name))
                .ForMember(dest=>dest.ImageUrl,opt=>opt.MapFrom(src=>
                    !string.IsNullOrEmpty(src.ImageUrl) ? $"/images/{src.ImageUrl}" : null));

            // Category mappings
            CreateMap<Category, CategoryDto>();
            CreateMap<CreateCategoryDto, Category>()
                .ConstructUsing(src => new Category(src.Name, src.Description));

            // Department mappings
            CreateMap<Department, DepartmentDto>();
            CreateMap<CreateDepartmentDto, Department>()
                .ConstructUsing(src => new Department(src.Name, src.Description));
        }
    }
}
