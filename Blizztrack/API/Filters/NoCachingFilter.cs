using Microsoft.AspNetCore.Mvc.Filters;

namespace Blizztrack.API.Filters
{
    public class NoCachingFilter : IActionFilter
    {
        public void OnActionExecuted(ActionExecutedContext context)
        {
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.Response.Headers is null)
                return;

            context.HttpContext.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            context.HttpContext.Response.Headers.Pragma = "no-cache";
            context.HttpContext.Response.Headers.Expires = "0";
        }
    }
}
