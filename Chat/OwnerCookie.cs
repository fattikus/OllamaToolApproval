namespace OllamaToolApproval.Chat;

/// <summary>Stand-in for real authentication: a random per-browser ID that approvals are bound to.</summary>
public static class OwnerCookie
{
    private const string Name = "gp_owner";

    public static IApplicationBuilder UseOwnerCookie(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (!context.Request.Cookies.TryGetValue(Name, out var ownerId) || string.IsNullOrWhiteSpace(ownerId))
        {
            ownerId = Guid.NewGuid().ToString("N");
            context.Response.Cookies.Append(Name, ownerId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
            });
        }

        context.Items[Name] = ownerId;
        await next(context);
    });

    public static string GetOwnerId(this HttpContext context) => (string)context.Items[Name]!;
}
