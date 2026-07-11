using Calloatti.Config;
using HarmonyLib;
using Timberborn.Modding;
using Timberborn.ModManagerScene;

namespace Calloatti.BeaversForReal
{
  public class ModStarter : IModStarter
  {
    public static SimpleConfig Config { get; private set; }

    public void StartMod(IModEnvironment modEnvironment)
    {
      Config = new SimpleConfig(modEnvironment.ModPath);
      new Harmony("calloatti.beaversforreal").PatchAll();
    }
  }
}