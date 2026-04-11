using Bindito.Core;
using System;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;

namespace Calloatti.BeaversForReal
{
  public class BARInputService : ILoadableSingleton, IInputProcessor, IDisposable
  {
    private readonly InputService _inputService;

    public event Action OnToggleDebug;

    [Inject]
    public BARInputService(InputService inputService)
    {
      _inputService = inputService;
    }

    public void Load()
    {
      _inputService.AddInputProcessor(this);
    }

    public bool ProcessInput()
    {
      if (_inputService.IsKeyDown("Calloatti.BeaversForReal.KeyBind.Toggle"))
      {
        OnToggleDebug?.Invoke();
        return false;
      }
      return false;
    }

    public void Dispose()
    {
      _inputService.RemoveInputProcessor(this);
    }
  }
}