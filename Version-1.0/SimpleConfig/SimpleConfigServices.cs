using Timberborn.DropdownSystem;
using Timberborn.Modding;
using Timberborn.SingletonSystem;
using Timberborn.Localization;

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
    public ILoc Loc { get; }

    public SimpleConfigUIDependencies(DropdownItemsSetter dropdownItemsSetter, DropdownListDrawer dropdownListDrawer, ILoc loc)
    {
      DropdownItemsSetter = dropdownItemsSetter;
      DropdownListDrawer = dropdownListDrawer;
      Loc = loc;

      // Assign the static instance immediately upon instantiation, 
      // preventing race conditions with Harmony UI patches.
      Instance = this;
    }

    public void Load()
    {
      // No-op: instance assignment moved to constructor
    }
  }
}