using Bindito.Core;

namespace Calloatti.Config
{
  [Context("MainMenu")]
  public class SimpleConfigConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<SimpleConfigUIRegistry>().AsSingleton();
      Bind<SimpleConfigUIDependencies>().AsSingleton();
    }
  }
}