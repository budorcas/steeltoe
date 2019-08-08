﻿// Copyright 2017 the original author or authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Steeltoe.CircuitBreaker.Hystrix.Collapser
{
    public class CollapsedRequest<RequestResponseType, RequestArgumentType> : ICollapsedRequest<RequestResponseType, RequestArgumentType>
    {
        private readonly RequestArgumentType argument;
        private readonly CancellationToken token;
        private readonly ConcurrentQueue<CancellationToken> linkedTokens = new ConcurrentQueue<CancellationToken>();
        private RequestResponseType response;
        private Exception exception;
        private bool complete;
        private TaskCompletionSource<RequestResponseType> tcs;

        internal CollapsedRequest(RequestArgumentType arg, CancellationToken token)
        {
            argument = arg;
            this.token = token;
            tcs = null;
            response = default(RequestResponseType);
            exception = null;
            complete = false;
        }

        internal void AddLinkedToken(CancellationToken token)
        {
            linkedTokens.Enqueue(token);
        }

        internal CancellationToken Token
        {
            get { return token; }
        }

        internal TaskCompletionSource<RequestResponseType> CompletionSource
        {
            get
            {
                return tcs;
            }

            set
            {
                tcs = value;
            }
        }

        internal void SetExceptionIfResponseNotReceived(Exception e)
        {
            if (!complete)
            {
                Exception = e;
            }
        }

        internal bool IsRequestCanceled()
        {
            foreach (var linkedToken in linkedTokens)
            {
                if (!linkedToken.IsCancellationRequested)
                {
                    return false;
                }
            }

            // All linked tokens have been cancelled
            return Token.IsCancellationRequested;
        }

        internal Exception SetExceptionIfResponseNotReceived(Exception e, string exceptionMessage)
        {
            Exception newException = e;

            if (!complete)
            {
                if (e == null)
                {
                    newException = new InvalidOperationException(exceptionMessage);
                }

                SetExceptionIfResponseNotReceived(newException);
            }

            // return any exception that was generated
            return newException;
        }

        #region ICollapsedRequest
        public RequestArgumentType Argument
        {
            get
            {
                return argument;
            }
        }

        public bool Complete
        {
            get
            {
                return complete;
            }

            set
            {
                complete = value;
                if (!tcs.Task.IsCompleted)
                {
                    Response = default(RequestResponseType);
                }
            }
        }

        public Exception Exception
        {
            get
            {
                return exception;
            }

            set
            {
                exception = value;
                complete = true;
                if (!tcs.TrySetException(value))
                {
                    throw new InvalidOperationException("Task has already terminated so exectpion can not be set : " + value);
                }
            }
        }

        public RequestResponseType Response
        {
            get
            {
                return response;
            }

            set
            {
                response = value;
                complete = true;
                if (!tcs.TrySetResult(value))
                {
                    throw new InvalidOperationException("Task has already terminated so response can not be set : " + value);
                }
            }
        }

        #endregion ICollapsedRequest
    }
}
