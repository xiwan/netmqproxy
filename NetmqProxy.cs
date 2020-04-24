using JetBrains.Annotations;
using NetMQ;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ZinEngine.Framework
{
    // author: xiwan
    public class NetmqProxy
    {
        [NotNull] private readonly NetMQSocket m_frontend;
        [NotNull] private readonly NetMQSocket m_backend;
        [CanBeNull] private readonly NetMQSocket m_controlIn;
        [CanBeNull] private readonly NetMQSocket m_controlOut;
        [CanBeNull] private INetMQPoller m_poller;
        private readonly bool m_externalPoller;

        private int m_state = StateStopped;

        private const int StateStopped = 0;
        private const int StateStarting = 1;
        private const int StateStarted = 2;
        private const int StateStopping = 3;

        // proxybetween function 
        [CanBeNull] private readonly Action<IReceivingSocket, IOutgoingSocket> m_ProxyBetween;

        /// <summary>
        /// Create a new instance of a Proxy (NetMQ.Proxy)
        /// with the given sockets to serve as a front-end, a back-end, and a control socket.
        /// </summary>
        /// <param name="frontend">the socket that messages will be forwarded from</param>
        /// <param name="backend">the socket that messages will be forwarded to</param>
        /// <param name="controlIn">this socket will have incoming messages also sent to it - you can set this to null if not needed</param>
        /// <param name="controlOut">this socket will have outgoing messages also sent to it - you can set this to null if not needed</param>
        /// <param name="poller">an optional external poller to use within this proxy</param>
        /// <param name="proxyBetween"> user provided proxy function</param>
        public NetmqProxy([NotNull] NetMQSocket frontend, [NotNull] NetMQSocket backend, [CanBeNull] NetMQSocket controlIn, [CanBeNull] NetMQSocket controlOut, [CanBeNull] INetMQPoller poller = null, Action<IReceivingSocket, IOutgoingSocket> proxyBetween = null)
        {
            if (poller != null)
            {
                m_externalPoller = true;
                m_poller = poller;
            }

            m_frontend = frontend;
            m_backend = backend;
            m_controlIn = controlIn;
            m_controlOut = controlOut ?? controlIn;
            m_ProxyBetween = proxyBetween;
        }

        /// <summary>
        /// Create a new instance of a Proxy (NetMQ.Proxy)
        /// with the given sockets to serve as a front-end, a back-end, and a control socket.
        /// </summary>
        /// <param name="frontend">the socket that messages will be forwarded from</param>
        /// <param name="backend">the socket that messages will be forwarded to</param>
        /// <param name="control">this socket will have messages also sent to it - you can set this to null if not needed</param>
        /// <param name="poller">an optional external poller to use within this proxy</param>
        /// <exception cref="InvalidOperationException"><paramref name="poller"/> is not <c>null</c> and either <paramref name="frontend"/> or <paramref name="backend"/> are not contained within it.</exception>
        public NetmqProxy([NotNull] NetMQSocket frontend, [NotNull] NetMQSocket backend, [CanBeNull] NetMQSocket control = null, [CanBeNull] INetMQPoller poller = null)
            : this(frontend, backend, control, null, poller)
        { }

        /// <summary>
        /// A Enhanced the constructor, you could customized the proxyBetween function.
        /// </summary>
        /// <param name="frontend"></param>
        /// <param name="backend"></param>
        /// <param name="proxyBetween"></param>
        public NetmqProxy([NotNull] NetMQSocket frontend, [NotNull] NetMQSocket backend, Action<IReceivingSocket, IOutgoingSocket> proxyBetween)
            : this(frontend, backend, null, null, null, proxyBetween)
        { }


    /// <summary>
    /// Start proxying messages between the front and back ends. Blocks, unless using an external <see cref="NetMQPoller"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The proxy has already been started.</exception>
    public void Start()
        {
            if (Interlocked.CompareExchange(ref m_state, StateStarting, StateStopped) != StateStopped)
                throw new InvalidOperationException("Proxy has already been started");

            m_frontend.ReceiveReady += OnFrontendReady;
            m_backend.ReceiveReady += OnBackendReady;

            if (m_externalPoller)
            {
                m_state = StateStarted;
            }
            else
            {
                m_poller = new NetMQPoller { m_frontend, m_backend };
                m_state = StateStarted;
                m_poller.Run();
            }
        }

        /// <summary>
        /// Stops the proxy, blocking until the underlying <see cref="NetMQPoller"/> has completed.
        /// </summary>
        /// <exception cref="InvalidOperationException">The proxy has not been started.</exception>
        public void Stop()
        {
            if (Interlocked.CompareExchange(ref m_state, StateStopping, StateStarted) != StateStarted)
                throw new InvalidOperationException("Proxy has not been started");

            if (!m_externalPoller)
            {
                m_poller.Stop();
                m_poller.Dispose();
                m_poller = null;
            }

            m_frontend.ReceiveReady -= OnFrontendReady;
            m_backend.ReceiveReady -= OnBackendReady;

            m_state = StateStopped;
        }

        private void OnFrontendReady(object sender, NetMQSocketEventArgs e)
        {
            if (m_ProxyBetween == null)
                ProxyBetween(m_frontend, m_backend, m_controlIn);
            else
                m_ProxyBetween(m_frontend, m_backend);
        }

        private void OnBackendReady(object sender, NetMQSocketEventArgs e)
        {
            if (m_ProxyBetween == null)
                ProxyBetween(m_backend, m_frontend, m_controlOut);
            else
                m_ProxyBetween(m_backend, m_frontend);
        } 

        /// default proxy function
        private static void ProxyBetween(IReceivingSocket from, IOutgoingSocket to, [CanBeNull] IOutgoingSocket control)
        {
            var msg = new Msg();
            msg.InitEmpty();

            var copy = new Msg();
            copy.InitEmpty();

            while (true)
            {
                from.Receive(ref msg);
                var more = msg.HasMore;

                if (control != null)
                {
                    copy.Copy(ref msg);

                    control.Send(ref copy, more);
                }

                to.Send(ref msg, more);

                if (!more)
                    break;
            }

            copy.Close();
            msg.Close();
        }
    }
}
