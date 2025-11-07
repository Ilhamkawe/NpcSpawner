using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Chat;
using UnityEngine;

namespace NpcSpawner.Commands
{
    public class RemoveNpcCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "removenpc";

        public string Help => "Removes a persisted NPC placement and despawns the active NPC.";

        public string Syntax => "/removenpc <placementId>";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "npcspawner.remove" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            if (command.Length == 0)
            {
                UnturnedChat.Say(caller, Syntax, Color.yellow);
                return;
            }

            var placementId = command[0];

            if (NpcSpawnerPlugin.Instance.TryRemovePlacement(placementId))
            {
                UnturnedChat.Say(caller, $"Removed NPC placement {placementId}.", Color.green);
            }
            else
            {
                UnturnedChat.Say(caller, $"Placement {placementId} not found.", Color.red);
            }
        }
    }
}

