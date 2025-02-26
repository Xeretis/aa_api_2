using LiteDB;
using WebApi.Models;

namespace WebApi.Data;

public interface ITodoRepository
{
    IEnumerable<Todo> GetAll();
    Todo? GetById(int id);
    Todo Create(Todo todo);
    bool Update(Todo todo);
    bool Delete(int id);
}

public class TodoRepository : ITodoRepository
{
    private readonly ILiteDatabase _database;
    private readonly ILiteCollection<Todo> _todos;
    private readonly ILogger<TodoRepository> _logger;

    public TodoRepository(ILiteDatabase database, ILogger<TodoRepository> logger)
    {
        _database = database;
        _logger = logger;
        _todos = database.GetCollection<Todo>("todos");
        
        // Create index on Id if it doesn't exist
        _todos.EnsureIndex(x => x.Id);

        // Seed initial data if collection is empty
        if (_todos.Count() == 0)
        {
            _logger.LogInformation("Seeding initial todo data");
            SeedData();
        }
    }

    public IEnumerable<Todo> GetAll()
    {
        return _todos.FindAll();
    }

    public Todo? GetById(int id)
    {
        return _todos.FindById(id);
    }

    public Todo Create(Todo todo)
    {
        // Get the max Id and increment by 1, or start at 1 if no records exist
        var maxId = _todos.Max(x => x.Id);
        todo.Id = maxId > 0 ? maxId + 1 : 1;
        todo.CreatedAt = DateTime.Now;
        
        _todos.Insert(todo);
        return todo;
    }

    public bool Update(Todo todo)
    {
        return _todos.Update(todo);
    }

    public bool Delete(int id)
    {
        return _todos.Delete(id);
    }

    private void SeedData()
    {
        var todos = new[]
        {
            new Todo
            {
                Id = 1,
                Title = "API hitelesítés tanulása",
                Description = "Megtanulni, hogyan használjunk API kulcsokat a header-ben",
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
        };

        _todos.Insert(todos);
    }
}