//using medico_backend.Model;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace medico_backend.Services
{
    public class TokenService
    {
        private readonly IConfiguration _config;

        public TokenService(IConfiguration config)
        {
            _config = config;
        }

        //public string GenerateTenantToken(Tenant tenant)
        //{
        //    var jwtSettings = _config.GetSection("JwtSettings");
        //    var secretKey = jwtSettings["SecretKey"]
        //                    ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

        //    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        //    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        //    var claims = new[]
        //    {
        //        new Claim(JwtRegisteredClaimNames.Sub,   tenant.contact_email),
        //        new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        //        new Claim("tenant_id",                   tenant.tenant_id.ToString()),
        //        new Claim("tenant_code",                 tenant.tenant_code),
        //        new Claim("tenant_name",                 tenant.tenant_name),
        //        new Claim("role",                        "Tenant")
        //    };

        //    var token = new JwtSecurityToken(
        //        issuer: jwtSettings["Issuer"],
        //        audience: jwtSettings["Audience"],
        //        claims: claims,
        //        expires: DateTime.UtcNow.AddMinutes(
        //                                Convert.ToDouble(jwtSettings["TokenValidityInMinutes"] ?? "60")),
        //        signingCredentials: creds
        //    );

        //    return new JwtSecurityTokenHandler().WriteToken(token);
        //}

        //public string GenerateUserToken(user_master user)
        //{
        //    var jwtSettings = _config.GetSection("JwtSettings");
        //    var secretKey = jwtSettings["SecretKey"]
        //                    ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

        //    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        //    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        //    var claims = new[]
        //    {
        //        new Claim(JwtRegisteredClaimNames.Sub, user.name),
        //        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        //        new Claim("usercode",                  user.usercode.ToString()),
        //        new Claim("bhcode",                    user.bhcode.ToString()),
        //        new Claim("cntcode",                   user.cntcode.ToString()),
        //        new Claim("tenant_code",               user.tenant_code),
        //        new Claim("poweruser",                 user.poweruser.ToString()),
        //        new Claim("role",                      user.poweruser == true ? "Admin" : "User")
        //    };

        //    var token = new JwtSecurityToken(
        //        issuer: jwtSettings["Issuer"],
        //        audience: jwtSettings["Audience"],
        //        claims: claims,
        //       expires: DateTime.UtcNow.AddDays(1),
        //        signingCredentials: creds
        //    );

        //    return new JwtSecurityTokenHandler().WriteToken(token);
        //}
    }
}