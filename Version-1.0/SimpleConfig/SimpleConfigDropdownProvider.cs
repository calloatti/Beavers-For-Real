using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Timberborn.DropdownSystem;
using UnityEngine;

namespace Calloatti.Config
{
  public class SimpleConfigDropdownProvider : IExtendedDropdownProvider
  {
    private readonly Action<string> _setter;
    private readonly Func<string> _getter;
    private readonly List<string> _options;

    public IReadOnlyList<string> Items => _options;

    public SimpleConfigDropdownProvider(List<string> options, Func<string> getter, Action<string> setter)
    {
      _options = options;
      _getter = getter;
      _setter = setter;
    }

    public string GetValue() => _getter();
    public void SetValue(string value) => _setter(value);
    public string FormatDisplayText(string value, bool selected) => value;
    public Sprite GetIcon(string value) => null;
    public ImmutableArray<string> GetItemClasses(string value) => ImmutableArray<string>.Empty;
  }
}