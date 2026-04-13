using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.DTOs.Requests;
using SmartHelpdesk.DTOs.Responses;

namespace SmartHelpdesk.Interfaces
{
    public interface ICannedResponsesService
    {
        public Task<Guid> CreateCannedResponse(CreateCannedResponseDTO dto);
        public Task<List<CannedResponseDTO>> GetAllCannedResponses();
        public Task<List<CannedResponseDTO>> GetCannedResponsesByCategory(string category);
        public Task UpdateCannedResponse(Guid id, CreateCannedResponseDTO dto);
        public Task DeleteCannedResponse(Guid id);
    }
}
