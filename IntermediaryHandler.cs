using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NetMQ;

namespace ZinEngine.Framework
{
    public static class IntermediaryHandler
    {
        // 默认随机分发模式
        static readonly bool _randomSendMode = true;
        static ConcurrentDictionary<string, string> proxyCollection = new ConcurrentDictionary<string, string>();

        public static void ReceiveReadyAction(this IntermediaryServer server, object sender, NetMQQueueEventArgs<IntermediaryMsg> e)
        {
            try
            {
                var Item = e.Queue.Dequeue();
                var msg = Item.Msg;
                var from = Item.From;
                var to = Item.To;

                var more = msg.HasMore;
                if (more)
                {
                    var topic = Encoding.Default.GetString(msg.Data);
                    var head = topic.SplitTrim(":")[0];
                    var host = topic.SplitTrim(":")[1];
                    // random dispatch
                    if (string.Equals(head, TopicType.SVRR))
                    {
                        // 随机分发
                        if (_randomSendMode && proxyCollection.Count > 0)
                        {
                            var newTopic = proxyCollection.Values.Shuffle().Get(0);
                            var data = Encoding.Default.GetBytes(newTopic);
                            to.SendFrame(data, more);
                            return;
                        }
                    }
                    // heartbeat logic
                    else if (string.Equals(head, TopicType.ONLINE))
                    {
                        var proxy = $"{TopicType.SVRR}:{host}";
                        proxyCollection.AddOrUpdate(host, proxy, (k, v) => v);
                    }
                    // offline logic
                    else if (string.Equals(head, TopicType.OFFLINE))
                    {
                        proxyCollection.Remove(host);
                    }
                }
                // send frame
                to.Send(ref msg, more);
                msg.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IntermediaryHandler-ReceiveReadyAction {ex.Message}");
            }
        }

        // custom proxy function
        public static void ProxyBetweenAction(this IntermediaryServer server, IReceivingSocket from, IOutgoingSocket to)
        {
            var msg = new Msg();
            msg.InitEmpty();

            while (true)
            {
                from.Receive(ref msg);
                var more = msg.HasMore;

                server.queue.Enqueue(new IntermediaryMsg
                {
                    Msg = msg,
                    From = from,
                    To = to
                });
                if (!more)
                    break;
            }
        }
    }
}
