using System;
using System.Collections.Generic;
using Timberborn.Navigation;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  public partial class BFRManager
  {
    private const int ShorelinesPerTick = 500;

    public void Tick()
    {
      if (!_dynamicUpdatesEnabled) return;

      // Process all pending navmesh changes (triggered by terrain/building edits)
      if (_pendingUpdateCoordinates.Count > 0)
      {
        var coords = new List<Vector3Int>(_pendingUpdateCoordinates);
        _pendingUpdateCoordinates.Clear();
        ProcessLocalizedChange(coords);
      }

      // Process a batch of shorelines for water level updates
      int maxToProcess = Math.Min(ShorelinesPerTick, _shorelines.Count);
      int processed = 0;
      while (processed < maxToProcess)
      {
        if (_validationIndex >= _shorelines.Count)
        {
          _validationIndex = 0;
        }

        ProcessWaterLevel(_shorelines[_validationIndex]);
        _validationIndex++;
        processed++;
      }
    }

    private void ProcessWaterLevel(BFREdge s)
    {
      // Ask the engine for the true physics height of the water surface at this column
      float waterSurface = _waterMap.WaterHeightOrFloor(s.Lower);
      float zDiff = s.Upper.z - waterSurface;

      // Use the Vector3Int override to respect vertical water column stacking (e.g., aqueducts)
      float contamination = _waterMap.ColumnContamination(s.Lower);

      bool blockedByHeight = zDiff > ModStarter.Config.GetFloat("MaxWaterNavigationHeight");
      bool blockedByContamination = contamination > ModStarter.Config.GetFloat("MaxWaterContamination");

      // If the jump is too high OR the water is too toxic, block the path
      bool shouldBeBlocked = blockedByHeight || blockedByContamination;

      if (shouldBeBlocked != s.IsBlockedByWater)
      {
        s.IsBlockedByWater = shouldBeBlocked;

        if (shouldBeBlocked)
        {
          _navMeshService.BlockEdge(s.EdgeDown);
          _navMeshService.BlockEdge(s.EdgeUp);
        }
        else
        {
          _navMeshService.UnblockEdge(s.EdgeDown);
          _navMeshService.UnblockEdge(s.EdgeUp);
        }
      }
    }
  }
}