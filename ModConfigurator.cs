using Bindito.Core;
using Timberborn.ModManagerScene;

namespace Calloatti.BeaversForReal
{
  [Context("Game")]
  public class ModConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<BVRShorelineManager>().AsSingleton();
      Bind<BVRInputService>().AsSingleton();
    }
  }
}