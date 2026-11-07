using UnityEngine;
using BlastFrame.Core.Seeding;

namespace BlastFrame.Gameplay.Rooms
{
    /// <summary>Weighted, seeded set of room layouts for one room slot (DESIGN.md §6). Pick with
    /// <c>set.Pick(seeds, areaId, roomIndex, slotId)</c>.</summary>
    [CreateAssetMenu(fileName = "NewRoomVariantSet", menuName = "Blast Frame/Rooms/Room Variant Set")]
    public sealed class RoomVariantSetSO : VariantSetSO<RoomVariantSO> { }
}
