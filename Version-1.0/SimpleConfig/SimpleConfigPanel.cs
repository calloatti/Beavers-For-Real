using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace Calloatti.Config
{
  public class SimpleConfigPanel : IPanelController
  {
    private readonly PanelStack _panelStack;
    private readonly VisualElement _root;

    public SimpleConfigPanel(PanelStack panelStack)
    {
      _panelStack = panelStack;
      _root = SimpleConfigUIBuilder.BuildConfigurationOverlay(OnUICancelled);
    }

    public VisualElement GetPanel() => _root;

    public bool OnUIConfirmed() => false;

    public void OnUICancelled()
    {
      _panelStack.Pop(this);
    }
  }
}