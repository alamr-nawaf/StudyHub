using StudyHub.API.Authorization;
using StudyHub.API.Common;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.DependencyInjection;
using StudyHub.Infrastructure.DependencyInjection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using StudyHub.Infrastructure.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// مصدر الهوية المؤقت — يحتاج الوصول للطلب الحالي
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// نفس الإعدادات التي أصدرت التوكن. فحص الطول يبقى قائمًا في AddInfrastructureServices،
// فمفتاح قصير يوقف الإقلاع حتى لو وصل إلى هنا
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // الأسماء تصل كما صدرت: sub وrole، بلا تحويل إلى روابط طويلة
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),

            // صريح لا افتراضي: الافتراضي خمس دقائق، فتوكن منتهٍ يبقى مقبولًا (§9.1)
            ClockSkew = TimeSpan.FromSeconds(30),

            RoleClaimType = "role",
            NameClaimType = "sub"
        };
    });

// محمي افتراضيًا: كل endpoint يحتاج توكنًا ما لم يُعلَن [AllowAnonymous] صراحةً.
// بدونها يمرّ الطلب المجهول إلى المعالِج فيرجع 403 بدل 401، ويكشف 404 وجود المعرّفات
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPermissionPolicies();
});
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

await app.SeedAdministratorAsync();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();