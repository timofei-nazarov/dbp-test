using System.Globalization;
using System.Text.Json.Nodes;
using TaskManager.Api.Errors;

namespace TaskManager.Api.Requests;

public sealed class RequestData(JsonObject values, IFormFile? avatar = null)
{
    public IFormFile? Avatar { get; } = avatar;
    public bool Has(string key) => values.ContainsKey(key);
    public JsonNode? this[string key] => values[key];

    public static async Task<RequestData> ReadAsync(HttpRequest request)
    {
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            var data = new JsonObject();
            foreach (var field in form)
                data[field.Key] = field.Value.ToString();
            return new(data, form.Files.GetFile("avatar_picture"));
        }
        try
        {
            var body = await JsonNode.ParseAsync(request.Body);
            return body is JsonObject data ? new(data) : throw ApiException.Invalid("non_field_errors", "Invalid data. Expected a dictionary.");
        }
        catch (System.Text.Json.JsonException)
        {
            throw ApiException.Invalid("detail", "JSON parse error.");
        }
    }

    public string? String(string key, int maxLength = int.MaxValue, bool required = false, bool blank = true, bool nullable = false, bool trim = true)
    {
        if (!Has(key))
        {
            if (required) throw ApiException.Invalid(key, "This field is required.");
            return null;
        }
        if (values[key] is null)
        {
            if (nullable) return null;
            throw ApiException.Invalid(key, "This field may not be null.");
        }
        if (values[key] is not JsonValue scalar || scalar.TryGetValue<bool>(out _))
            throw ApiException.Invalid(key, "Not a valid string.");
        var value = trim ? scalar.ToString().Trim() : scalar.ToString();
        if (!blank && value.Length == 0) throw ApiException.Invalid(key, "This field may not be blank.");
        if (value.Length > maxLength) throw ApiException.Invalid(key, $"Ensure this field has no more than {maxLength} characters.");
        return value;
    }

    public long? Integer(string key, bool required = false, bool nullable = false)
    {
        if (!Has(key))
        {
            if (required) throw ApiException.Invalid(key, "This field is required.");
            return null;
        }
        if (values[key] is null)
        {
            if (nullable) return null;
            throw ApiException.Invalid(key, "This field may not be null.");
        }
        if (!decimal.TryParse(values[key]!.ToString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || decimal.Truncate(value) != value || value > long.MaxValue || value < long.MinValue)
            throw ApiException.Invalid(key, "A valid integer is required.");
        return (long)value;
    }

    public DateOnly? Date(string key)
    {
        if (!Has(key) || values[key] is null) return null;
        return ParseDate(key, values[key]!.ToString());
    }

    public static DateOnly ParseDate(string key, string? text)
    {
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            throw ApiException.Invalid(key, "Date has wrong format. Use one of these formats instead: YYYY-MM-DD.");
        return result;
    }
}
