using System;
using System.Diagnostics;
using Timberborn.Navigation;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  public partial class BARManager
  {
    public void UpdateSingleton()
    {
      if (!_dynamicUpdatesEnabled) return;

      double frameTimeMs = Time.unscaledDeltaTime * 1000.0;
      double finalBudget = Math.Max(0.2, Math.Min(frameTimeMs * 0.05, 1.5));
      Stopwatch sw = Stopwatch.StartNew();

      // Loop through shorelines and update water levels within the time budget
      while (sw.Elapsed.TotalMilliseconds < finalBudget && _shorelines.Count > 0)
      {
        if (_validationIndex >= _shorelines.Count)
        {
          _validationIndex = 0;
          break;
        }

        ProcessWaterLevel(_shorelines[_validationIndex]);
        _validationIndex++;
      }
    }

    private void ProcessWaterLevel(BAREdge s)
    {
      float depth = _waterMap.WaterDepth(s.Lower);
      float zDiff = s.Upper.z - (s.Lower.z + depth);

      bool shouldBeBlocked = zDiff > ModStarter.Config.GetFloat("MaxWaterNavigationHeight");

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