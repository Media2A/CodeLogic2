namespace CodeLogic.Abstractions;

/// <summary>
/// Interface for libraries that need to hook into application lifecycle events.
/// Libraries implementing this interface can perform actions during application startup and shutdown.
/// </summary>
public interface IApplicationLifecycle
{
    /// <summary>
    /// Called when the application is starting up, before services are fully configured.
    /// Use this for early initialization tasks.
    /// </summary>
    /// <param name="services">Service provider for accessing registered services</param>
    Task OnApplicationStartingAsync(IServiceProvider services);

    /// <summary>
    /// Called when the application has fully started and is ready to serve requests.
    /// Use this for final initialization tasks that depend on all services being available.
    /// </summary>
    /// <param name="services">Service provider for accessing registered services</param>
    Task OnApplicationStartedAsync(IServiceProvider services);

    /// <summary>
    /// Called when the application is beginning to shut down.
    /// Use this for cleanup tasks that need to happen before services are disposed.
    /// </summary>
    Task OnApplicationStoppingAsync();

    /// <summary>
    /// Called when the application has fully stopped.
    /// Use this for final cleanup tasks.
    /// </summary>
    Task OnApplicationStoppedAsync();
}
