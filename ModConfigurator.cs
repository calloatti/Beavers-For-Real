using Bindito.Core;
using Timberborn.ModManagerScene;

namespace Calloatti.BeaversForReal
{
  [Context("Game")]
  public class ModConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<BFRManager>().AsSingleton();
      Bind<BFRInputService>().AsSingleton();
    }
  }
}