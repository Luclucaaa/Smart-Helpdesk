using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SmartHelpdesk.Data.Entities;
using SmartHelpdesk.DTOs.Requests;
using SmartHelpdesk.Interfaces;

namespace SmartHelpdesk.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IJwtTokenService _tokenService;
        private readonly IMapper _mapper;
        private IValidator<UserLoginDTO> _loginValidator;
        private IValidator<UserRegistrationDTO> _registrationValidator;

        public AuthController(IMapper mapper, UserManager<User> userManager, SignInManager<User> signInManager, IJwtTokenService tokenService, IValidator<UserRegistrationDTO> registrationValidator, IValidator<UserLoginDTO> loginValidator)
        {
            _mapper = mapper;
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _registrationValidator = registrationValidator;
            _loginValidator = loginValidator;
        }

        [HttpPost("Register")]
        public async Task<IActionResult> Register(UserRegistrationDTO userRegDTO)
        {
            var validationRes = _registrationValidator.Validate(userRegDTO);
            if (!validationRes.IsValid)
                return BadRequest(validationRes);

            var user = _mapper.Map<UserRegistrationDTO, User>(userRegDTO);
            var result = await _userManager.CreateAsync(user, userRegDTO.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Customer");
                // Tạo token và trả về để frontend có thể tự động đăng nhập
                var token = await _tokenService.GenerateJwtToken(user);
                var roles = await _userManager.GetRolesAsync(user);
                return Ok(new { 
                    token = token, 
                    name = user.Name,
                    surname = user.Surname,
                    roles = roles
                });
            }

            return BadRequest(result);
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login(UserLoginDTO userLoginDTO)
        {
            var validationRes = _loginValidator.Validate(userLoginDTO);
            if (!validationRes.IsValid)
                return BadRequest(validationRes);

            var user = await _userManager.FindByEmailAsync(userLoginDTO.Email);

            if (user != null && await _userManager.CheckPasswordAsync(user, userLoginDTO.Password))
            {
                var token = await _tokenService.GenerateJwtToken(user);
                var roles = await _userManager.GetRolesAsync(user);
                return Ok(new { 
                    token = token, 
                    name = user.Name,
                    surname = user.Surname,
                    roles = roles
                });
            }
            return Unauthorized();
        }

        /// <summary>
        /// Gán role Customer cho tất cả user chưa có role (chỉ dùng 1 lần để fix dữ liệu)
        /// </summary>
        [HttpPost("FixUserRoles")]
        public async Task<IActionResult> FixUserRoles()
        {
            var users = _userManager.Users.ToList();
            int fixedCount = 0;
            
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Count == 0)
                {
                    await _userManager.AddToRoleAsync(user, "Customer");
                    fixedCount++;
                }
            }
            
            return Ok(new { message = $"Đã gán role Customer cho {fixedCount} user" });
        }

        /// <summary>
        /// Debug endpoint để kiểm tra thông tin user đang đăng nhập
        /// </summary>
        [HttpGet("Me")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var emailFromToken = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("sub")?.Value;
            
            Console.WriteLine($"DEBUG Me: Email from token = {emailFromToken}");
            Console.WriteLine($"DEBUG Me: All claims:");
            foreach (var claim in User.Claims)
            {
                Console.WriteLine($"  - {claim.Type}: {claim.Value}");
            }
            
            if (string.IsNullOrEmpty(emailFromToken))
            {
                return Unauthorized(new { error = "Token không chứa thông tin email" });
            }
            
            var user = await _userManager.FindByEmailAsync(emailFromToken);
            if (user == null)
            {
                return NotFound(new { error = $"Không tìm thấy user với email: {emailFromToken}" });
            }
            
            var roles = await _userManager.GetRolesAsync(user);
            
            return Ok(new {
                id = user.Id,
                email = user.Email,
                name = user.Name,
                surname = user.Surname,
                roles = roles
            });
        }

    }
}
