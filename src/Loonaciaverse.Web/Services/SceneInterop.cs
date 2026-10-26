using Loonaciaverse.Core.Content;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Loonaciaverse.Web.Services;

public sealed class SceneInterop(IJSRuntime js) : IAsyncDisposable
{
    private readonly SemaphoreSlim lifecycleLock = new(1, 1);
    private Task<IJSObjectReference>? moduleTask;
    private DotNetObjectReference<SceneInterop>? callbacks;
    private Task? disposeTask;
    private bool disposed;

    public async Task<bool> InitAsync(ElementReference container, Universe universe)
    {
        await lifecycleLock.WaitAsync();
        try
        {
            if (disposed)
            {
                return false;
            }

            var module = await (moduleTask ??= js.InvokeAsync<IJSObjectReference>("import", "./js/scene.js").AsTask());
            if (disposed)
            {
                return false;
            }

            callbacks ??= DotNetObjectReference.Create(this);
            var ready = await module.InvokeAsync<bool>("init", container, universe, callbacks);
            return ready && !disposed;
        }
        catch (JSException)
        {
            // A missing module or unavailable renderer falls back to the list home.
            return false;
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        disposed = true;
        return new ValueTask(disposeTask ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        await lifecycleLock.WaitAsync();
        try
        {
            if (moduleTask is not null)
            {
                var module = await moduleTask;
                try
                {
                    await module.InvokeVoidAsync("dispose");
                }
                finally
                {
                    await module.DisposeAsync();
                }
            }
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
            // A failed import or disconnected runtime needs no further JS cleanup.
        }
        finally
        {
            callbacks?.Dispose();
            callbacks = null;
            moduleTask = null;
            lifecycleLock.Release();
        }
    }
}
