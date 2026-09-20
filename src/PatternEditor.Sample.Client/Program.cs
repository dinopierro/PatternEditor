using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PatternEditor.Element.Circle;
using PatternEditor.Element.Ellipse;
using PatternEditor.Element.Image;
using PatternEditor.Element.Line;
using PatternEditor.Element.Path;
using PatternEditor.Element.Polygon;
using PatternEditor.Element.Polyline;
using PatternEditor.Element.Rect;
using PatternEditor.Element.Text;
using PatternEditor.Extensions;
using PatternEditor.Sample.Client;

// ---------------------------------------------------------------------------------------
// Avvio dell'applicazione di riferimento (Blazor WebAssembly).
//
// Questo file è l'unico punto dell'intera soluzione in cui si sa quali tipi di elemento
// esistono: la libreria non referenzia i plugin, e i plugin non si conoscono fra loro. È qui
// che si decide la dotazione dell'applicazione, ed è qui - e solo qui - che si interviene
// per aggiungerne o toglierne uno.
// ---------------------------------------------------------------------------------------
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Libreria Pattern Editor + i plugin che questa applicazione desidera rendere disponibili.
// Un futuro elemento (Circle, Path, ...) richiederebbe solo un'ulteriore riga qui,
// senza alcuna modifica alla libreria o al componente PatternEditor.
builder.Services.AddPatternEditor();
builder.Services.AddPatternEditorPlugin<LinePlugin>();
builder.Services.AddPatternEditorPlugin<RectPlugin>();
builder.Services.AddPatternEditorPlugin<CirclePlugin>();
builder.Services.AddPatternEditorPlugin<EllipsePlugin>();
builder.Services.AddPatternEditorPlugin<PathPlugin>();
builder.Services.AddPatternEditorPlugin<PolygonPlugin>();
builder.Services.AddPatternEditorPlugin<PolylinePlugin>();
builder.Services.AddPatternEditorPlugin<TextPlugin>();
builder.Services.AddPatternEditorPlugin<ImagePlugin>();

// L'API risponde dalla stessa origine da cui arriva il client: e' la stessa applicazione,
// che serve sia le pagine sia le risposte. Nessun indirizzo da configurare al cambio di
// ambiente, nessun CORS da concedere, nessun secondo certificato.
//
// L'impostazione resta comunque leggibile dalla configurazione, e non per abitudine: chi
// volesse tenere l'API su una macchina propria — perche' scala diversamente, o perche' sta
// dietro a un'altra rete — la dichiara e tutto continua a funzionare. Il valore di riposo
// e' l'indirizzo di casa.
//
// Il client HTTP passa da un gestore che aggiunge il gettone della sessione a ogni
// richiesta. In un punto solo, e non nei singoli metodi: una chiamata a cui ci si
// dimenticasse di aggiungerlo non fallirebbe in modo evidente — risponderebbe come se
// l'utente fosse anonimo, cioè somigliando a un comportamento corretto.
builder.Services.AddScoped<PatternEditor.Sample.Client.Services.SessioneCorrente>();
builder.Services.AddScoped(sp => new HttpClient(
    new PatternEditor.Sample.Client.Services.GettoneHandler(
        sp.GetRequiredService<PatternEditor.Sample.Client.Services.SessioneCorrente>())
    {
        InnerHandler = new HttpClientHandler(),
    })
{
    BaseAddress = new Uri(builder.Configuration["PatternApiBaseAddress"]
                          ?? builder.HostEnvironment.BaseAddress),
});
builder.Services.AddScoped<PatternEditor.Sample.Client.Services.PatternApiClient>();
builder.Services.AddScoped<PatternEditor.Sample.Client.Services.AuthClient>();

// I testi tradotti sono file statici, e li chiede un client HTTP diverso da quello dei
// pattern: quello porta il gettone della sessione a ogni richiesta, e un catalogo di
// traduzioni non ha ragione di portarlo. Che i due indirizzi ora coincidano non cambia
// niente — il giorno in cui l'API tornasse altrove, questo resterebbe qui.
builder.Services.AddScoped(sp => new PatternEditor.Sample.Client.Services.Lingua(
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) },
    sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>(),
    sp.GetRequiredService<PatternEditor.Abstractions.Localization.TestiEditor>()));

await builder.Build().RunAsync();
