using AutoMapper;
using RouteService.Application.DTOs;
using RouteService.Domain.Entities;

namespace RouteService.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<InventoryRoute, InventoryRouteDto>()
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductSnapshot.ProductId))
                .ForMember(dest => dest.InventoryCode, opt => opt.MapFrom(src => src.ProductSnapshot.InventoryCode))
                .ForMember(dest => dest.Model, opt => opt.MapFrom(src => src.ProductSnapshot.Model))
                .ForMember(dest => dest.Vendor, opt => opt.MapFrom(src => src.ProductSnapshot.Vendor))
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.ProductSnapshot.CategoryName));
        }
    }
}
