using System.Collections.Generic;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace NpcSpawner.Commands
{
    public class PlaceNpcCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "placenpc";

        public string Help => "Places an NPC at your current position with optional gesture";

        public string Syntax => "/placenpc <npcId> [gesture]";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "npcspawner.place" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            if (!(caller is UnturnedPlayer player))
            {
                return;
            }

            if (command.Length == 0 || !ushort.TryParse(command[0], out var npcId))
            {
                UnturnedChat.Say(caller, NpcSpawnerPlugin.Instance.Translate("placenpc_usage"), Color.yellow);
                return;
            }

            var position = player.Position;
            var yaw = player.Rotation;
            var gesture = EPlayerGesture.NONE;

            // Parse gesture parameter if provided
            if (command.Length > 1)
            {
                if (!TryParseGesture(command[1], out gesture))
                {
                    UnturnedChat.Say(caller, $"Invalid gesture: {command[1]}. Available: NONE, SALUTE, POINT, WAVE, FACEPALM", Color.red);
                    return;
                }
            }

            if (NpcSpawnerPlugin.Instance.TryAddPlacement(npcId, position, yaw, gesture, out var message))
            {
                UnturnedChat.Say(caller, message, Color.green);
            }
            else
            {
                UnturnedChat.Say(caller, message, Color.red);
            }
        }

        private static bool TryParseGesture(string gestureStr, out EPlayerGesture gesture)
        {
            gesture = EPlayerGesture.NONE;

            if (string.IsNullOrWhiteSpace(gestureStr))
            {
                return true; // Default to NONE
            }

            gestureStr = gestureStr.ToUpperInvariant();

            switch (gestureStr)
            {
                case "NONE":
                case "0":
                    gesture = EPlayerGesture.NONE;
                    return true;
                case "SALUTE":
                case "1":
                    gesture = EPlayerGesture.SALUTE;
                    return true;
                case "POINT":
                case "2":
                    gesture = EPlayerGesture.POINT;
                    return true;
                case "WAVE":
                case "3":
                    gesture = EPlayerGesture.WAVE;
                    return true;
                case "FACEPALM":
                case "4":
                    gesture = EPlayerGesture.FACEPALM;
                    return true;
                default:
                    return false;
            }
        }
    }
}

