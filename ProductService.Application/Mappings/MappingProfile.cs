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
                    !string.IsNullOrEmpty(src.ImageUrl) ? $"{src.ImageUrl}" : null));

            // Category mappings
            CreateMap<Category, CategoryDto>()
                .ForMember(dest => dest.ProductCount,
                 opt => opt.MapFrom(src => src.Products.Count));

            CreateMap<CreateCategoryDto, Category>()
                .ConstructUsing(src => new Category(src.Name, src.Description,src.IsActive));

            // Department mappings
            CreateMap<Department, DepartmentDto>()
                .ForMember(dest => dest.ProductCount,
                opt => opt.MapFrom(src => src.Products.Count))
                .ForMember(dest => dest.WorkerCount,
                opt => opt.MapFrom(src => src.WorkerCount));

            CreateMap<CreateDepartmentDto, Department>()
                .ConstructUsing(src => new Department(src.Name, src.DepartmentHead, src.Description,src.IsActive));
        }
    }
}
