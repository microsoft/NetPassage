# Node Passage


This is a prototype for building the Net Passage idea in Node. This has not been fully tested and will not work for all scenarios.

This sample uses Azure Relay Hybric Connections and creates a listener to the Relay.
Requests that are received are forwarded to the local target server. All responses from
the local target server will be returned to the relay response.

## Install

- Install ['Node'](https://nodejs.org/en/download/)
- Install the pacakges - npm install 

## Usage

- Copy the .env.template file and rename it to '.env'
- Update the values in the .env file with the settings from your relay
- Run the following command:

`npm run start`