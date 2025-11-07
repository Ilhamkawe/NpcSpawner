using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Chat;
using UnityEngine;

namespace NpcSpawner.Commands
{
    public class ListNpcCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "listnpc";

        public string Help => "Lists persisted NPC placements";

        public string Syntax => "/listnpc";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "npcspawner.list" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var placements = NpcSpawnerPlugin.Instance.Placements;

            if (placements.Count == 0)
            {
                UnturnedChat.Say(caller, NpcSpawnerPlugin.Instance.Translate("npc_list_none"), Color.yellow);
                return;
            }

            foreach (var placement in placements)
            {
                var gestureStr = placement.Gesture.ToString();
                UnturnedChat.Say(
                    caller,
                    $"[{placement.PlacementId}] ID={placement.NpcId} Pos=({placement.X:F1}, {placement.Y:F1}, {placement.Z:F1}) Yaw={placement.Yaw:F1} Gesture={gestureStr}",
                    Color.cyan);
            }
        }
    }
}

