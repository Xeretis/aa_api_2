using LiteDB;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using WebApi.Data;
using WebApi.Models;
using JsonSerializer = System.Text.Json.JsonSerializer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

Console.WriteLine(builder.Environment.ContentRootPath);

// Configure LiteDB
builder.Services.AddSingleton<ILiteDatabase>(serviceProvider =>
{
    var dbPath = builder.Environment.IsProduction() ? Path.Combine("/www/aa_api_2/", "Todos.db") : Path.Combine(builder.Environment.ContentRootPath, "Todos.db");
    return new LiteDatabase($"{dbPath}");
});

// Register TodoRepository
builder.Services.AddScoped<ITodoRepository, TodoRepository>();

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

// Initialize database and verify connection
using (var scope = app.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<ITodoRepository>();
    var todos = repository.GetAll().ToList();
    app.Logger.LogInformation($"Database initialized. Found {todos.Count} todos.");
    app.Logger.LogInformation(JsonSerializer.Serialize(todos.FirstOrDefault()));
}

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

app.MapGet("/todos", async (
        [FromQuery] string? title,
        [FromQuery] bool? isCompleted,
        [FromQuery] DateTime? dueDate,
        ITodoRepository repository) =>
    {
        var allTodos = repository.GetAll();

        // Apply filters
        var filteredTodos = allTodos
            .Where(t => string.IsNullOrEmpty(title) || t.Title.Contains(title))
            .Where(t => !isCompleted.HasValue || t.IsCompleted == isCompleted.Value)
            .Where(t => !dueDate.HasValue || t.DueDate.Date == dueDate.Value.Date)
            .ToList();

        return Results.Ok(filteredTodos);
    })
    .WithName("GetTodos")
    .WithOpenApi()
    .Produces<List<Todo>>(200)
    .Produces(401);

app.MapPost("/todos", async ([FromBody] TodoCreateDto todoDto, ITodoRepository repository) =>
    {
        if (string.IsNullOrEmpty(todoDto.Title))
            return Results.BadRequest("Title is required");

        var todo = new Todo
        {
            Title = todoDto.Title,
            Description = todoDto.Description,
            IsCompleted = todoDto.IsCompleted,
            DueDate = todoDto.DueDate,
            CreatedAt = DateTime.UtcNow
        };

        var created = repository.Create(todo);
        return Results.Created($"/todos/{created.Id}", created);
    })
    .WithName("CreateTodo")
    .WithOpenApi()
    .Produces<Todo>(201)
    .Produces(400)
    .Produces(401);

app.MapPatch("/todos/{id}", async (int id, [FromBody] TodoUpdateDto todoDto, ITodoRepository repository) =>
    {
        var todo = repository.GetById(id);
    
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
    
        var success = repository.Update(todo);
        if (!success)
            return Results.StatusCode(500);
    
        return Results.Ok(todo);
    })
    .WithName("UpdateTodo")
    .WithOpenApi()
    .Produces<Todo>(200)
    .Produces(404)
    .Produces(401)
    .Produces(500);

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