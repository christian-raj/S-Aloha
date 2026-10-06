using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<LdapService>();
builder.Services.AddSingleton<TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole("Admin"))
    .AddPolicy("Manager", p => p.RequireRole("Admin", "Manager"))
    .AddPolicy("User", p => p.RequireRole("Admin", "Manager", "User"));

// Les entités EF portent leurs navigations dans les deux sens (Problem ↔
// Analyses/Actions, Action ↔ RACI). Sans IgnoreCycles, renvoyer une entité
// juste ajoutée au contexte levait une JsonException APRÈS l'enregistrement :
// la première analyse d'un problème était créée en base mais répondait 500.
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Schéma par migrations EF ; une base créée par l'ancien EnsureCreated est reprise telle quelle.
using (var scope = app.Services.CreateScope())
    DatabaseSchema.Migrate(scope.ServiceProvider.GetRequiredService<AppDbContext>(), app.Logger);

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

// Exposé aux tests d'intégration (WebApplicationFactory<Program>).
public partial class Program;
