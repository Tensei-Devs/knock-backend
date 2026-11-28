// Muestra para la regla knock-sin-http-logging (spec 035, CA-08). No se compila y CI la ignora (.semgrepignore); la regla debe marcar las dos líneas.
var builder = WebApplication.CreateBuilder(args);
// ruleid: knock-sin-http-logging
builder.Services.AddHttpLogging(options => options.LoggingFields = HttpLoggingFields.All);
var app = builder.Build();
// ruleid: knock-sin-http-logging
app.UseHttpLogging();
