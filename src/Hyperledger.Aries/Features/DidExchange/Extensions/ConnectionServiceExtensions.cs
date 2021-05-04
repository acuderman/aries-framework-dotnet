﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Features.IssueCredential;
using Hyperledger.Aries.Storage;
using Hyperledger.Aries.Utils;

namespace Hyperledger.Aries.Features.DidExchange
{
    /// <summary>
    /// A collection of convenience methods for the <see cref="ICredentialService"/> class.
    /// </summary>
    public static class ConnectionServiceExtensions
    {
        /// <summary>
        /// Retrieves a list of <see cref="ConnectionRecord"/> that are in <see cref="ConnectionState.Negotiating"/> state.
        /// </summary>
        /// <returns>The negotiating connections async.</returns>
        /// <param name="connectionService">Connection service.</param>
        /// <param name="agentContext">Agent Context.</param>
        /// <param name="count">Count.</param>
        public static Task<List<ConnectionRecord>> ListNegotiatingConnectionsAsync(
            this IConnectionService connectionService, IAgentContext agentContext, int count = 100)
            => connectionService.ListAsync(agentContext,
                SearchQuery.Equal(nameof(ConnectionRecord.State), ConnectionState.Negotiating.ToString("G")), count);

        /// <summary>
        /// Retrieves a list of <see cref="ConnectionRecord"/> that are in <see cref="ConnectionState.Connected"/> state.
        /// </summary>
        /// <returns>The connected connections async.</returns>
        /// <param name="connectionService">Connection service.</param>
        /// <param name="agentContext">Agent Context.</param>
        /// <param name="count">Count.</param>
        public static Task<List<ConnectionRecord>> ListConnectedConnectionsAsync(
            this IConnectionService connectionService, IAgentContext agentContext, int count = 100)
            => connectionService.ListAsync(agentContext,
                SearchQuery.Equal(nameof(ConnectionRecord.State), ConnectionState.Connected.ToString("G")), count);

        /// <summary>
        /// Retrieves a list of <see cref="ConnectionRecord"/> that are in <see cref="ConnectionState.Invited"/> state.
        /// </summary>
        /// <returns>The invited connections async.</returns>
        /// <param name="connectionService">Connection service.</param>
        /// <param name="agentContext">Agent Context.</param>
        /// <param name="count">Count.</param>
        public static Task<List<ConnectionRecord>> ListInvitedConnectionsAsync(
            this IConnectionService connectionService, IAgentContext agentContext, int count = 100)
            => connectionService.ListAsync(agentContext,
                SearchQuery.And(SearchQuery.Equal(nameof(ConnectionRecord.State), ConnectionState.Invited.ToString("G")),
                                SearchQuery.Equal(nameof(ConnectionRecord.MultiPartyInvitation), false.ToString())), count);

        /// <summary>
        /// Retrieves a list of <see cref="ConnectionRecord"/> that are multi-party invitations.
        /// </summary>
        /// <returns>The invited connections async.</returns>
        /// <param name="connectionService">Connection service.</param>
        /// <param name="agentContext">Agent Context.</param>
        /// <param name="count">Count.</param>
        public static Task<List<ConnectionRecord>> ListMultiPartyInvitationsAsync(
            this IConnectionService connectionService, IAgentContext agentContext, int count = 100)
            => connectionService.ListAsync(agentContext,
                SearchQuery.And(SearchQuery.Equal(nameof(ConnectionRecord.State), ConnectionState.Invited.ToString("G")),
                    SearchQuery.Equal(nameof(ConnectionRecord.MultiPartyInvitation), true.ToString())), count);

        /// <summary>
        /// Retrieves a <see cref="ConnectionRecord"/> by key.
        /// </summary>
        /// <returns>The connection record.</returns>
        /// <param name="connectionService">Connection service.</param>
        /// <param name="agentContext">Agent Context.</param>
        /// <param name="myKey">My key.</param>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="AriesFrameworkException">Throw with error code 'RecordNotFound' if a connection record for the key was not found</exception>
        public static async Task<ConnectionRecord> ResolveByMyKeyAsync(
            this IConnectionService connectionService, IAgentContext agentContext, string myKey)
        {
            if (string.IsNullOrEmpty(myKey))
                throw new ArgumentNullException(nameof(myKey));

            if (agentContext == null)
                throw new ArgumentNullException(nameof(agentContext));

            var stopwatc = Stopwatch.StartNew();

            var firstQuery = (await connectionService.ListAsync(agentContext,
                    SearchQuery.Equal(nameof(ConnectionRecord.MyVk), myKey), 5))
                .SingleOrDefault();

            Console.WriteLine("First connections query ms: " + stopwatc.ElapsedMilliseconds);

            stopwatc.Restart();

            ConnectionRecord secondQuery = null;

            if (firstQuery == null)
            {
                secondQuery = (await connectionService.ListAsync(agentContext,
                        SearchQuery.And(
                            SearchQuery.Equal(nameof(ConnectionRecord.MultiPartyInvitation), "True"),
                            SearchQuery.Equal(TagConstants.ConnectionKey, myKey)
                            ), 5))
                    .SingleOrDefault();

                Console.WriteLine("Second connections query ms: " + stopwatc.ElapsedMilliseconds);
                stopwatc.Restart();
            }

            ConnectionRecord thirdQuery = null;

            if (firstQuery == null && secondQuery == null)
            {
                thirdQuery = (await connectionService.ListAsync(agentContext,
                        SearchQuery.Equal(TagConstants.ConnectionKey, myKey), 5))
                    .SingleOrDefault();

                Console.WriteLine("Third connections query ms: " + stopwatc.ElapsedMilliseconds);
                stopwatc.Stop();
            }

            var record =
                // Check if key is part of a connection
                firstQuery

                // Check if key is part of a multiparty invitation
                ?? secondQuery

                // Check if key is part of a single party invitation
                ?? thirdQuery;

            // Connection can be null if protocol uses connectionless transport
            //if (record == null)
            //    throw new AgentFrameworkException(ErrorCode.RecordNotFound, $"Connection Record not found for key {myKey}");

            return record;
        }

        /// <summary>
        /// Retrieves a connection record by its thread id.
        /// </summary>
        /// <param name="connectionService">Connection service.</param>
        /// <param name="context">The context.</param>
        /// <param name="threadId">The thread id.</param>
        /// <returns>The connection record.</returns>
        public static async Task<ConnectionRecord> GetByThreadIdAsync(
            this IConnectionService connectionService, IAgentContext context, string threadId)
        {
            var search = await connectionService.ListAsync(context, SearchQuery.Equal(TagConstants.LastThreadId, threadId), 1);

            if (search.Count == 0)
                throw new AriesFrameworkException(ErrorCode.RecordNotFound, $"Connection record not found by thread id : {threadId}");

            if (search.Count > 1)
                throw new AriesFrameworkException(ErrorCode.RecordInInvalidState, $"Multiple connection records found by thread id : {threadId}");

            return search.Single();
        }
    }
}
