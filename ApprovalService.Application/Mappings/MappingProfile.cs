using ApprovalService.Application.DTOs;
using ApprovalService.Domain.Entities;
using AutoMapper;

namespace ApprovalService.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<ApprovalRequest, ApprovalRequestDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
        }
    }

}