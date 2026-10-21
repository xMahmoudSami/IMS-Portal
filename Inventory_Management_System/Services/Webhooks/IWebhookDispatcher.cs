namespace Inventory_Management_System.Services.Webhooks
{
    public interface IWebhookDispatcher
    {
        /// <summary>
        /// Dispatches an event to the configured webhook endpoint in a non-blocking, fault-tolerant manner.
        /// </summary>
        /// <typeparam name="T">Type of the event payload data.</typeparam>
        /// <param name="eventType">Standard event identifier (e.g. 'inventory.low_stock_alert').</param>
        /// <param name="data">The event payload data object.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if successfully dispatched, false if disabled, unconfigured, or delivery failed.</returns>
        Task<bool> DispatchAsync<T>(string eventType, T data, CancellationToken cancellationToken = default);
    }
}
