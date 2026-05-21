using ADS.WindowsAuth.API.Data;
using ADS.WindowsAuth.API.Services;
using ADS.WindowsAuth.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// Portal Account — Login / Logout / Register / Profile
/// Използва ASP.NET Core Identity (PortalUser / AspNetUsers)
/// </summary>
public class AccountController : Controller
{
    private readonly UserManager<PortalUser> _userManager;
    private readonly SignInManager<PortalUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly WebAuthnService _webAuthn;
    private readonly IAdService _adService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<PortalUser> userManager,
        SignInManager<PortalUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        WebAuthnService webAuthn,
        IAdService adService,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _webAuthn = webAuthn;
        _adService = adService;
        _logger = logger;
    }

    // ──────────────────────────────────────────
    // LOGIN
    // ──────────────────────────────────────────

    /// <summary>
    /// Login страница – директно на / и /Account/Login
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [Route("/")]
    [Route("/Account/Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocal(returnUrl);

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        // Намираме потребителя по username или email
        var user = await _userManager.FindByNameAsync(model.Username)
                ?? await _userManager.FindByEmailAsync(model.Username);

        if (user == null)
        {
            _logger.LogWarning("Login неуспешен – потребител не намерен: {Username}", model.Username);
            ModelState.AddModelError(string.Empty, "Невалидни потребителско Имя или парола.");
            return View(model);
        }
        if (!user.IsActive)
        {
            _logger.LogWarning("Login неуспешен – акаунт деактивиран: {Username}", model.Username);
            ModelState.AddModelError(string.Empty, "Акаунтът е деактивиран.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user, model.Password,
            isPersistent: model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("Потребител {Username} влезе в системата", user.UserName);
            return RedirectToLocal(returnUrl);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Login неуспешен – акаунт заключен: {Username}", model.Username);
            ModelState.AddModelError(string.Empty, "Акаунтът е заключен. Опитайте след 15 минути.");
            return View(model);
        }

        _logger.LogWarning("Login неуспешен – грешна парола за: {Username}", model.Username);
        ModelState.AddModelError(string.Empty, "Невалидни потребителско Имя или парола.");
        return View(model);
    }

    // ──────────────────────────────────────────
    // FIDO2 / BIOMETRIC LOGIN (Ajax endpoints)
    // ──────────────────────────────────────────

    [HttpPost("api/account/webauthn/begin")]
    [AllowAnonymous]
    public IActionResult WebAuthnBegin([FromBody] BeginWebAuthnLoginRequest req)
    {
        var opts = _webAuthn.BeginAuthentication(req.Username);
        HttpContext.Session.SetString("webauthn_challenge_key", opts.ChallengeKey);
        return Ok(opts);
    }

    [HttpPost("api/account/webauthn/complete")]
    [AllowAnonymous]
    public async Task<IActionResult> WebAuthnComplete([FromBody] CompleteWebAuthnLoginRequest req)
    {
        var challengeKey = HttpContext.Session.GetString("webauthn_challenge_key") ?? req.ChallengeKey;
        if (string.IsNullOrEmpty(challengeKey))
            return BadRequest(new { error = "Няма активна WebAuthn сесия" });

        var (ok, username, error) = await _webAuthn.CompleteAuthenticationAsync(
            challengeKey, req.CredentialId, req.AuthenticatorData, req.ClientDataJson, req.Signature);

        if (!ok)
            return Unauthorized(new { error });

        // Намираме/създаваме Identity потребителя
        var user = await _userManager.FindByNameAsync(username);
        if (user == null)
            return Unauthorized(new { error = "Потребителят не е намерен в системата" });

        if (!user.IsActive)
            return Unauthorized(new { error = "Акаунтът е деактивиран" });

        // Sign-in чрез Identity cookie
        await _signInManager.SignInAsync(user, isPersistent: false, authenticationMethod: "FIDO2");
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        HttpContext.Session.Remove("webauthn_challenge_key");
        _logger.LogInformation("FIDO2 вход за {Username}", username);

        return Ok(new { redirectUrl = "/" });
    }

    // ──────────────────────────────────────────
    // FIDO2 REGISTER (свързва с Identity user)
    // ──────────────────────────────────────────

    [HttpPost("api/account/webauthn/register/begin")]
    [AllowAnonymous]
    public IActionResult WebAuthnRegisterBegin([FromBody] BeginWebAuthnRegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username))
            return BadRequest(new { error = "Username е задължителен" });

        var opts = _webAuthn.BeginRegistration(req.Username, req.DisplayName ?? req.Username);
        return Ok(opts);
    }

    [HttpPost("api/account/webauthn/register/complete")]
    [AllowAnonymous]
    public async Task<IActionResult> WebAuthnRegisterComplete([FromBody] CompleteWebAuthnRegisterRequest req)
    {
        var (ok, error) = await _webAuthn.CompleteRegistrationAsync(
            req.Username, req.CredentialId, req.AttestationObject, req.ClientDataJson, req.DeviceDescription);

        if (!ok)
            return BadRequest(new { error });

        return Ok(new { message = "Устройството е регистрирано успешно!" });
    }

    // ──────────────────────────────────────────
    // LOGOUT
    // ──────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("Потребителят излезе от системата");
        return RedirectToAction("Login");
    }

    [HttpGet]
    [ActionName("Logout")]
    public async Task<IActionResult> LogoutGet()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    // ──────────────────────────────────────────
    // REGISTER (само за Admin)
    // ──────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Register() => View();

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Когато AD е включен – само потребители от домейна могат да се създават
        if (_adService.IsEnabled)
        {
            var usernameToCheck = model.Username.Contains('@')
                ? model.Username.Split('@')[0]
                : model.Username.Split('\\').LastOrDefault() ?? model.Username;
            if (!_adService.UserExists(usernameToCheck))
            {
                ModelState.AddModelError(string.Empty, $"Потребителят '{model.Username}' не съществува в домейна. Използвайте sAMAccountName (напр. ahmed).");
                return View(model);
            }
        }

        var user = new PortalUser
        {
            UserName = model.Username,
            Email = model.Email,
            DisplayName = model.DisplayName,
            Role = model.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, model.Role);
            _logger.LogInformation("Създаден потребител {Username}", model.Username);
            TempData["Success"] = $"Потребителят '{model.Username}' е създаден успешно.";
            return RedirectToAction("Users");
        }

        foreach (var err in result.Errors)
            ModelState.AddModelError(string.Empty, err.Description);

        return View(model);
    }

    // ──────────────────────────────────────────
    // USER MANAGEMENT (само за Admin)
    // ──────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Users()
    {
        var users = _userManager.Users
            .OrderBy(u => u.UserName)
            .Select(u => new
            {
                u.Id, u.UserName, u.Email, u.DisplayName,
                u.Role, u.IsActive, u.LastLoginAt, u.CreatedAt,
                hasFido2 = _webAuthn.HasCredentials(u.UserName ?? "")
            }).ToList();

        ViewBag.Users = users;
        return View(users);
    }

    /// <summary>Управление на роли – списък, добавяне, изтриване</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Roles()
    {
        var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
        return View(roles);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRole(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName) || roleName.Length < 2)
        {
            TempData["Error"] = "Името на ролята трябва да е минимум 2 символа.";
            return RedirectToAction("Roles");
        }
        var name = roleName.Trim();
        if (await _roleManager.RoleExistsAsync(name))
        {
            TempData["Error"] = $"Ролята '{name}' вече съществува.";
            return RedirectToAction("Roles");
        }
        var result = await _roleManager.CreateAsync(new IdentityRole(name));
        if (result.Succeeded)
        {
            TempData["Success"] = $"Ролята '{name}' е създадена успешно.";
            _logger.LogInformation("Създадена нова роля: {Role}", name);
        }
        else
        {
            TempData["Error"] = result.Errors.FirstOrDefault()?.Description ?? "Грешка при създаване.";
        }
        return RedirectToAction("Roles");
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRole(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null) return RedirectToAction("Roles");
        if (role.Name == "Admin" || role.Name == "User")
        {
            TempData["Error"] = "Ролите Admin и User не могат да бъдат изтрити.";
            return RedirectToAction("Roles");
        }
        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Count > 0)
        {
            TempData["Error"] = $"Не можете да изтриете '{role.Name}' – има {usersInRole.Count} потребител(и) с тази роля.";
            return RedirectToAction("Roles");
        }
        var result = await _roleManager.DeleteAsync(role);
        if (result.Succeeded)
        {
            TempData["Success"] = $"Ролята '{role.Name}' е изтрита.";
            _logger.LogInformation("Изтрита роля: {Role}", role.Name);
        }
        else
        {
            TempData["Error"] = result.Errors.FirstOrDefault()?.Description ?? "Грешка при изтриване.";
        }
        return RedirectToAction("Roles");
    }

    [HttpPost("api/account/users/{id}/toggle")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);
        return Ok(new { isActive = user.IsActive });
    }

    // ──────────────────────────────────────────
    // PROFILE
    // ──────────────────────────────────────────

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");
        ViewData["Credentials"] = _webAuthn.GetCredentialsForUser(user.UserName ?? "");
        return View(user);
    }

    /// <summary>Смяна на собствена парола</summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Моля, попълнете правилно полетата за парола.";
            return RedirectToAction("Profile");
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            TempData["Success"] = "Паролата е сменена успешно.";
            _logger.LogInformation("Потребител {Username} смени паролата", user.UserName);
        }
        else
        {
            TempData["Error"] = result.Errors.FirstOrDefault()?.Description ?? "Грешка при смяна на парола.";
        }
        return RedirectToAction("Profile");
    }

    /// <summary>Обновяване на профил – имейл, пълно име</summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Моля, проверете въведените данни.";
            return RedirectToAction("Profile");
        }

        user.Email = model.Email;
        user.DisplayName = model.DisplayName ?? "";
        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] = "Профилът е обновен успешно.";
            _logger.LogInformation("Потребител {Username} обнови профила", user.UserName);
        }
        else
        {
            TempData["Error"] = result.Errors.FirstOrDefault()?.Description ?? "Грешка при обновяване.";
        }
        return RedirectToAction("Profile");
    }

    // ──────────────────────────────────────────
    // EDIT USER & RESET PASSWORD (само за Admin)
    // ──────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        ViewBag.Roles = (await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync()).Select(r => r.Name ?? "").ToList();
        var model = new EditUserViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            Email = user.Email ?? "",
            DisplayName = user.DisplayName ?? "",
            Role = user.Role,
            IsActive = user.IsActive,
            Notes = user.Notes ?? ""
        };
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null) return NotFound();

        if (!ModelState.IsValid)
            return View(model);

        user.Email = model.Email;
        user.DisplayName = model.DisplayName;
        user.Notes = model.Notes;
        user.IsActive = model.IsActive;

        var existingRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
        if (existingRole != model.Role)
        {
            if (existingRole != null)
                await _userManager.RemoveFromRoleAsync(user, existingRole);
            await _userManager.AddToRoleAsync(user, model.Role);
            user.Role = model.Role;
        }

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] = $"Потребителят '{user.UserName}' е обновен успешно.";
            return RedirectToAction("Users");
        }
        foreach (var err in result.Errors)
            ModelState.AddModelError(string.Empty, err.Description);
        model.UserName = user.UserName ?? model.UserName;
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8)
        {
            TempData["Error"] = "Паролата трябва да е минимум 8 символа.";
            return RedirectToAction("Edit", new { id });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (result.Succeeded)
        {
            TempData["Success"] = $"Паролата за '{user.UserName}' е сменена успешно.";
            _logger.LogInformation("Admin сменя парола на потребител {Username}", user.UserName);
        }
        else
        {
            TempData["Error"] = result.Errors.FirstOrDefault()?.Description ?? "Грешка при смяна.";
        }
        return RedirectToAction("Edit", new { id });
    }

    // ──────────────────────────────────────────
    // HELPERS
    // ──────────────────────────────────────────

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }
}

