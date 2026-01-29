using AutoMapper;
using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.DTOs.Requests;
using SmartHelpdesk.DTOs.Responses;

namespace SmartHelpdesk.Common.Mappings
{
    public class SmartHelpdeskProfile : Profile
    {
        public SmartHelpdeskProfile()
        {
            CreateMap<UserRegistrationDTO, User>();
            CreateMap<User, UserDTO>();

            CreateMap<CreateCommentDTO, Comment>();
            CreateMap<Comment, CommentDTO>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.Name))
                .ForMember(dest => dest.UserEmail, opt => opt.MapFrom(src => src.User.Email))
                .ForMember(dest => dest.TicketTitle, opt => opt.MapFrom(src => src.Ticket.Title))
                .ForMember(dest => dest.IsFromAgent, opt => opt.MapFrom(src => src.UserId != src.Ticket.UserId));


            CreateMap<CreateTicketDTO, Ticket>();
            CreateMap<Ticket, TicketDTO>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.Name))
                .ForMember(dest => dest.UserEmail, opt => opt.MapFrom(src => src.User.Email))
                .ForMember(dest => dest.AssignedToEmail, opt => opt.MapFrom(src => src.AssignedTo == null ? null : src.AssignedTo.Email))
                .ForMember(dest => dest.AssignedToName, opt => opt.MapFrom(src => src.AssignedTo == null ? null : src.AssignedTo.Name))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name));
            CreateMap<Ticket, TicketDetailsDTO>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User.Name))
                .ForMember(dest => dest.UserEmail, opt => opt.MapFrom(src => src.User.Email))
                .ForMember(dest => dest.AssignedToEmail, opt => opt.MapFrom(src => src.AssignedTo == null ? null : src.AssignedTo.Email))
                .ForMember(dest => dest.AssignedToName, opt => opt.MapFrom(src => src.AssignedTo == null ? null : src.AssignedTo.Name))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name));

            CreateMap<Attachment, AttachmentDTO>();
        }

    }
}
