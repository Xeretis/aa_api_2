using Microsoft.EntityFrameworkCore;
using WebApi.Models;

namespace WebApi.Data;

public class TodoDbContext : DbContext
{
    public TodoDbContext(DbContextOptions<TodoDbContext> options)
        : base(options)
    {
    }

    public DbSet<Todo> Todos { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Seed some sample data
        modelBuilder.Entity<Todo>().HasData(
            new Todo
            {
                Id = 1,
                Title = "API hitelesítés tanulása",
                Description = "Tanulmányozni, hogyan használjunk API kulcsokat a header-ben",
                IsCompleted = false,
                DueDate = DateTime.Now.AddDays(3),
                CreatedAt = DateTime.Now
            },
            new Todo
            {
                Id = 2,
                Title = "Lekérdezési paraméterek demó előkészítése",
                Description = "Példák készítése lekérdezési paraméterekkel történő szűrésre",
                IsCompleted = true,
                DueDate = DateTime.Now.AddDays(1),
                CreatedAt = DateTime.Now.AddDays(-1)
            },
            new Todo
            {
                Id = 3,
                Title = "Frontend írása",
                Description = "Frontend írása a példa API-hoz",
                IsCompleted = false,
                DueDate = DateTime.Now.AddDays(2),
                CreatedAt = DateTime.Now.AddDays(-2)
            }
        );
    }
}