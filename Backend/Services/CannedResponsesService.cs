using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SmartHelpdesk.Common.Exceptions;
using SmartHelpdesk.Data;
using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.DTOs.Requests;
using SmartHelpdesk.DTOs.Responses;
using SmartHelpdesk.Interfaces;

namespace SmartHelpdesk.Services
{
    public class CannedResponsesService : ICannedResponsesService
    {
        private readonly SmartHelpdeskContext _context;
        private readonly IMapper _mapper;

        public CannedResponsesService(SmartHelpdeskContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<Guid> CreateCannedResponse(CreateCannedResponseDTO dto)
        {
            var cannedResponse = new CannedResponse
            {
                Id = Guid.NewGuid(),
                Title = dto.Title,
                Text = dto.Text,
                Category = dto.Category,
                CreatedByUserId = dto.CreatedBy,
                CreatedAt = DateTimeOffset.Now,
                IsActive = true
            };

            _context.CannedResponses.Add(cannedResponse);
            await _context.SaveChangesAsync();

            return cannedResponse.Id;
        }

        public async Task<List<CannedResponseDTO>> GetAllCannedResponses()
        {
            var responses = await _context.CannedResponses
                .Include(c => c.CreatedBy)
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return _mapper.Map<List<CannedResponse>, List<CannedResponseDTO>>(responses);
        }

        public async Task<List<CannedResponseDTO>> GetCannedResponsesByCategory(string category)
        {
            var responses = await _context.CannedResponses
                .Include(c => c.CreatedBy)
                .Where(c => c.IsActive && (category == null || c.Category.ToString() == category))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return _mapper.Map<List<CannedResponse>, List<CannedResponseDTO>>(responses);
        }

        public async Task UpdateCannedResponse(Guid id, CreateCannedResponseDTO dto)
        {
            var response = await _context.CannedResponses.FindAsync(id);
            if (response == null)
                throw new NotFoundException();

            response.Title = dto.Title;
            response.Text = dto.Text;
            response.Category = dto.Category;
            response.UpdatedAt = DateTimeOffset.Now;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteCannedResponse(Guid id)
        {
            var response = await _context.CannedResponses.FindAsync(id);
            if (response == null)
                throw new NotFoundException();

            response.IsActive = false;
            response.UpdatedAt = DateTimeOffset.Now;

            await _context.SaveChangesAsync();
        }
    }
}
