using System.ComponentModel.DataAnnotations;

namespace AIpoweredVivaExamSystem.Web.Models;

/// <summary>Input cho form MVC; validation lỗi trả lại form trước khi gọi nghiệp vụ Login.</summary>
public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(255)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}
