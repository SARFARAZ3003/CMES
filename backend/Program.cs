using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using CMES.Data;
using CMES.Services;
using CMES.Authorization;

var builder = WebApplication.CreateBuilder(args);

//Service add karo
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//---- Windows Integrated Authentication ----
//Negotiate: IIS/Kestrel se Windows identity flow karti hain (passwordless).
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();             // WWID -> CMES_USERS lookup
builder.Services.AddScoped<CycleTimeService>();               // cycle-time (sec/engine) Oracle se (ya fallback)
builder.Services.AddScoped<IAuthorizationHandler, CmesUserHandler>();

//Policy "CmesUser": current Windows user CMES_USERS mein active ho.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CmesUser", p => p.AddRequirements(new CmesUserRequirement()));
});

//SQL Server connect (appsettings.json -> CMES_DB).
//DbContextFactory: dashboard kai queries PARALLEL chalata hain - har query ka apna
//short-lived context chahiye (ek context thread-safe nahi hota). Isse bade DB pe
//latency = sabse dheere query, na ki sabka jod.
builder.Services.AddPooledDbContextFactory<CmesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CMES_DB"),
        sql => sql.CommandTimeout(180)));  // bade DB pe heavy trends query 30s pe cancel na ho

//Auth ke liye ALAG database (CMES_USERS). Sir ke setup mein ye production DB se alag hain.
builder.Services.AddPooledDbContextFactory<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AUTH_DB")));

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
                  .AllowAnyMethod()
                  .AllowCredentials(); // Windows auth ke liye browser credentials bheje
        });
});


var app = builder.Build();

// ---- Startup DB connectivity check: real DB se connect karte waqt PEHLA failure point yahin console pe pakdo ----
{
    var slog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CMES.Startup");
    try
    {
        await using var db = await app.Services.GetRequiredService<IDbContextFactory<CmesDbContext>>().CreateDbContextAsync();
        if (await db.Database.CanConnectAsync())
            slog.LogInformation("[CMES-DB-OK] CMES_DB connected -> {Db}", db.Database.GetDbConnection().Database);
        else
            slog.LogError("[CMES-DB-FAIL] CMES_DB CanConnect=false -> connection string / server / network check karo");
    }
    catch (Exception ex) { slog.LogError(ex, "[CMES-DB-FAIL] CMES_DB connect FAILED -> {Msg}", ex.Message); }

    try
    {
        await using var adb = await app.Services.GetRequiredService<IDbContextFactory<AuthDbContext>>().CreateDbContextAsync();
        if (await adb.Database.CanConnectAsync())
            slog.LogInformation("[CMES-DB-OK] AUTH_DB connected -> {Db}", adb.Database.GetDbConnection().Database);
        else
            slog.LogError("[CMES-DB-FAIL] AUTH_DB CanConnect=false -> connection string / server / network check karo");
    }
    catch (Exception ex) { slog.LogError(ex, "[CMES-DB-FAIL] AUTH_DB connect FAILED -> {Msg}", ex.Message); }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowReactApp");
app.UseAuthentication();   // Windows identity resolve
app.UseAuthorization();
app.MapControllers();
app.Run();
