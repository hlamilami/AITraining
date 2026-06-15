using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FundTransfer.Api.Configuration;

/// <summary>Adds Idempotency-Key header to transfer endpoint operations.</summary>
public class IdempotencyKeyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var actionDescriptor = context.ApiDescription.ActionDescriptor;
        actionDescriptor.RouteValues.TryGetValue("controller", out var controllerName);
        actionDescriptor.RouteValues.TryGetValue("action", out var actionName);

        if (controllerName == "Transfers" && actionName == "CreateTransfer")
        {
            operation.Parameters ??= new List<OpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Idempotency-Key",
                In = ParameterLocation.Header,
                Required = true,
                Schema = new OpenApiSchema { Type = "string", Format = "uuid" },
                Description = "Unique UUID to prevent duplicate transfer submissions."
            });
        }
    }
}
