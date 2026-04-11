using UnityEngine;

namespace Calloatti.BeaversForReal
{
  public partial class BARManager
  {
    private bool IsStandableSurface(Vector3Int airCoords)
    {
      if (airCoords.z <= 0) return false;

      // 1. Is there solid terrain directly below this air block?
      if (_terrainService.OnGround(airCoords)) return true;

      // 2. Is there a solid, finished man-made block (like a Levee or Platform) directly below?
      Vector3Int blockBelow = new Vector3Int(airCoords.x, airCoords.y, airCoords.z - 1);
      if (_stackableBlockService.IsFinishedStackableBlockAt(blockBelow)) return true;

      return false;
    }
  }
}