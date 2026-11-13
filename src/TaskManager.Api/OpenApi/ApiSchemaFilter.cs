using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using TaskManager.Api.Controllers;
using TaskManager.Contracts;

namespace TaskManager.Api.OpenApi;

public sealed class ApiSchemaFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var controller = context.MethodInfo.DeclaringType;
        var method = context.ApiDescription.HttpMethod;
        Type? input = controller == typeof(AuthController)
            ? context.MethodInfo.Name == "Refresh" ? typeof(RefreshInput) : typeof(LoginInput)
            : controller == typeof(UsersController) ? typeof(UserInput)
            : controller == typeof(TagsController) ? typeof(TagInput)
            : controller == typeof(TasksController) ? typeof(TaskInput)
            : controller == typeof(JobsController) ? typeof(CountdownInput) : null;
        if (input is not null && method is "POST" or "PUT" or "PATCH")
        {
            var schema = context.SchemaGenerator.GenerateSchema(input, context.SchemaRepository);
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new() { Schema = schema } }
            };
            if (controller == typeof(UsersController))
            {
                operation.RequestBody.Content!["multipart/form-data"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["username"] = new OpenApiSchema { Type = JsonSchemaType.String },
                            ["first_name"] = new OpenApiSchema { Type = JsonSchemaType.String },
                            ["last_name"] = new OpenApiSchema { Type = JsonSchemaType.String },
                            ["email"] = new OpenApiSchema { Type = JsonSchemaType.String },
                            ["role"] = new OpenApiSchema { Type = JsonSchemaType.String },
                            ["avatar_picture"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" }
                        }
                    }
                };
            }
        }
        if (controller?.IsDefined(typeof(AuthorizeAttribute), true) == true)
        {
            operation.Security = [new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer")] = []
            }];
        }
    }
}
