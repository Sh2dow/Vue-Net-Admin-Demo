using System.Text.Json;

namespace backend.Shared.Application.Messaging;

public static class IntegrationEventSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T message)
    {
        return JsonSerializer.Serialize(message, JsonOptions);
    }

    public static T Deserialize<T>(string payload)
    {
        var result = JsonSerializer.Deserialize<T>(payload, JsonOptions);
        if (result == null)
        {
            throw new InvalidOperationException($"Failed to deserialize integration payload to {typeof(T).Name}.");
        }

        return result;
    }
}
