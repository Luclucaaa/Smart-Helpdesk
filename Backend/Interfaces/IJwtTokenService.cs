using SmartHelpdesk.Data.Entities;

namespace SmartHelpdesk.Interfaces
{
    public interface IJwtTokenService
    {
        public Task<string> GenerateJwtToken(User user);
    }
}
