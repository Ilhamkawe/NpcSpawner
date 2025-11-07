using Rocket.API;

namespace NpcSpawner
{
    public class NpcSpawnerPluginConfiguration : IRocketPluginConfiguration
    {
        public string DataFileName { get; set; }

        public void LoadDefaults()
        {
            DataFileName = "NpcPlacements.json";
        }
    }
}

