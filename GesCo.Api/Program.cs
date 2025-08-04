using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using GesCo.Api.Data;
using GesCo.Api.Models;
using GesCo.Api.Services;
using DotNetEnv;

// 🔧 Cargar variables de entorno desde archivo .env
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// 🔧 Construir connection string desde variables de entorno
var connectionString = $"Host={Environment.GetEnvironmentVariable("POSTGRES_HOST")};Database={Environment.GetEnvironmentVariable("POSTGRES_DATABASE")};Username={Environment.GetEnvironmentVariable("POSTGRES_USERNAME")};Password={Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")};Port={Environment.GetEnvironmentVariable("POSTGRES_PORT")}";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configuración de Identity
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    // 🔧 Configuración de contraseñas desde variables de entorno
    var minLength = int.Parse(Environment.GetEnvironmentVariable("SECURITY_PASSWORD_MIN_LENGTH") ?? "6");
    var requireDigit = bool.Parse(Environment.GetEnvironmentVariable("SECURITY_PASSWORD_REQUIRE_DIGIT") ?? "true");
    var requireUppercase = bool.Parse(Environment.GetEnvironmentVariable("SECURITY_PASSWORD_REQUIRE_UPPERCASE") ?? "true");
    var requireNonAlphanumeric = bool.Parse(Environment.GetEnvironmentVariable("SECURITY_PASSWORD_REQUIRE_NON_ALPHANUMERIC") ?? "false");
    
    options.Password.RequiredLength = minLength;
    options.Password.RequireDigit = requireDigit;
    options.Password.RequireUppercase = requireUppercase;
    options.Password.RequireNonAlphanumeric = requireNonAlphanumeric;
    options.Password.RequireLowercase = true;

    // Configuración de usuario
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    // 🔧 Configuración de bloqueo desde variables de entorno
    var maxFailedAttempts = int.Parse(Environment.GetEnvironmentVariable("SECURITY_MAX_FAILED_ATTEMPTS") ?? "5");
    var lockoutMinutes = int.Parse(Environment.GetEnvironmentVariable("SECURITY_LOCKOUT_MINUTES") ?? "15");
    
    options.Lockout.MaxFailedAccessAttempts = maxFailedAttempts;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(lockoutMinutes);
    options.Lockout.AllowedForNewUsers = true;

    // Confirmación de email
    options.SignIn.RequireConfirmedEmail = false; // Cambiar a true en producción
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 🔧 Configuración de JWT desde variables de entorno
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("JWT_SECRET_KEY no está configurada en las variables de entorno");
}

var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Cambiar a true en producción
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER"),
        ValidateAudience = true,
        ValidAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Agregar servicios de autorización
builder.Services.AddAuthorization();

// Registrar servicios personalizados
builder.Services.AddScoped<IAuthService, AuthService>();

// Agregar controllers
builder.Services.AddControllers();

// Configuración de Swagger con soporte para JWT
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "GesCo API", 
        Version = "v1",
        Description = "Sistema Integral de Gestión para Organizaciones Comunitarias"
    });

    // Configuración para JWT en Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando el esquema Bearer. Ejemplo: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configuración de CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configuración del pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "GesCo API v1");
    });
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Endpoint de salud básico
app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTime.UtcNow })
    .WithName("HealthCheck")
    .WithOpenApi();

// Crear base de datos y aplicar migraciones automáticamente en desarrollo
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        await context.Database.MigrateAsync();
        Console.WriteLine("✅ Base de datos migrada exitosamente");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error al migrar la base de datos: {ex.Message}");
    }
}

app.Run();