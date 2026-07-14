using medico_backend.Class;
using medico_backend.Model;
using medico_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace medico_backend.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UserClass _userClass;
        private readonly TokenService _tokenService;
        private readonly S3ImageService _s3Service;

        private const string EntityType = "users"; // S3 folder name

        public UserController(UserClass userClass, TokenService tokenService, S3ImageService s3Service)
        {
            _userClass = userClass;
            _tokenService = tokenService;
            _s3Service = s3Service;
        }

        // ─── Get User ─────────────────────────────────────────────────────────
        // tenant_code is now a manual query parameter — NOT read from a header.
        // GET /api/User/get?user_code=5&tenant_code=0004
        [HttpGet("get")]
        public async Task<IActionResult> GetUser([FromQuery] int user_code, [FromQuery] string tenant_code)
        {
            try
            {
                if (string.IsNullOrEmpty(tenant_code))
                    return BadRequest(new { message = "tenant_code is required." });

                if (user_code <= 0)
                    return BadRequest(new { message = "Invalid user_code." });

                var res = await _userClass.View_profile(user_code, tenant_code);
                if (res == null)
                    return NotFound(new { message = "User not found" });

                return Ok(res);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─── Register (self-registration, no auth, unverified) ───────────────
        // Content-Type: multipart/form-data
        // Bind Profile.User.*, Profile.Branches[i].*, Profile.Departments[i].*
        // tenant_code is now its own top-level form field — manual entry,
        // NOT read from a "tenant_code" request header.
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromForm] UserProfileFormModel profile,
            [FromForm] string tenant_code,
            [FromForm] IFormFile? userImageFile,
            [FromForm] IFormFile? signatureImageFile)
        {
            try
            {
                if (string.IsNullOrEmpty(tenant_code))
                    return BadRequest(new { message = "tenant_code is required." });

                var model = profile.User;
                model.power_user = false;
                model.tenant_code = tenant_code;

                // Step 1: Create user (unverified)
                var result = await _userClass.CreateUser(model, profile.Branches, profile.Departments);
                if (result != "User Created")
                    return MapInsertError(result);

                // Step 2: Guard — user_code must have been set by RETURNING clause
                if (model.user_code <= 0)
                    return StatusCode(500, new { message = "User created but user_code not returned." });

                // Step 3: Upload images and update ONLY image columns (safe — no null overwrite)
                await UploadAndSaveImages(model, tenant_code, userImageFile, signatureImageFile);

                return Ok(new
                {
                    message = "Registration successful. Please wait for admin verification.",
                    user_code = model.user_code
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─── Insert (admin creates user, auto-verified) ───────────────────────
        // Content-Type: multipart/form-data
        // tenant_code is manual — passed as its own form field.
        [HttpPost("insert")]
        public async Task<IActionResult> Insert(
            [FromForm] UserProfileFormModel profile,
            [FromForm] string tenant_code,
            [FromForm] IFormFile? userImageFile,
            [FromForm] IFormFile? signatureImageFile)
        {
            try
            {
                if (string.IsNullOrEmpty(tenant_code))
                    return BadRequest(new { message = "tenant_code is required." });

                var model = profile.User;
                model.power_user = false;
                model.tenant_code = tenant_code;

                // Step 1: Create user (verified)
                var result = await _userClass.InsertUser(model, profile.Branches, profile.Departments);
                if (result != "User Created")
                    return MapInsertError(result);

                // Step 2: Guard — user_code must have been set by RETURNING clause
                if (model.user_code <= 0)
                    return StatusCode(500, new { message = "User created but user_code not returned." });

                // Step 3: Upload images and update ONLY image columns (safe — no null overwrite)
                await UploadAndSaveImages(model, tenant_code, userImageFile, signatureImageFile);

                return Ok(new
                {
                    message = "User created successfully.",
                    user_code = model.user_code
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─── Update User ──────────────────────────────────────────────────────
        // Content-Type: multipart/form-data
        // tenant_code is manual — passed as its own form field.
        [HttpPost("update")]
        public async Task<IActionResult> UpdateUser(
            [FromForm] UserProfileFormModel profile,
            [FromForm] string tenant_code,
            [FromForm] IFormFile? userImageFile,
            [FromForm] IFormFile? signatureImageFile)
        {
            try
            {
                if (string.IsNullOrEmpty(tenant_code))
                    return BadRequest(new { message = "tenant_code is required." });

                var user = profile.User;
                if (user.user_code <= 0)
                    return BadRequest(new { message = "Valid user_code is required" });

                user.tenant_code = tenant_code;

                // Step 1: Fetch existing user for old image keys
                var existing = await _userClass.View_profile(user.user_code, tenant_code);
                if (existing == null)
                    return NotFound(new { message = "User not found" });

                // Step 2: Replace or preserve images
                user.user_image = await _s3Service.ReplaceAsync(
                    userImageFile,
                    existing.user_image,
                    tenant_code, EntityType, user.user_code, "avatar");

                user.signature_image = await _s3Service.ReplaceAsync(
                    signatureImageFile,
                    existing.signature_image,
                    tenant_code, EntityType, user.user_code, "signature");

                // Step 3: Full profile update (null-safe merge happens inside Update_profile)
                var res = await _userClass.Update_profile(user, profile.Branches, profile.Departments);

                return res == "success"
                    ? Ok(new { message = "Updated successfully" })
                    : BadRequest(new { message = res });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─── Soft Delete ──────────────────────────────────────────────────────
        // tenant_code is manual — GET query parameter.
        [HttpGet("softdelete")]
        public async Task<IActionResult> DeleteUser([FromQuery] int user_code, [FromQuery] string tenant_code)
        {
            try
            {
                if (string.IsNullOrEmpty(tenant_code))
                    return BadRequest(new { message = "tenant_code is required." });

                if (user_code <= 0)
                    return BadRequest(new { message = "Valid user_code is required" });

                // Step 1: Get image keys before deleting
                var existing = await _userClass.View_profile(user_code, tenant_code);

                // Step 2: Soft delete in DB
                var res = await _userClass.Delete_profile(user_code, tenant_code);
                if (res != "success")
                    return BadRequest(new { message = res });

                // Step 3: Clean up S3 images
                if (existing != null)
                {
                    await _s3Service.DeleteAsync(existing.user_image);
                    await _s3Service.DeleteAsync(existing.signature_image);
                }

                return Ok(new { message = "Deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─── Permanent Delete ─────────────────────────────────────────────────
        // tenant_code is manual — GET query parameter. [Authorize] kept for
        // authentication; tenant scoping is now explicit input, not from claims.
        [HttpGet("permanentdelete")]
        [Authorize]
        public async Task<IActionResult> PermanentDelete([FromQuery] int user_code, [FromQuery] string tenant_code)
        {
            try
            {
                if (string.IsNullOrEmpty(tenant_code))
                    return BadRequest(new { message = "tenant_code is required." });

                if (user_code <= 0)
                    return BadRequest(new { message = "Valid user_code is required" });

                // Step 1: Get image keys before permanent delete
                var existing = await _userClass.View_profile(user_code, tenant_code);

                // Step 2: Permanent delete in DB
                var res = await _userClass.PermanentDelete(user_code, tenant_code);
                if (res != "success")
                    return NotFound(new { message = res });

                // Step 3: Clean up S3 images
                if (existing != null)
                {
                    await _s3Service.DeleteAsync(existing.user_image);
                    await _s3Service.DeleteAsync(existing.signature_image);
                }

                return Ok(new { message = "User permanently deleted" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─── Get All Users ────────────────────────────────────────────────────
        // tenant_code is manual — GET query parameter.
        [HttpGet("getall")]
        public async Task<IActionResult> GetallUser([FromQuery] string tenant_code)
        {
            try
            {
                if (string.IsNullOrEmpty(tenant_code))
                    return BadRequest(new { message = "tenant_code is required." });

                var res = await _userClass.GetAllUsers(tenant_code);
                return Ok(res);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─── Verify User ──────────────────────────────────────────────────────
        // tenant_code is manual — GET query parameter, NOT pulled from the JWT
        // claim anymore. [Authorize] kept for authentication only.
        [HttpGet("verify")]
        [Authorize]
        public async Task<IActionResult> VerifyUser([FromQuery] int user_code, [FromQuery] string tenant_code)
        {
            try
            {
                if (user_code <= 0)
                    return BadRequest(new { message = "Valid user_code is required." });

                if (string.IsNullOrEmpty(tenant_code))
                    return BadRequest(new { message = "tenant_code is required." });

                var result = await _userClass.VerifyUser(user_code, tenant_code);

                return result switch
                {
                    "User Verified" => Ok(new { message = result }),
                    "User already verified" => Conflict(new { message = result }),
                    "User not found" => NotFound(new { message = result }),
                    _ => StatusCode(500, new { message = "Unexpected error." })
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─── Product Roles ────────────────────────────────────────────────────
        // tenant_code is manual — GET query parameter.
        [HttpGet("roles")]
        public async Task<IActionResult> GetProductRoles([FromQuery] string tenant_code)
        {
            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest(new { message = "tenant_code is required." });

            var res = await _userClass.GetProductRoles(tenant_code);
            return Ok(res);
        }

        // ─── Login (no rights returned) ───────────────────────────────────────
        // No tenant_code needed here — LoginWithRights looks the user up by
        // input (email/mobile/username) across all tenants, unchanged from LIMS.
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest(new { message = "Request body is required." });

                if (string.IsNullOrWhiteSpace(dto.input))
                    return BadRequest(new { message = "Username, Email or Mobile is required." });

                if (string.IsNullOrWhiteSpace(dto.password))
                    return BadRequest(new { message = "Password is required." });

                var result = await _userClass.LoginWithRights(dto);

                if (!result.Success)
                {
                    return result.Message switch
                    {
                        "Not verified" => StatusCode(403, new
                        {
                            message = "Your account is not verified. Please contact your tenant admin."
                        }),
                        _ => Unauthorized(new { message = result.Message })
                    };
                }

                var user = result.User!;

                var userDto = new UserSafeDto
                {
                    usercode = user.user_code,
                    name = user.name,
                    shortname = user.short_name,
                    description = user.description,
                    email = user.email,
                    mobile = user.mobile,
                    userimage = user.user_image,
                    signatureimage = user.signature_image,
                    poweruser = user.power_user,
                    bhcode = user.bh_code,
                    cntcode = user.cnt_code,
                    tenant_code = user.tenant_code,
                    address = user.current_address,
                    role = user.role,
                    branches = result.Branches,
                    departments = result.Departments
                };

                //var token = _tokenService.GenerateUserToken(user);

                return Ok(new LoginResponse
                {
                    //token = token,
                    userdetails = userDto
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─── Private Helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Uploads avatar and/or signature to S3, then updates ONLY the two image
        /// columns in the DB via a targeted SQL statement.
        /// </summary>
        private async Task UploadAndSaveImages(
            user_master model,
            string tenant_code,
            IFormFile? userImageFile,
            IFormFile? signatureImageFile)
        {
            string? userImageKey = null;
            string? signatureImageKey = null;

            if (userImageFile != null)
                userImageKey = await _s3Service.UploadAsync(
                    userImageFile, tenant_code, EntityType, model.user_code, "avatar");

            if (signatureImageFile != null)
                signatureImageKey = await _s3Service.UploadAsync(
                    signatureImageFile, tenant_code, EntityType, model.user_code, "signature");

            // Only hit the DB if at least one image was actually uploaded
            if (userImageKey != null || signatureImageKey != null)
            {
                await _userClass.UpdateImages(
                    model.user_code,
                    tenant_code,
                    userImageKey,
                    signatureImageKey);

                model.user_image = userImageKey;
                model.signature_image = signatureImageKey;
            }
        }

        /// <summary>
        /// Maps insert/register error strings to the correct HTTP response.
        /// </summary>
        private IActionResult MapInsertError(string result) =>
        result switch
        {
            "Invalid tenant code" => BadRequest(new { message = result }),
            "Username already exists"
            or "Email already in use"
            or "Mobile already in use" => Conflict(new { message = result }),
            _ => StatusCode(500, new { message = result })
        };
    }
}