using CulinaryBlog.Domain.Common;

namespace CulinaryBlog.Domain.Entities;

public class RecipeStep : BaseEntity
{
    public Guid RecipeId { get; set; }
    public int StepNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? TimerMinutes { get; set; }
    public string? ImageUrl { get; set; }
    public Recipe? Recipe { get; set; }

    public void Update(string title, string description, int? timerMinutes, string? imageUrl)
    {
        Title = title;
        Description = description;
        TimerMinutes = timerMinutes;
        ImageUrl = imageUrl;
        UpdatedAt = DateTime.UtcNow;
    }
}