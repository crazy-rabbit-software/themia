namespace Themia.Core.Tests;

/// <summary>Synthetic open-meteo replies (never the network), shared by the SDK's wire tests and the air widget's rule tests.</summary>
internal static class OnlineReplies
{
    public const long Hour = 1_791_799_200; // a whole hour, unix seconds

    public static string AirJson(string current) =>
        "{\"latitude\":47.4,\"longitude\":8.5,\"current_units\":{\"time\":\"unixtime\"},\"current\":{" + current + "}}";

    public const string GoodAir =
        "\"time\":1791799200,\"interval\":3600,\"european_aqi\":25,\"us_aqi\":36,\"pm2_5\":7.7,\"pm10\":11.9,"
        + "\"ozone\":56.0,\"uv_index\":2.5,\"alder_pollen\":0.0,\"birch_pollen\":0.0,\"grass_pollen\":12.0,"
        + "\"mugwort_pollen\":0.2,\"olive_pollen\":0.0,\"ragweed_pollen\":6.0";
}
