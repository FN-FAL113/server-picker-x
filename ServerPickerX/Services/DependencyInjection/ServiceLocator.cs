using Microsoft.Extensions.DependencyInjection;
using System;

namespace ServerPickerX.Services.DependencyInjection
{
    public static class ServiceLocator
    {
        private static IServiceProvider? _provider;

        public static void Initialize(IServiceProvider provider) => _provider = provider;

        public static T GetService<T>() where T : class
        {
            if (_provider == null)
                throw new InvalidOperationException("ServiceLocator not initialized");
        
            return _provider!.GetService<T>()!;
        }

        public static T GetRequiredService<T>() where T : class
        {
            if (_provider == null)
                throw new InvalidOperationException("ServiceLocator not initialized");
        
            return _provider!.GetRequiredService<T>()!;
        }

        
    }
}