// ──────────────────────────────────────────
// VIEW MODELS
// ──────────────────────────────────────────

public class LoginViewModel
{
    [Required(ErrorMessage = "Потребителското Иmе е задължително")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Паролата е задължителна")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Потребителското Иmе е задължително")]
    [StringLength(64, MinimumLength = 2)]
    public string Username { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Невалиден имейл адрес")]
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Паролата е задължителна")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Паролата трябва да е между 8 и 128 символа")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string Role { get; set; } = "User";
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Текущата парола е задължителна")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Новата парола е задължителна")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Паролата трябва да е между 8 и 128 символа")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Паролите не съвпадат")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class UpdateProfileViewModel
{
    [EmailAddress(ErrorMessage = "Невалиден имейл адрес")]
    public string Email { get; set; } = string.Empty;

    [StringLength(128)]
    public string DisplayName { get; set; } = string.Empty;
}

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class ResetPasswordViewModel
{
    [Required]
    [StringLength(128, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;
    [DataType(DataType.Password)]
    [Compare("NewPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// WebAuthn request models (used by AccountController)
public class BeginWebAuthnLoginRequest { public string? Username { get; set; } }
public class CompleteWebAuthnLoginRequest
{
    public string ChallengeKey { get; set; } = string.Empty;
    public string CredentialId { get; set; } = string.Empty;
    public string AuthenticatorData { get; set; } = string.Empty;
    public string ClientDataJson { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}
public class BeginWebAuthnRegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}
public class CompleteWebAuthnRegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string CredentialId { get; set; } = string.Empty;
    public string AttestationObject { get; set; } = string.Empty;
    public string ClientDataJson { get; set; } = string.Empty;
    public string? DeviceDescription { get; set; }
}
