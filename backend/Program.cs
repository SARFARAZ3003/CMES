using Microsoft.EntityFrameworkCore;
using CMES.Data;

var builder = WebApplication.CreateBuilder(args);

//Service add karo
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//SQL Server connect (appsettings.json -> CMES_DB).
//DbContextFactory: dashboard kai queries PARALLEL chalata hain - har query ka apna
//short-lived context chahiye (ek context thread-safe nahi hota). Isse bade DB pe
//latency = sabse dheere query, na ki sabka jod.
builder.Services.AddPooledDbContextFactory<CmesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CMES_DB")));

//Calendar range + trends ko cache karne ke liye (baar-baar heavy recompute na ho).
builder.Services.AddMemoryCache();

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
