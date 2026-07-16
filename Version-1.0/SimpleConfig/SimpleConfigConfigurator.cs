using Bindito.Core;

namespace Calloatti.Config
{
  [Context("MainMenu")]
  [Context("Game")]
  [Context("MapEditor")]
  public class SimpleConfigConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<SimpleConfigUIRegistry>().AsSingleton();
      Bind<SimpleConfigUIDependencies>().AsSingleton();
    }
  }
}