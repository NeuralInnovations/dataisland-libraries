using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace DataIsland.Middleware;

public class ApiResponseFilter : IAsyncResultFilter
{
    private const string PaginatedResultTypeName = "Dataisland.MongoDB.PaginatedResult`1";

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Opt-out: controllers/actions marked with [SkipApiResponseWrapper] get
        // raw JSON output for external-protocol compatibility.
        if (context.ActionDescriptor is ControllerActionDescriptor cad
            && (cad.MethodInfo.GetCustomAttributes(typeof(SkipApiResponseWrapperAttribute), inherit: true).Length > 0
                || cad.ControllerTypeInfo.GetCustomAttributes(typeof(SkipApiResponseWrapperAttribute), inherit: true).Length > 0))
        {
            await next();
            return;
        }

        var statusCode = (context.Result as IStatusCodeActionResult)?.StatusCode
                         ?? context.HttpContext.Response.StatusCode;
        if (statusCode >= StatusCodes.Status400BadRequest)
        {
            var source = (context.Result as ObjectResult)?.Value;
            context.Result = new ObjectResult(
                ApiErrorResponses.Create(context.HttpContext, statusCode, source))
            {
                StatusCode = statusCode
            };
            await next();
            return;
        }

        if (context.Result is ObjectResult { Value: not null } result)
        {
            var value = result.Value;
            var type = value.GetType();

            if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == PaginatedResultTypeName)
            {
                var items = type.GetProperty("Items")!.GetValue(value);
                var page = (int)type.GetProperty("Page")!.GetValue(value)!;
                var pageSize = (int)type.GetProperty("PageSize")!.GetValue(value)!;
                var total = (long)type.GetProperty("Total")!.GetValue(value)!;
                var totalPages = (int)type.GetProperty("TotalPages")!.GetValue(value)!;

                result.Value = new PaginatedApiResponse(
                    items,
                    null,
                    new PaginationMeta(page, pageSize, total, totalPages));
            }
            else
            {
                result.Value = new ApiResponse(value, null);
            }
        }

        await next();
    }
}
