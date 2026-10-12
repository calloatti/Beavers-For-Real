using System.Collections.Generic;
using UnityEngine;

namespace Calloatti.BeaversForReal
{
  public partial class BFRManager
  {
    // Sparse changes can lie at opposite ends of the map. The legacy code
    // rescans their entire enclosing rectangle even when its middle is unchanged.
    private const int LocalChangeBucketSize = 8;
    private const long MinimumSplitArea = 144;

    private void ProcessLocalizedChangesBatched(IReadOnlyList<Vector3Int> changedCoordinates)
    {
      if (changedCoordinates.Count < 2)
      {
        ProcessLocalizedChange(changedCoordinates);
        return;
      }

      int minX = int.MaxValue, minY = int.MaxValue;
      int maxX = int.MinValue, maxY = int.MinValue;
      foreach (var coordinate in changedCoordinates)
      {
        if (coordinate.x < minX) minX = coordinate.x;
        if (coordinate.y < minY) minY = coordinate.y;
        if (coordinate.x > maxX) maxX = coordinate.x;
        if (coordinate.y > maxY) maxY = coordinate.y;
      }

      // ProcessLocalizedChange adds one tile of padding on every side.
      long originalArea = ((long)maxX - minX + 3) * ((long)maxY - minY + 3);
      if (originalArea < MinimumSplitArea)
      {
        ProcessLocalizedChange(changedCoordinates);
        return;
      }

      var buckets = new Dictionary<(int X, int Y), LocalChangeBucket>();
      var orderedBuckets = new List<LocalChangeBucket>();
      foreach (var coordinate in changedCoordinates)
      {
        var key = (BucketIndex(coordinate.x), BucketIndex(coordinate.y));
        if (!buckets.TryGetValue(key, out var bucket))
        {
          bucket = new LocalChangeBucket();
          buckets.Add(key, bucket);
          orderedBuckets.Add(bucket);
        }
        bucket.Add(coordinate);
      }

      if (orderedBuckets.Count < 2)
      {
        ProcessLocalizedChange(changedCoordinates);
        return;
      }

      long groupedArea = 0;
      foreach (var bucket in orderedBuckets)
      {
        groupedArea += bucket.PaddedArea;
      }

      // Extra calls each rescan the shoreline list, and nearby padded areas
      // overlap. Keep the original path unless splitting saves at least 25%
      // of the estimated XY scan area (the same rule as TB Optimizer).
      if (groupedArea * 4 >= originalArea * 3)
      {
        ProcessLocalizedChange(changedCoordinates);
        return;
      }

      // Run the existing four-phase scan/removal/addition without altering
      // water gating, navmesh edge ownership, or tick scheduling.
      // Avoid retrying an already processed group on failure: replaying edge
      // mutations after a partial exception is not known to be safe.
      foreach (var bucket in orderedBuckets)
      {
        ProcessLocalizedChange(bucket.Coordinates);
      }
    }

    private static int BucketIndex(int value)
    {
      int quotient = value / LocalChangeBucketSize;
      return value % LocalChangeBucketSize < 0 ? quotient - 1 : quotient;
    }

    private sealed class LocalChangeBucket
    {
      public readonly List<Vector3Int> Coordinates = new List<Vector3Int>();
      private int _minX = int.MaxValue, _minY = int.MaxValue;
      private int _maxX = int.MinValue, _maxY = int.MinValue;

      public long PaddedArea =>
        ((long)_maxX - _minX + 3) * ((long)_maxY - _minY + 3);

      public void Add(Vector3Int coordinate)
      {
        Coordinates.Add(coordinate);
        if (coordinate.x < _minX) _minX = coordinate.x;
        if (coordinate.y < _minY) _minY = coordinate.y;
        if (coordinate.x > _maxX) _maxX = coordinate.x;
        if (coordinate.y > _maxY) _maxY = coordinate.y;
      }
    }
  }
}
