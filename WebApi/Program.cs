using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using WebApi.Data;
using WebApi.Models;

var builder = WebApplication.CreateBuilder(args);

var possibleDbPaths = new[]
{
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "todos.db"),
    Path.Combine(Directory.GetCurrentDirectory(), "todos.db"),
    Path.Combine(Path.GetTempPath(), "todos.db"), // Temp directory should be writable
    "/tmp/todos.db" // Linux temp directory
};

// Try to find a writable path
string dbPath = null;
StringBuilder pathLog = new StringBuilder();

foreach (var path in possibleDbPaths)
{
    pathLog.AppendLine($"Trying path: {path}");
    try
    {
        var directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            pathLog.AppendLine($"Directory {directory} doesn't exist, skipping...");
            continue;
        }

        // Check if we can write to this directory
        var testFile = Path.Combine(directory, $"test_write_{Guid.NewGuid()}.tmp");
        File.WriteAllText(testFile, "test");
        File.Delete(testFile);
        
        dbPath = path;
        pathLog.AppendLine($"Found writable path: {dbPath}");
        break;
    }
    catch (Exception ex)
    {
        pathLog.AppendLine($"Error with path {path}: {ex.Message}");
    }
}

Console.WriteLine("Path tests:");
Console.WriteLine(pathLog.ToString());

builder.Services.AddOpenApi();

builder.Services.AddDbContext<TodoDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.Logger.LogInformation(AppDomain.CurrentDomain.BaseDirectory);

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.MapOpenApi();
app.MapScalarApiReference(o => 
{
    o.WithCdnUrl("https://cdn.jsdelivr.net/npm/@scalar/api-reference");
    o.AddServer("https://aa-api.bluemin.de");
});

app.UseCors("AllowAll");
app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    
    if (path.StartsWith("/scalar") || path.StartsWith("/openapi"))
    {
        await next();
        return;
    }
    
    var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
    var validApiKey = "api-key-12345";

    if (string.IsNullOrEmpty(apiKey) || apiKey != validApiKey)
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { message = "Invalid or missing API key" });
        return;
    }

    await next();
});

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
    dbContext.Database.EnsureCreated();
}

app.MapGet("/todos", async (
        [FromQuery] string? title,
        [FromQuery] bool? isCompleted,
        [FromQuery] DateTime? dueDate,
        TodoDbContext dbContext) =>
    {
        var query = dbContext.Todos.AsQueryable();

        if (!string.IsNullOrEmpty(title))
            query = query.Where(t => t.Title.Contains(title));

        if (isCompleted.HasValue)
            query = query.Where(t => t.IsCompleted == isCompleted.Value);

        if (dueDate.HasValue)
            query = query.Where(t => t.DueDate.Date == dueDate.Value.Date);

        var todos = await query.ToListAsync();
        return Results.Ok(todos);
    })
    .WithName("GetTodos")
    .WithOpenApi()
    .Produces<List<Todo>>(200)
    .Produces(401);

app.MapPost("/todos", async ([FromBody] TodoCreateDto todoDto, TodoDbContext dbContext) =>
    {
        if (string.IsNullOrEmpty(todoDto.Title))
            return Results.BadRequest("Title is required");

        var todo = new Todo
        {
            Title = todoDto.Title,
            Description = todoDto.Description,
            IsCompleted = todoDto.IsCompleted,
            DueDate = todoDto.DueDate
        };

        dbContext.Todos.Add(todo);
        await dbContext.SaveChangesAsync();

        return Results.Created($"/todos/{todo.Id}", todo);
    })
    .WithName("CreateTodo")
    .WithOpenApi()
    .Produces<Todo>(201)
    .Produces(400)
    .Produces(401);

app.MapPatch("/todos/{id}", async (int id, [FromBody] TodoUpdateDto todoDto, TodoDbContext dbContext) =>
    {
        var todo = await dbContext.Todos.FindAsync(id);
    
        if (todo == null)
            return Results.NotFound(new { message = $"Todo with ID {id} not found" });
        
        if (!string.IsNullOrEmpty(todoDto.Title))
            todo.Title = todoDto.Title;
        
        if (todoDto.Description != null)
            todo.Description = todoDto.Description;
        
        if (todoDto.IsCompleted.HasValue)
            todo.IsCompleted = todoDto.IsCompleted.Value;
        
        if (todoDto.DueDate.HasValue)
            todo.DueDate = todoDto.DueDate.Value;
    
        await dbContext.SaveChangesAsync();
    
        return Results.Ok(todo);
    })
    .WithName("UpdateTodo")
    .WithOpenApi()
    .Produces<Todo>(200)
    .Produces(404)
    .Produces(401);

app.MapGet("/", () => 
{
    return Results.Ok(new 
    { 
        message = "Todo API Demo",
        endpoints = new[]
        {
            new { method = "GET", path = "/todos", description = "Get all todos with optional filtering" },
            new { method = "POST", path = "/todos", description = "Create a new todo" },
            new { method = "PATCH", path = "/todos/{id}", description = "Update an existing todo" }
        },
        authentication = "Required API Key in X-API-Key header"
    });
});

app.Run();