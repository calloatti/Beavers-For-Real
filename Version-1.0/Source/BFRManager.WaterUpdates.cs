using System;
using System.Diagnostics;
using Timberborn.Navigation;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  public partial class BFRManager
  {
    // OPTIMIZATION: Reuse a single Stopwatch to prevent GC allocations (lag spikes) every frame
    private readonly Stopwatch _waterUpdateStopwatch = new Stopwatch();

    // OPTIMIZATION: Cache the config value so we don't query the file/dictionary thousands of times
    private float _cachedMaxWaterNavHeight;

    public void UpdateSingleton()
    {
      if (!_dynamicUpdatesEnabled) return;

      double frameTimeMs = Time.unscaledDeltaTime * 1000.0;
      double finalBudget = Math.Max(0.2, Math.Min(frameTimeMs * 0.05, 1.5));

      // Fetch the config exactly ONCE per frame
      _cachedMaxWaterNavHeight = ModStarter.Config.GetFloat("MaxWaterNavigationHeight");

      _waterUpdateStopwatch.Restart();

      // Loop through shorelines and update water levels within the time budget
      while (_waterUpdateStopwatch.Elapsed.TotalMilliseconds < finalBudget && _shorelines.Count > 0)
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

    private void ProcessWaterLevel(BFREdge s)
    {
      float depth = _waterMap.WaterDepth(s.Lower);
      float zDiff = s.Upper.z - (s.Lower.z + depth);

      // OPTIMIZATION: Read the cached float directly from memory
      bool shouldBeBlocked = zDiff > _cachedMaxWaterNavHeight;

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