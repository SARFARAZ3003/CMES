using Microsoft.EntityFrameworkCore;
using CMES.Data;

var builder = WebApplication.CreateBuilder(args);

//Service add karo
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//SQL Server connect - localhost, CMES_DB, Windows Auth (Trusted_Connection)
//Connection string appsettings.json mein hain. Abhi controllers mock data dete hain,
//par DbContext register ho gaya - DB ready hote hi use kar sakte hain.
builder.Services.AddDbContext<CmesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CMES_DB")));

//CORS - React(port 5173) ko allow karo
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowReactApp");
app.UseAuthorization();
app.MapControllers();
app.Run();
