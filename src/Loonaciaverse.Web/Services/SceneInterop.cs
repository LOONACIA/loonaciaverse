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
    private bool initialized;

    public event Func<string?, Task>? BodyClicked;

    [JSInvokable]
    public Task OnBodyClicked(string? id) => BodyClicked is { } handlers ? Task.WhenAll(handlers.GetInvocationList().Cast<Func<string?, Task>>().Select(handler => handler(id))) : Task.CompletedTask;

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
            initialized = await module.InvokeAsync<bool>("init", container, universe, callbacks) && !disposed;
            return initialized;
        }
        catch (JSException)
        {
            // A missing module or unavailable renderer falls back to the list home.
            initialized = false;
            return false;
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    public Task FocusAsync(string bodyId, bool immediate) => InvokeSceneAsync("focus", bodyId, immediate);

    public Task ClearFocusAsync() => InvokeSceneAsync("clearFocus");

    private async Task InvokeSceneAsync(string identifier, params object?[] arguments)
    {
        if (disposed || !initialized)
        {
            return;
        }

        await lifecycleLock.WaitAsync();
        try
        {
            if (disposed || !initialized || moduleTask is null)
            {
                return;
            }

            var module = await moduleTask;
            await module.InvokeVoidAsync(identifier, arguments);
        }
        catch (JSException)
        {
            // Visual effects must not prevent HUD updates or navigation.
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        disposed = true;
        initialized = false;
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
