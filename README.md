# Overview

This repository contains DotNet and NodeJs (both Typescript and Javascript) packages and samples for the Microsoft **NetPassage** tunneling utility that leverages Hybrid Connections feature of the Microsoft Azure Relay to establish bi-directional, binary stream communication between two networked applications, whereby either or both of the parties can reside behind NATs or Firewalls. **NetPassage** supports HTTP(S) and WebSockets. **NetPassage** is a great alternative to ngrok.io tunneling service.

**NetPassage** supports network load balancing without the need of additional appliance. As the Relay resides at the cloud environment, we can have multiple listeners and the Network will be load balanced based on round robin fashion and you get a secured connectivity without requiring any external VPN.

**NetPassage** supports cross platform deployment which includes Windows, Mac and Linux.

**NetPassage** allows a publicly discoverable and reachable WebSocket server to be hosted on any machine that has outbound access to the Internet, and specifically to the Microsoft Azure Relay service in the chosen region, via HTTPS port 443.

It is useful for debug scenarios or for more complex situations where the BotEmulator is not enough (i.e.: you use the WebChat control hosted on a site and you need to receive ChannelData in your requests).

The WebSocket server code for NodeJS version **NetPassage** will look instantly familiar as it is directly based on and integrated with two of the most popular existing WebSocket packages in the Node universe: **ws** and **websocket**.

As you create a WebSocket server, the server will not listen on a TCP port on the local network, but rather delegate listening to a configured Hybrid Connection path the Azure Relay service in Service Bus. The delegation happens by ways of opening and maintaining a "control connection" WebSocket that remains opened and reconnects automatically when dropped inadvertently. This listener connection is automatically TLS/SSL protected without you having to juggle any certificates.

Up to 25 WebSocket listeners can listen concurrently on the same Hybrid Connection path on the Relay; if two or more listeners are connected, the service will automatically balance incoming connection requests across the connected listeners which also provides an easy failover capability. You don't have to do anything to enable this, just have multiple listeners share the same path.

When you start `NetPassage`, it will display a UI in your terminal with the public URL of your tunnel and other status and metrics information about connections made over your tunnel.

![UI Terminal](docs/images/WebSocketConsole.png)

## Architecture

`NetPassage` uses Microsoft Azure Service Bus Relay to tunnel all incoming
messages thru the Relay's hybrid connections (either Websocket or Http) and to
the remotely running (e.g. local) `NetPassage` client utility's listener, as
shown in the architecture diagram below:

![Architecture](docs/images/passage.png)

## How to configure and run the utility

The `NetPassage` utility is constructed from the following parts:

1. NetPassage client console app
2. Microsoft.HybridConnections.Core - the library containing the common modules and services used by NetPassage

### Building with Microsoft Visual Studio 2019 or higher

1. Once the github repository has been cloned or forked to your machine, open the `NetPassage` solution in Visual Studio.

2. In Solution Explorer, expand the **NetPassage** folder.

3. Clone the **NetPassage.json.template** file into **NetPassage.json** and replace the following values with those from your Azure Relay settings.

    a. `Namespace` is the name of your Azure Relay service.
    >Note: Depending on the number of connections you might have, the following settings in the **ConnectionSettings** section should be completed for each individual hybrid connection.

    b.`HybridConnection` is the name of the Hybrid Connection.
    c. `PolicyName` is the name of the shared access policy.
    d. `PolicyKey` is the secret key value for the shared access policy.
    e. `TargetHttp` is the localhost url address to your local service (e.g. Bot, web app, etc...) The address and port number should match the address and port used by your local client. For example, `http://localhost:[PORT]`.

4. Clone the **appsettings.json.template** file into **appsettings.json** and change the value of the `Verbose` in the `Log` section to be either `false` or `true` if you want to have a verbose output of all outgoing message. The latter is helpful only for troubleshooting and debugging purposes.

5. Before building the solution, please make sure to change the `Build Action` for `NetPassage.json` file to `Content`.

Then, before running `NetPassage` with your Bot application that installed, for example, within the Microsoft Teams application, make sure to update the Bot's `Messaging Endpoint` to your `NetPassage` Hybrid Connection Url. To do so, follow these steps:

1. Login to the Azure portal and open your Azure Bot resource.

2. Select **Configuration** under `Settings` to open the Azure Bot Configuration settings.

3. In the **Messaging endpoint** field, enter the hybrid connection url as follows: `https://<your relay namespace>.servicebus.windows.net/<your hybrid connection name>`
4. Append **/api/messages** to the end to create the full endpoint to be used. For example, `https://example-service-bus.servicebus.windows.net/websocketrelay/api/messages`.
5. Click **Save** when completed.

Now, back to the Visual Studio.

1. In Visual Studio, Build the solution. Make sure to select `NetPassage` project as your startup project if you want to run it directly from the Visual Studio. Alternatively, you can open your Command Console or PowerShell console, navigate to the solution binary directory and execute `Netpassage.exe` executable file.

2. Open and run your locally hosted bot.

3. Test your bot on a channel (Test in Web Chat, Skype, Teams, etc.). User data is captured and logged as activity occurs.

    - When using the Bot Framework Emulator: The endpoint entered in Emulator must be the service bus endpoint saved in your Azure Web Bot **Settings** blade, under **Messaging Endpoint**.

### Building with Visual Studio for Mac

When building the solution on Mac, the steps are largely the same as shown above with a couple things to keep note of:

1. The **appsettings.json** file may not automatically get moved into **/bin** after building by default. It is required, so ensure it is being properly copied over during build if you run into issues.

2. The `NetPassage` default run configuration may not pass in the required configuration file by default. To fix this, ensure "**NetPassage.json**" is being passed into your default Run Configuration.

    a. Right click the project folder in Visual Studio for Mac and select **Options**.

    b. Under **Run**, select the **Default** configuration.

    c. In the **Arguments** field, enter "**NetPassage.json**".

    d. Select **OK** and retry running `NetPassage` via Visual Studio for Mac.


### Note about response message headers

By default, NetPassage adds the following header to all response messages in order to support rendering web page responses: `Content-Type: "text/html; charset=UTF-8"`.

This may impact scenarios where the client may want to instead return other content-types (e.g. pure JSON documents). 

## Node Passage

This is a prototype for building the Net Passage idea in Node. This has not been fully tested and will not work for all scenarios.

This sample uses Azure Relay Hybric Connections and creates a listener to the Relay.
Requests that are received are forwarded to the local target server. All responses from
the local target server will be returned to the relay response.

### Install

- Install ['Node'](https://nodejs.org/en/download/)
- Install the pacakges - npm install 

### Usage

- Copy the .env.template file and rename it to '.env'
- Update the values in the .env file with the settings from your relay
- Run the following command:

`npm run start`

## Acknowledgments

Part of this code is based on the work that [Gabo Gilabert](https://github.com/gabog) did in his project [here](https://github.com/gabog/AzureServiceBusBotRelay).


