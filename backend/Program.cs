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

//CORS - koi bhi localhost port (Vite kabhi 5173, kabhi 5174 leta hain) allow karo
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.SetIsOriginAllowed(origin =>
                      new Uri(origin).Host is "localhost" or "127.0.0.1")
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
