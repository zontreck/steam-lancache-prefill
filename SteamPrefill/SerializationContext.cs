namespace SteamPrefill
{
    //TODO move to models folder
    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
    [JsonSerializable(typeof(List<uint>))]
    [JsonSerializable(typeof(Dictionary<uint, HashSet<ulong>>))]
    [JsonSerializable(typeof(GetMostPlayedGamesResponse))]
    [JsonSerializable(typeof(UserLicenses))]
    [JsonSerializable(typeof(GeolocationDetails))]
    internal sealed partial class SerializationContext : JsonSerializerContext
    {
    }
}