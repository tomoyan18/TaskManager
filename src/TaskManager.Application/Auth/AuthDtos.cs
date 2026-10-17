using System.ComponentModel.DataAnnotations;
using TaskManager.Application.Common;
using TaskManager.Application.Users;

namespace TaskManager.Application.Auth;

public sealed class LoginRequest
{
    [Display(Name = "メールアドレス")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [StringLength(254, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "パスワード")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [StringLength(128, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string Password { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Display(Name = "現在のパスワード")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [StringLength(128, MinimumLength = 1, ErrorMessage = ValidationMessages.LengthRange)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Display(Name = "新しいパスワード")]
    [Required(ErrorMessage = ValidationMessages.Required)]
    [StringLength(128, MinimumLength = 12, ErrorMessage = ValidationMessages.LengthRange)]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>ログイン・トークン更新・パスワード変更の応答本文。</summary>
public sealed record LoginResponse(string AccessToken, int ExpiresIn, CurrentUserResponse User);

/// <summary>Api 層がリフレッシュ Cookie を設定するための結果（Cookie の値は応答本文に含めない）。</summary>
public sealed record AuthResult(LoginResponse Response, string RefreshToken, DateTime RefreshExpiresAtUtc);
