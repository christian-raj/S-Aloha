using System.Diagnostics;
using System.Diagnostics.Metrics;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace SAloha.Api.Core.Observability;

/// <summary>
/// O2 (ADR-0017) : journal JSON hors développement, identifiant de trace sur chaque ligne et
/// dans l'en-tête <c>traceparent</c> des réponses, traces et métriques OpenTelemetry exportées
/// en OTLP seulement si <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> est défini. Jamais de secret ni de
/// contenu de fiche dans le journal ou les traces.
/// </summary>
public static class Telemetry
{
    public const string Name = "SAloha";
    public static readonly ActivitySource Source = new(Name);
    public static readonly Meter Meter = new(Name);

    /// <summary>Connexions à l'annuaire, par résultat (ok, refus, injoignable).</summary>
    public static readonly Histogram<double> DirectoryLogin =
        Meter.CreateHistogram<double>("saloha.directory.login.duration", "s", "Durée d'une connexion par l'annuaire");
    /// <summary>Exécutions des tâches de découverte, par type de scan et résultat.</summary>
    public static readonly Counter<long> DiscoveryRuns =
        Meter.CreateCounter<long>("saloha.discovery.runs", description: "Tâches de découverte exécutées");
    public static readonly Counter<long> DiscoveredAssets =
        Meter.CreateCounter<long>("saloha.discovery.assets", description: "Équipements vus par la découverte");
    public static readonly Counter<long> Notifications =
        Meter.CreateCounter<long>("saloha.notifications", description: "Notifications enregistrées");

    /// <summary><c>Logging:Format</c> : <c>json</c> ou <c>text</c> ; sans réglage, JSON hors développement.</summary>
    public static bool JsonLogs(IConfiguration config, IHostEnvironment env) => config["Logging:Format"]?.ToLowerInvariant() switch
    {
        "json" => true,
        "text" => false,
        _ => !env.IsDevelopment(),
    };

    public static void AddObservability(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        if (JsonLogs(builder.Configuration, builder.Environment))
            builder.Logging.AddJsonConsole(o =>
            {
                o.IncludeScopes = true;
                o.UseUtcTimestamp = true;
                o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            });
        else
            builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
        // Chaque ligne porte la trace de la requête (portée TraceId, SpanId).
        builder.Logging.Configure(o => o.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

        var export = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        var otel = builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService(
            builder.Configuration["OTEL_SERVICE_NAME"] ?? "s-aloha-api",
            serviceVersion: typeof(Telemetry).Assembly.GetName().Version?.ToString()));
        // Les traces sont toujours suivies (identifiant de trace du journal et de traceparent) ;
        // elles ne quittent le processus que vers un collecteur configuré.
        otel.WithTracing(t =>
        {
            t.AddSource(Name)
             .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/swagger"))
             .AddHttpClientInstrumentation()
             .AddNpgsql();
            if (export) t.AddOtlpExporter();
        });
        if (export)
            otel.WithMetrics(m => m.AddMeter(Name).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter());
    }

    /// <summary>L'identifiant de trace de la requête est renvoyé au client (<c>traceparent</c>) : un incident signalé se retrouve dans le journal.</summary>
    public static Task TraceparentHeader(HttpContext ctx, Func<Task> next)
    {
        ctx.Response.OnStarting(() =>
        {
            if (Activity.Current is { } activity) ctx.Response.Headers["traceparent"] = activity.Id;
            return Task.CompletedTask;
        });
        return next();
    }
}
