using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ContosoDashboard.Services.Documents
{
    /// <summary>
    /// SignalR hub for broadcasting real-time document scan status updates to connected clients.
    /// Connected clients receive notifications when documents complete virus scanning or threats are detected.
    /// </summary>
    public class DocumentNotificationHub : Hub
    {
        private readonly ILogger<DocumentNotificationHub> _logger;

        public DocumentNotificationHub(ILogger<DocumentNotificationHub> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Called when a client connects to the hub.
        /// Logs the connection and optionally joins user-specific groups.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"Client connected: {Context.ConnectionId}");
            
            // Get user ID from HTTP context if available
            var userIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                // Add user to a group for targeted notifications
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
                _logger.LogInformation($"Client {Context.ConnectionId} joined group user_{userId}");
            }

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a client disconnects from the hub.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogWarning($"Client {Context.ConnectionId} disconnected with error: {exception.Message}");
            }
            else
            {
                _logger.LogInformation($"Client {Context.ConnectionId} disconnected");
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Subscribe to document scan updates for a specific document.
        /// Called by client when a user is viewing a document's status.
        /// </summary>
        public async Task SubscribeToDocumentUpdates(int documentId)
        {
            var groupName = $"document_{documentId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"Client {Context.ConnectionId} subscribed to document {documentId}");
            
            // Notify client that subscription was successful
            await Clients.Caller.SendAsync("SubscriptionConfirmed", new { documentId = documentId });
        }

        /// <summary>
        /// Unsubscribe from document scan updates.
        /// Called when user navigates away from document status view.
        /// </summary>
        public async Task UnsubscribeFromDocumentUpdates(int documentId)
        {
            var groupName = $"document_{documentId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"Client {Context.ConnectionId} unsubscribed from document {documentId}");
        }

        /// <summary>
        /// Ping method to keep connection alive and check if hub is responsive.
        /// Clients can call this periodically to detect disconnections.
        /// </summary>
        public async Task Ping()
        {
            await Clients.Caller.SendAsync("Pong");
        }
    }

    /// <summary>
    /// Service for sending document-related notifications through SignalR.
    /// Called by DocumentScanningHostedService when scans complete.
    /// </summary>
    public class DocumentNotificationService
    {
        private readonly IHubContext<DocumentNotificationHub> _hubContext;
        private readonly ILogger<DocumentNotificationService> _logger;

        public DocumentNotificationService(
            IHubContext<DocumentNotificationHub> hubContext,
            ILogger<DocumentNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// Notify that a document has completed scanning and is ready (no threats found).
        /// </summary>
        public async Task NotifyDocumentReadyAsync(int documentId, int userId, string fileName)
        {
            try
            {
                var groupName = $"document_{documentId}";
                var userGroupName = $"user_{userId}";

                var notification = new
                {
                    documentId = documentId,
                    status = "Clear",
                    fileName = fileName,
                    timestamp = DateTime.UtcNow,
                    message = $"'{fileName}' has been scanned and is ready for download"
                };

                // Send to document-specific subscribers
                await _hubContext.Clients.Group(groupName)
                    .SendAsync("DocumentReady", notification);

                // Send to user group for dashboard notification
                await _hubContext.Clients.Group(userGroupName)
                    .SendAsync("DocumentStatusChanged", notification);

                _logger.LogInformation($"DocumentReady notification sent for document {documentId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending DocumentReady notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Notify that a document has been quarantined due to threat detection.
        /// </summary>
        public async Task NotifyDocumentQuarantinedAsync(int documentId, int userId, string fileName, string threatType)
        {
            try
            {
                var groupName = $"document_{documentId}";
                var userGroupName = $"user_{userId}";

                var notification = new
                {
                    documentId = documentId,
                    status = "Quarantined",
                    fileName = fileName,
                    threatType = threatType,
                    timestamp = DateTime.UtcNow,
                    message = $"'{fileName}' contains a threat ({threatType}) and has been quarantined"
                };

                // Send to document-specific subscribers
                await _hubContext.Clients.Group(groupName)
                    .SendAsync("DocumentQuarantined", notification);

                // Send to user group for dashboard notification
                await _hubContext.Clients.Group(userGroupName)
                    .SendAsync("DocumentStatusChanged", notification);

                _logger.LogInformation($"DocumentQuarantined notification sent for document {documentId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending DocumentQuarantined notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Notify that a document scan has started.
        /// </summary>
        public async Task NotifyDocumentScanningAsync(int documentId, int userId, string fileName)
        {
            try
            {
                var groupName = $"document_{documentId}";

                var notification = new
                {
                    documentId = documentId,
                    status = "Scanning",
                    fileName = fileName,
                    timestamp = DateTime.UtcNow,
                    message = $"Scanning '{fileName}'..."
                };

                await _hubContext.Clients.Group(groupName)
                    .SendAsync("DocumentScanning", notification);

                _logger.LogInformation($"DocumentScanning notification sent for document {documentId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending DocumentScanning notification: {ex.Message}");
            }
        }
    }
}
