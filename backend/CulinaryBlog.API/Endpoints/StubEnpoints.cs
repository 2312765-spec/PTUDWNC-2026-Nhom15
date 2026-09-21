namespace CulinaryBlog.API.Endpoints;

public static class StubEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app) => app;
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group) => group;
    public static RouteGroupBuilder MapRecipeEndpoints(this RouteGroupBuilder group) => group;
    public static RouteGroupBuilder MapImageEndpoints(this RouteGroupBuilder group) => group;
}