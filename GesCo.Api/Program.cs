using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using GesCo.Api.Data;
using GesCo.Api.Models;
using GesCo.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuración de la base de datos
var connectionString = builder.Configuration.GetConnectionString("PostgreSQLConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configuración de Identity
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    // Configuración de contraseñas basada en appsettings.json
    var passwordSettings = builder.Configuration.GetSection("Security:PasswordRequirements");
    options.Password.RequiredLength = passwordSettings.GetValue<int>("MinLength", 6);
    options.Password.RequireDigit = passwordSettings.GetValue<bool>("RequireDigit", true);
    options.Password.RequireUppercase = passwordSettings.GetValue<bool>("RequireUppercase", true);
    options.Password.RequireNonAlphanumeric = passwordSettings.GetValue<bool>("RequireNonAlphanumeric", false);
    options.Password.RequireLowercase = true;

    // Configuración de usuario
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    // Configuración de bloqueo
    var securitySettings = builder.Configuration.GetSection("Security");
    options.Lockout.MaxFailedAccessAttempts = securitySettings.GetValue<int>("MaxFailedAttempts", 5);
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(securitySettings.GetValue<int>("LockoutMinutes", 15));
    options.Lockout.AllowedForNewUsers = true;

    // Confirmación de email
    options.SignIn.RequireConfirmedEmail = false; // Cambiar a true en producción
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configuración de JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
var key = Encoding.ASCII.GetBytes(secretKey!);

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
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
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