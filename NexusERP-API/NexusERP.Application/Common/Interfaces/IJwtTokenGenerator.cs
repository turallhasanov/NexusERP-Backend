using NexusERP.Domain.Entities;

namespace NexusERP.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
