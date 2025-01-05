using BepInEx;
using BepInEx.Configuration;

namespace AiLimit
{
    public class Settings
    {
        public static ConfigEntry<bool> PluginEnabled;
        public static ConfigEntry<int> BotLimit;
        public static ConfigEntry<float> CheckInterval;

        public static ConfigEntry<float> factoryDistance;
        public static ConfigEntry<float> interchangeDistance;
        public static ConfigEntry<float> laboratoryDistance;
        public static ConfigEntry<float> lighthouseDistance;
        public static ConfigEntry<float> reserveDistance;
        public static ConfigEntry<float> shorelineDistance;
        public static ConfigEntry<float> woodsDistance;
        public static ConfigEntry<float> customsDistance;
        public static ConfigEntry<float> tarkovstreetsDistance;
        public static ConfigEntry<float> groundZeroDistance;
        
        public static void Init(ConfigFile config)
        {
            PluginEnabled = config.Bind(
                "Main Settings",
                "Plugin on/off",
                true,
                "");

            BotLimit = config.Bind(
                "Main Settings",
                "Bot Limit (At Distance)",
                10,
                "Based on your distance selected, limits up to this many # of bots moving at one time");

            CheckInterval = config.Bind(
                "Main Settings",
                "Check bots time interval",
                3f,
                "Time to wait before rechecking bots");

            factoryDistance = config.Bind(
                "Map Related",
                "factory",
                80.0f,
                "Distance after which bots are disabled.");

            customsDistance = config.Bind(
                "Map Related",
                "customs",
                400.0f,
                "Distance after which bots are disabled.");

            groundZeroDistance = config.Bind(
                "Map Related",
                "ground zero",
                400.0f,
                "Distance after which bots are disabled.");

            interchangeDistance = config.Bind(
                "Map Related",
                "interchange",
                400.0f,
                "Distance after which bots are disabled.");

            laboratoryDistance = config.Bind(
                "Map Related",
                "labs",
                250.0f,
                "Distance after which bots are disabled.");

            lighthouseDistance = config.Bind(
                "Map Related",
                "lighthouse",
                400.0f,
                "Distance after which bots are disabled.");

            reserveDistance = config.Bind(
                "Map Related",
                "reserve",
                400.0f,
                "Distance after which bots are disabled.");

            shorelineDistance = config.Bind(
                "Map Related",
                "shoreline",
                400.0f,
                "Distance after which bots are disabled.");

            woodsDistance = config.Bind(
                "Map Related",
                "woods",
                400.0f,
                "Distance after which bots are disabled.");

            tarkovstreetsDistance = config.Bind(
                "Map Related",
                "streets",
                400.0f,
                "Distance after which bots are disabled.");
        }
    }
}
