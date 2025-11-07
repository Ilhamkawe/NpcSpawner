using UnityEngine;
using SDG.Unturned;

namespace NpcSpawner
{
    public class NpcPlacement
    {
        public string PlacementId { get; set; }
        public ushort NpcId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Yaw { get; set; }
        public EPlayerGesture Gesture { get; set; }

        public Vector3 GetPosition()
        {
            return new Vector3(X, Y, Z);
        }
    }
}

