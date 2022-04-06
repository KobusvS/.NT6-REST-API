using System.Net.Mime;
using Catalog.Repositories;
using Catalog.Settings;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options => 
{
    options.SuppressAsyncSuffixInActionNames = false;
});
builder.Services.AddSingleton<IItemsRepository, MongoDbItemsRepository>();
BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String));
BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer(BsonType.String));
var mongoDbSettings = builder.Configuration.GetSection(nameof(MongoDbSettings)).Get<MongoDbSettings>();

builder.Services.AddSingleton<IMongoClient>(serviceProvider => 
{
    return new MongoClient(mongoDbSettings.ConnectionString);
});

builder.Services.AddHealthChecks()
    .AddMongoDb(
        mongoDbSettings.ConnectionString, 
        name: "mongo", 
        timeout: TimeSpan.FromSeconds(3),
        tags: new[]{"ready"});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.UseEndpoints(endpoint =>
{
    endpoint.MapControllers();

    endpoint.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions{
        Predicate = (check) => check.Tags.Contains("ready"),
        ResponseWriter = async(context, report) =>
        {
            var result = System.Text.Json.JsonSerializer.Serialize(
                new{
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        exeption = e.Value.Exception != null ? e.Value.Exception.Message : "none",
                        duration = e.Value.Duration.ToString()
                    })
                }
            );
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsync(result);
        }
    });

    endpoint.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions{
        Predicate = (_) => false
    });

});


app.MapControllers();

app.Run();
