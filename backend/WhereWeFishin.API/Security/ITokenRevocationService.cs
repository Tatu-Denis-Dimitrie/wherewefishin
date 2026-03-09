namespace WhereWeFishin.API.Security;

public interface ITokenRevocationService
{
    void RevokeToken(string jti, DateTime expiresAtUtc);
    bool IsTokenRevoked(string jti);
}
