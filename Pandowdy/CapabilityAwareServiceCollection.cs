// // Copyright 2026 Mark D. Long
// // Licensed under the Apache License, Version 2.0
// // See LICENSE file for details
//
//

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Pandowdy.EmuCore;
using System.Collections;

using Pandowdy.EmuCore.Machine;
namespace Pandowdy;

internal class CapabilityAwareServiceCollection(IServiceCollection inner) : IServiceCollection
{
    private readonly IServiceCollection _inner = inner;

    public void Add(ServiceDescriptor descriptor)
    {
        RegisterCapabilities(descriptor);
        _inner.Add(descriptor);
    }

    private void RegisterCapabilities(ServiceDescriptor descriptor)
    {
        var implType = descriptor.ImplementationType;
        if (implType == null)
        {
            return;
        }

        foreach (var cap in implType.GetCustomAttributes<CapabilityAttribute>())
        {
            // Forward to the existing registration so DI resolves the SAME singleton
            // instance, not a duplicate. Without this, each capability registration
            // would create a separate singleton with its own dependency graph.
            var serviceType = descriptor.ServiceType;
            _inner.Add(new ServiceDescriptor(
                cap.InterfaceType,
                sp => sp.GetRequiredService(serviceType),
                descriptor.Lifetime));
        }
    }

    // Forward all other IServiceCollection members
    public IEnumerator<ServiceDescriptor> GetEnumerator() => _inner.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void Clear() => _inner.Clear();
    public bool Contains(ServiceDescriptor item) => _inner.Contains(item);
    public void CopyTo(ServiceDescriptor[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
    public bool Remove(ServiceDescriptor item) => _inner.Remove(item);
    public int Count => _inner.Count;
    public bool IsReadOnly => _inner.IsReadOnly;
    public int IndexOf(ServiceDescriptor item) => _inner.IndexOf(item);
    public void Insert(int index, ServiceDescriptor item) => _inner.Insert(index, item);
    public void RemoveAt(int index) => _inner.RemoveAt(index);
    public ServiceDescriptor this[int index] { get => _inner[index]; set => _inner[index] = value; }
}
