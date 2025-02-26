using System.ComponentModel.DataAnnotations;

namespace WebApi.Models;

public class Todo
{
    public int Id { get; set; }
        
    [Required]
    public string Title { get; set; } = string.Empty;
        
    public string? Description { get; set; }
        
    public bool IsCompleted { get; set; }
        
    public DateTime DueDate { get; set; } = DateTime.Now.AddDays(1);
        
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class TodoCreateDto
{
    [Required]
    public string Title { get; set; } = string.Empty;
        
    public string? Description { get; set; }
        
    public bool IsCompleted { get; set; }
        
    public DateTime DueDate { get; set; } = DateTime.Now.AddDays(1);
}

public class TodoUpdateDto
{
    public string? Title { get; set; }
        
    public string? Description { get; set; }
        
    public bool? IsCompleted { get; set; }
        
    public DateTime? DueDate { get; set; }
}