using System;
using System.Collections.Generic;
using Dynamologio.App.ViewModels;

namespace Dynamologio.App.Navigation
{
    /// <summary>
    /// Explicit delegate factory. No reflection. No service locator.
    /// Creates feature ViewModels on demand using predefined delegates.
    /// </summary>
    public sealed class ViewModelFactory : IViewModelFactory
    {
        private readonly IReadOnlyDictionary<NavigationSection, Func<ViewModelBase>> _factories;

        public ViewModelFactory(IReadOnlyDictionary<NavigationSection, Func<ViewModelBase>> factories)
        {
            _factories = factories ?? throw new ArgumentNullException(nameof(factories));
        }

        public ViewModelBase Create(NavigationSection section)
        {
            if (_factories.TryGetValue(section, out var factory))
            {
                return factory();
            }
            throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown navigation section.");
        }
    }
}
