using TechMove.Contracts;

namespace TechMove.Models;

public sealed class ProfileViewModel
{
    public UserProfileDto Profile { get; set; } = new(string.Empty, string.Empty, false);
    public IFormFile? ProfilePicture { get; set; }
}
