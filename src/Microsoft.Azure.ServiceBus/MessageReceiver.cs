﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Amqp;

    public abstract class MessageReceiver : ClientEntity
    {
        readonly TimeSpan operationTimeout;
        int prefetchCount;

        protected MessageReceiver(ReceiveMode receiveMode, TimeSpan operationTimeout)
            : base(nameof(MessageReceiver) + StringUtility.GetRandomString())
        {
            this.ReceiveMode = receiveMode;
            this.operationTimeout = operationTimeout;
        }

        public abstract string Path { get; }

        public ReceiveMode ReceiveMode { get; protected set; }

        public virtual int PrefetchCount
        {
            get
            {
                return this.prefetchCount;
            }

            set
            {
                if (value < 0)
                {
                    throw Fx.Exception.ArgumentOutOfRange(nameof(this.PrefetchCount), value, "Value must be greater than 0");
                }

                this.prefetchCount = value;
            }
        }

        internal TimeSpan OperationTimeout
        {
            get { return this.operationTimeout; }
        }

        protected MessagingEntityType EntityType { get; set; }

        public async Task<BrokeredMessage> ReceiveAsync()
        {
            IList<BrokeredMessage> messages = await this.ReceiveAsync(1).ConfigureAwait(false);
            if (messages != null && messages.Count > 0)
            {
                return messages[0];
            }

            return null;
        }

        public async Task<IList<BrokeredMessage>> ReceiveAsync(int maxMessageCount)
        {
            MessagingEventSource.Log.MessageReceiveStart(this.ClientId, maxMessageCount);

            IList<BrokeredMessage> messages = null;
            try
            {
                 messages = await this.OnReceiveAsync(maxMessageCount).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageReceiveException(this.ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageReceiveStop(this.ClientId);
            return messages;
        }

        public async Task<IList<BrokeredMessage>> ReceiveBySequenceNumberAsync(IEnumerable<long> sequenceNumbers)
        {
            this.ThrowIfNotPeekLockMode();
            int count = MessageReceiver.ValidateSequenceNumbers(sequenceNumbers);

            MessagingEventSource.Log.MessageReceiveBySequenceNumberStart(this.ClientId, count, sequenceNumbers);

            IList<BrokeredMessage> messages = null;
            try
            {
                messages = await this.OnReceiveBySequenceNumberAsync(sequenceNumbers).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageReceiveBySequenceNumberException(this.ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageReceiveBySequenceNumberStop(this.ClientId);

            return messages;
        }

        public async Task CompleteAsync(IEnumerable<Guid> lockTokens)
        {
            this.ThrowIfNotPeekLockMode();
            int count = MessageReceiver.ValidateLockTokens(lockTokens);

            MessagingEventSource.Log.MessageCompleteStart(this.ClientId, count, lockTokens);

            try
            {
                await this.OnCompleteAsync(lockTokens).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageCompleteException(this.ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageCompleteStop(this.ClientId);
        }

        public async Task AbandonAsync(IEnumerable<Guid> lockTokens)
        {
            this.ThrowIfNotPeekLockMode();
            int count = MessageReceiver.ValidateLockTokens(lockTokens);

            MessagingEventSource.Log.MessageAbandonStart(this.ClientId, count, lockTokens);
            try
            {
                await this.OnAbandonAsync(lockTokens).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageAbandonException(this.ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageAbandonStop(this.ClientId);
        }

        public async Task DeferAsync(IEnumerable<Guid> lockTokens)
        {
            this.ThrowIfNotPeekLockMode();
            int count = MessageReceiver.ValidateLockTokens(lockTokens);

            MessagingEventSource.Log.MessageDeferStart(this.ClientId, count, lockTokens);

            try
            {
                await this.OnDeferAsync(lockTokens).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageDeferException(this.ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageDeferStop(this.ClientId);
        }

        public async Task DeadLetterAsync(IEnumerable<Guid> lockTokens)
        {
            this.ThrowIfNotPeekLockMode();
            int count = MessageReceiver.ValidateLockTokens(lockTokens);

            MessagingEventSource.Log.MessageDeadLetterStart(this.ClientId, count, lockTokens);

            try
            {
                await this.OnDeadLetterAsync(lockTokens).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageDeadLetterException(this.ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageDeadLetterStop(this.ClientId);
        }

        public async Task<DateTime> RenewLockAsync(Guid lockToken)
        {
            this.ThrowIfNotPeekLockMode();
            int count = MessageReceiver.ValidateLockTokens(new Guid[] { lockToken });

            MessagingEventSource.Log.MessageRenewLockStart(this.ClientId, 1, new Guid[] { lockToken });

            DateTime lockedUntilUtc = DateTime.MinValue;
            try
            {
                lockedUntilUtc = await this.OnRenewLockAsync(lockToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.MessageRenewLockException(this.ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.MessageRenewLockStop(this.ClientId);
            return lockedUntilUtc;
        }

        internal Task<AmqpResponseMessage> ExecuteRequestResponseAsync(AmqpRequestMessage amqpRequestMessage)
        {
            return this.OnExecuteRequestResponseAsync(amqpRequestMessage);
        }

        protected abstract Task<IList<BrokeredMessage>> OnReceiveAsync(int maxMessageCount);

        protected abstract Task<IList<BrokeredMessage>> OnReceiveBySequenceNumberAsync(IEnumerable<long> sequenceNumbers);

        protected abstract Task OnCompleteAsync(IEnumerable<Guid> lockTokens);

        protected abstract Task OnAbandonAsync(IEnumerable<Guid> lockTokens);

        protected abstract Task OnDeferAsync(IEnumerable<Guid> lockTokens);

        protected abstract Task OnDeadLetterAsync(IEnumerable<Guid> lockTokens);

        protected abstract Task<DateTime> OnRenewLockAsync(Guid lockToken);

        protected abstract Task<AmqpResponseMessage> OnExecuteRequestResponseAsync(AmqpRequestMessage requestAmqpMessage);

        static int ValidateLockTokens(IEnumerable<Guid> lockTokens)
        {
            int count;
            if (lockTokens == null || (count = lockTokens.Count()) == 0)
            {
                throw Fx.Exception.ArgumentNull(nameof(lockTokens));
            }

            return count;
        }

        static int ValidateSequenceNumbers(IEnumerable<long> sequenceNumbers)
        {
            int count;
            if (sequenceNumbers == null || (count = sequenceNumbers.Count()) == 0)
            {
                throw Fx.Exception.ArgumentNull(nameof(sequenceNumbers));
            }

            return count;
        }

        void ThrowIfNotPeekLockMode()
        {
            if (this.ReceiveMode != ReceiveMode.PeekLock)
            {
                throw Fx.Exception.AsError(new InvalidOperationException("The operation is only supported in 'PeekLock' receive mode."));
            }
        }
    }
}