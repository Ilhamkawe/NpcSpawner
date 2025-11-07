using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Rocket.API.Collections;
using Rocket.Core.Logging;
using Rocket.Core.Plugins;
using SDG.Unturned;
using UnityEngine;

namespace NpcSpawner
{
    public class NpcSpawnerPlugin : RocketPlugin<NpcSpawnerPluginConfiguration>
    {
        private readonly List<NpcPlacement> _placements = new List<NpcPlacement>();
        private readonly object _sync = new object();

        public static NpcSpawnerPlugin Instance { get; private set; }

        private string DataFilePath
        {
            get
            {
                var directory = Directory;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                return Path.Combine(directory, Configuration.Instance.DataFileName);
            }
        }

        protected override void Load()
        {
            Instance = this;
            LoadPlacements();
            Logger.Log("[NpcSpawner] Plugin loaded.");
        }

        protected override void Unload()
        {
            Instance = null;
            Logger.Log("[NpcSpawner] Plugin unloaded.");
        }

        public override TranslationList DefaultTranslations => new TranslationList
        {
            { "placenpc_success", "NPC {0} placed and saved." },
            { "placenpc_spawn_failed", "Couldn't spawn NPC {0}: {1}" },
            { "placenpc_invalid_id", "Invalid NPC id." },
            { "placenpc_usage", "Usage: /placenpc <npcId>" },
            { "placenpc_asset_missing", "NPC asset {0} not found." },
            { "npc_list_entry", "[{0}] ID={1} Position=({2:F1}, {3:F1}, {4:F1}) Yaw={5:F1}" },
            { "npc_list_none", "No NPC placements saved." }
        };

        public IReadOnlyList<NpcPlacement> Placements
        {
            get
            {
                lock (_sync)
                {
                    return _placements.ToList();
                }
            }
        }

        public bool TryAddPlacement(ushort npcId, Vector3 position, float yaw, EPlayerGesture gesture, out string message)
        {
            var asset = Assets.find(EAssetType.NPC, npcId) as Asset;
            if (asset == null)
            {
                message = Translate("placenpc_asset_missing", npcId);
                return false;
            }

            if (!TrySpawnNpc(asset, npcId, position, yaw, gesture, out var spawnError))
            {
                message = Translate("placenpc_spawn_failed", npcId, spawnError ?? "unknown error");
                return false;
            }

            var placement = new NpcPlacement
            {
                PlacementId = Guid.NewGuid().ToString("N"),
                NpcId = npcId,
                X = position.x,
                Y = position.y,
                Z = position.z,
                Yaw = yaw,
                Gesture = gesture
            };

            lock (_sync)
            {
                _placements.Add(placement);
                SavePlacementsInternal();
            }

            message = Translate("placenpc_success", npcId);
            return true;
        }

        public void RespawnAll()
        {
            List<NpcPlacement> snapshot;
            lock (_sync)
            {
                snapshot = _placements.ToList();
            }

            foreach (var placement in snapshot)
            {
                var asset = Assets.find(EAssetType.NPC, placement.NpcId) as Asset;
                if (asset == null)
                {
                    Logger.LogWarning($"[NpcSpawner] NPC asset {placement.NpcId} missing. Skipping spawn.");
                    continue;
                }

                if (!TrySpawnNpc(asset, placement.NpcId, placement.GetPosition(), placement.Yaw, placement.Gesture, out var error))
                {
                    Logger.LogWarning($"[NpcSpawner] Failed to spawn NPC {placement.NpcId}: {error}");
                }
            }
        }

        private void LoadPlacements()
        {
            lock (_sync)
            {
                _placements.Clear();

                var path = DataFilePath;
                if (!File.Exists(path))
                {
                    SavePlacementsInternal();
                }
                else
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        var list = JsonConvert.DeserializeObject<List<NpcPlacement>>(json) ?? new List<NpcPlacement>();
                        _placements.AddRange(list);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[NpcSpawner] Failed to read placements: {ex.Message}");
                    }
                }
            }

