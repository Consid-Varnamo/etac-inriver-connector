using System;
using System.Collections.Generic;
using System.Linq;
using inRiver.Remoting.Connect;
using inRiver.Remoting.Extension;
using inRiver.Remoting.Log;

namespace inRiver.Connectors.EPiServer.Helpers
{
    public class ConnectorEventHelper
    {
        private static inRiverContext _context;

        public ConnectorEventHelper(inRiverContext inRiverContext)
        {
            _context = inRiverContext;
        }

        internal ConnectorEvent InitiateConnectorEvent(ConnectorEventType messageType, string message, int percentage, bool error = false)
        {
            string channelId;
            _context.Settings.TryGetValue("CHANNEL_ID", out channelId);
            ConnectorEvent connectorEvent = new ConnectorEvent
            {
                ChannelId = int.Parse(channelId ?? "0"),
                ConnectorEventType = messageType,
                ConnectorId = _context.ExtensionId,
                EventTime = DateTime.Now,
                SessionId = Guid.NewGuid(),
                Percentage = percentage,
                IsError = error,
                Message = message
            };

            _context.Log(LogLevel.Information, connectorEvent.Message);
            return connectorEvent;
        }

        internal ConnectorEvent UpdateConnectorEvent(ConnectorEvent connectorEvent, string message, int percentage, bool error = false)
        {
            if (percentage >= 0)
            {
                connectorEvent.Percentage = percentage;
            }

            connectorEvent.Message = message;
            connectorEvent.IsError = error;
            connectorEvent.EventTime = DateTime.Now;

            _context.Log(LogLevel.Information, connectorEvent.Message);

            return connectorEvent;
        }

        internal void CleanupOngoingConnectorEvents(Configuration configuration)
        {
            List<ConnectorEventSession> sessions = _context.ExtensionManager.ChannelService.GetOngoingConnectorEventSessions(null, configuration.Id);
            foreach (ConnectorEventSession connectorEventSession in sessions)
            {
                ConnectorEvent latestConnectorEvent = connectorEventSession.ConnectorEvents.First();
                ConnectorEvent connectorEvent = new ConnectorEvent
                {
                    SessionId = latestConnectorEvent.SessionId,
                    ChannelId = latestConnectorEvent.ChannelId,
                    ConnectorId = latestConnectorEvent.ConnectorId,
                    ConnectorEventType = latestConnectorEvent.ConnectorEventType,
                    Percentage = latestConnectorEvent.Percentage,
                    IsError = true,
                    Message = "Event stopped due to closedown of connector",
                    EventTime = DateTime.Now
                };

                _context.Log(LogLevel.Information, connectorEvent.Message);

            }
        }
    }
}