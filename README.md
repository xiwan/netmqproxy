# netmqproxy
This is just an enhancement for proxy

based on source of https://github.com/zeromq/netmq/blob/master/src/NetMQ/Proxy.cs

## How to use:

 
```
1. define your own: 
  public Action<IReceivingSocket, IOutgoingSocket> proxyBetweenAction;
  
2. pass it to constructor:

  var frontSocket = new SubscriberSocket(string.Format("@tcp://{0}", frontAddress));
  var backSocket = new PublisherSocket(string.Format("@tcp://{0}", backAddress));
  ((SubscriberSocket)frontSocket).SubscribeToAnyTopic();
  proxy1 = new NetmqProxy(frontSocket, backSocket, proxyBetweenAction);

3. that is...
```
