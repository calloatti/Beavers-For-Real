using Timberborn.DropdownSystem;
using Timberborn.Modding;
using Timberborn.SingletonSystem;

namespace Calloatti.Config
{
  public class SimpleConfigUIRegistry : ILoadableSingleton
  {
    public static ModRepository ActiveModRepository { get; private set; }
    private readonly ModRepository _modRepository;

    public SimpleConfigUIRegistry(ModRepository modRepository)
    {
      _modRepository = modRepository;
    }

    public void Load()
    {
      ActiveModRepository = _modRepository;
    }
  }

  public class SimpleConfigUIDependencies : ILoadableSingleton
  {
    public static SimpleConfigUIDependencies Instance { get; private set; }
    public DropdownItemsSetter DropdownItemsSetter { get; }
    public DropdownListDrawer DropdownListDrawer { get; }

    public SimpleConfigUIDependencies(DropdownItemsSetter dropdownItemsSetter, DropdownListDrawer dropdownListDrawer)
    {
      DropdownItemsSetter = dropdownItemsSetter;
      DropdownListDrawer = dropdownListDrawer;
    }

    public void Load()
    {
      Instance = this;
    }
  }
}