            RespawnAll();
        }

        public bool TryRemovePlacement(string placementId)
        {
            NpcPlacement placement = null;
            lock (_sync)
            {
                var index = _placements.FindIndex(p => p.PlacementId.Equals(placementId, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    return false;
                }

                placement = _placements[index];
                _placements.RemoveAt(index);
                SavePlacementsInternal();
            }

            // Despawn NPC yang aktif di posisi tersebut
            if (placement != null)
            {
                TryDespawnNpcAtPosition(placement.GetPosition(), placement.NpcId);
            }

            return true;
        }

        private static void TryDespawnNpcAtPosition(Vector3 position, ushort npcId)
        {
            try
            {
                // Cari semua InteractableObjectNPC di sekitar posisi
                var allObjects = UnityEngine.Object.FindObjectsOfType<InteractableObjectNPC>();
                if (allObjects == null || allObjects.Length == 0)
                {
                    return;
                }

                var targetPosition = position;
                var maxDistance = 2f; // Radius 2 meter untuk mencari NPC
                var maxDistanceSqr = maxDistance * maxDistance;

                InteractableObjectNPC closestNpc = null;
                var closestDistanceSqr = float.MaxValue;

                foreach (var npc in allObjects)
                {
                    if (npc == null || npc.transform == null)
                    {
                        continue;
                    }

                    var npcPosition = npc.transform.position;
                    var distanceSqr = (npcPosition - targetPosition).sqrMagnitude;

                    if (distanceSqr <= maxDistanceSqr && distanceSqr < closestDistanceSqr)
                    {
                        // Verify NPC ID matches if possible
                        var npcAsset = npc.asset;
                        if (npcAsset != null && npcAsset.id == npcId)
                        {
                            closestNpc = npc;
                            closestDistanceSqr = distanceSqr;
                        }
                        else if (npcAsset == null)
                        {
                            // If we can't verify ID, still consider it if it's close enough
                            closestNpc = npc;
                            closestDistanceSqr = distanceSqr;
                        }
                    }
                }

                if (closestNpc != null && closestNpc.transform != null)
                {
                    // Destroy the NPC GameObject
                    UnityEngine.Object.Destroy(closestNpc.gameObject);
                    Logger.Log($"[NpcSpawner] Despawned NPC {npcId} at position ({position.x:F1}, {position.y:F1}, {position.z:F1})");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[NpcSpawner] Failed to despawn NPC at position: {ex.Message}");
            }
        }

        private void SavePlacementsInternal()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_placements, Formatting.Indented);
                File.WriteAllText(DataFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[NpcSpawner] Failed to save placements: {ex.Message}");
            }
        }

        private static bool TrySpawnNpc(Asset npcAsset, ushort npcId, Vector3 position, float yaw, EPlayerGesture gesture, out string error)
        {
            error = null;

            try
            {
                // Attempt to use reflection to call NPCManager.spawnNPC or similar method.
                var assembly = npcAsset.GetType().Assembly;
                var npcManagerType = assembly.GetType("SDG.Unturned.NPCManager") ?? assembly.GetType("SDG.Unturned.NPCSpawner");
                if (npcManagerType == null)
                {
                    error = "NPCManager type not found";
                    return false;
                }

                var methods = npcManagerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                foreach (var method in methods.Where(m => m.Name.IndexOf("spawn", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length == 0)
                    {
                        continue;
                    }

                    var args = new object[parameters.Length];
                    var success = true;

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var param = parameters[i];
                        var pType = param.ParameterType;

                        if (pType.IsInstanceOfType(npcAsset))
                        {
                            args[i] = npcAsset;
                        }
                        else if (pType == typeof(Vector3))
                        {
                            args[i] = position;
                        }
                        else if (pType == typeof(Quaternion))
                        {
                            args[i] = Quaternion.Euler(0f, yaw, 0f);
                        }
                        else if (pType == typeof(float))
                        {
                            args[i] = yaw;
                        }
                        else if (pType == typeof(ushort))
                        {
                            args[i] = npcId;
                        }
                        else if (pType == typeof(uint))
                        {
                            args[i] = (uint)npcId;
                        }
                        else if (pType == typeof(bool))
                        {
                            args[i] = true;
                        }
                        else if (pType == typeof(string))
                        {
                            args[i] = string.Empty;
                        }
                        else
                        {
                            success = false;
                            break;
                        }
                    }

                    if (!success)
                    {
                        continue;
                    }

                    method.Invoke(null, args);

                    // Apply gesture setelah spawn
                    if (gesture != EPlayerGesture.NONE)
                    {
                        ApplyGestureToNpcAtPosition(position, gesture);
                    }

                    return true;
                }

                error = "No suitable spawn method found";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void ApplyGestureToNpcAtPosition(Vector3 position, EPlayerGesture gesture)
        {
            try
            {
                // Cari NPC yang baru di-spawn di posisi tersebut
                var allObjects = UnityEngine.Object.FindObjectsOfType<InteractableObjectNPC>();
                if (allObjects == null || allObjects.Length == 0)
                {
                    return;
                }

                var targetPosition = position;
                var maxDistance = 2f;
                var maxDistanceSqr = maxDistance * maxDistance;

                InteractableObjectNPC closestNpc = null;
                var closestDistanceSqr = float.MaxValue;

                foreach (var npc in allObjects)
                {
                    if (npc == null || npc.transform == null)
                    {
                        continue;
                    }

                    var npcPosition = npc.transform.position;
                    var distanceSqr = (npcPosition - targetPosition).sqrMagnitude;

                    if (distanceSqr <= maxDistanceSqr && distanceSqr < closestDistanceSqr)
                    {
                        closestNpc = npc;
                        closestDistanceSqr = distanceSqr;
                    }
                }

                if (closestNpc != null)
                {
                    // Apply gesture menggunakan reflection
                    var npcType = closestNpc.GetType();
                    var animatorProp = npcType.GetProperty("animator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? npcType.GetProperty("Animator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (animatorProp != null)
                    {
                        var animator = animatorProp.GetValue(closestNpc);
                        if (animator != null)
                        {
                            var animatorType = animator.GetType();
                            var tellGestureMethod = animatorType.GetMethod("tellGesture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                ?? animatorType.GetMethod("TellGesture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                            if (tellGestureMethod != null)
                            {
                                tellGestureMethod.Invoke(animator, new object[] { gesture });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[NpcSpawner] Failed to apply gesture: {ex.Message}");
            }
        }
    }
}

