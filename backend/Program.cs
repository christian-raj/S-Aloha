using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SAloha.Api.Core.Audit;
using SAloha.Api.Core.Data;
using SAloha.Api.Core.Auth;
using SAloha.Api.Core.Observability;
using SAloha.Api.Core.Search;
using SAloha.Api.Modules.ServiceConfigurationManagement.Discovery;

var builder = WebApplication.CreateBuilder(args);

// O2 (ADR-0017) : journal JSON, traces et métriques OpenTelemetry (export OTLP si configuré).
builder.AddObservability();

// Recherche hybride (ADR-0013) : index dérivé, tenu à jour en tâche de fond à
// chaque enregistrement d'un article, d'un problème ou d'un incident.
builder.Services.AddSingleton<IndexQueue>();
builder.Services.AddSingleton<VectorStore>();
builder.Services.AddSingleton<SearchIndexInterceptor>();
builder.Services.AddHttpClient<IEmbeddingClient, OllamaEmbeddingClient>();
builder.Services.AddScoped<SearchIndexer>();
builder.Services.AddScoped<HybridSearch>();
builder.Services.AddScoped<SAloha.Api.Core.Perimeters.PerimeterAccess>();
builder.Services.AddScoped<SAloha.Api.Core.Notifications.Notifier>();
builder.Services.AddScoped<SAloha.Api.Core.Notifications.DatedEvents>();
builder.Services.AddHostedService<SAloha.Api.Core.Notifications.DatedEventsWorker>();
builder.Services.AddHostedService<IndexWorker>();

// Découverte automatique (CFG-40 à CFG-47) : collecteurs, coffre, rapprochement, file d'exécution.
builder.Services.AddSingleton<SAloha.Api.Core.Administration.Heartbeats>();
builder.Services.AddScoped<SAloha.Api.Core.Administration.Duplicates>();
builder.Services.AddSingleton<CredentialVault>();
builder.Services.AddSingleton<DiscoveryQueue>();
builder.Services.AddScoped<IDiscoveryCollector, NetworkScanner>();
builder.Services.AddScoped<IDiscoveryCollector, SnmpCollector>();
builder.Services.AddScoped<IDiscoveryCollector, ActiveDirectoryCollector>();
builder.Services.AddScoped<IDiscoveryCollector, SshCollector>();
builder.Services.AddScoped<IDiscoveryCollector, ProxmoxCollector>();
builder.Services.AddScoped<IDiscoveryCollector, VCenterCollector>();
builder.Services.AddScoped<MatchingEngine>();
builder.Services.AddScoped<DiscoveryRunner>();
builder.Services.AddHostedService<DiscoveryWorker>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuditInterceptor>();
builder.Services.AddHostedService<AuditRetention>();

builder.Services.AddDbContext<AppDbContext>((sp, o) => o
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
    .AddInterceptors(sp.GetRequiredService<SearchIndexInterceptor>(), sp.GetRequiredService<AuditInterceptor>()));

builder.Services.AddSingleton<LdapService>();
builder.Services.AddSingleton<IUserDirectory>(sp => sp.GetRequiredService<LdapService>());
builder.Services.AddSingleton<IAuthenticator>(sp => sp.GetRequiredService<LdapService>());
builder.Services.AddSingleton<IDirectoryRoster>(sp => sp.GetRequiredService<LdapService>());
builder.Services.AddHostedService<SAloha.Api.Core.Administration.LastLoginRetention>();
builder.Services.AddSingleton<TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            // S8 : jeton émis pour S-Aloha seulement (Jwt:Audience, l'émetteur par défaut).
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? builder.Configuration["Jwt:Issuer"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
        // S5 (ADR-0018) : le navigateur présente le jeton par le cookie HttpOnly de session.
        o.Events = new JwtBearerEvents { OnMessageReceived = SessionCookie.ReadToken };
    });
// Derrière nginx : HTTPS se lit dans X-Forwarded-Proto (cookie Secure) ; seul ce schéma est repris.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
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
{
    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    // A2 (ADR-0016) : aucune entité EF dans une réponse ni une requête.
    o.JsonSerializerOptions.Converters.Add(new EntityGuard());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// S7 : pas de démarrage hors développement avec les secrets d'exemple.
SampleSecrets.Check(app.Configuration, app.Environment, app.Logger);

// Schéma par migrations EF ; une base créée par l'ancien EnsureCreated est reprise telle quelle.
// Puis structure du référentiel NIS 2 embarquée (ADR-0012), alignée à chaque démarrage.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DatabaseSchema.Migrate(db, app.Logger);
    // NIS-18 : figer les évaluations validées avant d'aligner la structure embarquée.
    SAloha.Api.Modules.ComplianceAssessment.AssessmentReferential.FreezeValidated(db);
    SAloha.Api.Modules.ComplianceAssessment.ReferentialStore.EnsureStructure(db);
    // INC-21 : échéances SLA des incidents enregistrés avant le calcul.
    SAloha.Api.Modules.IncidentManagement.IncidentSla.BackfillAsync(db).GetAwaiter().GetResult();
}

app.UseSwagger();
app.UseSwaggerUI();
app.Use(Telemetry.TraceparentHeader);
app.UseForwardedHeaders();
app.UseCors();
app.UseAuthentication();
app.Use(SessionCookie.CsrfGuard);
app.UseAuthorization();
app.MapControllers();
app.Run();

// Exposé aux tests d'intégration (WebApplicationFactory<Program>).
public partial class Program;
