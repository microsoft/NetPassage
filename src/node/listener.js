const https = require('hyco-https')
const axios = require('axios');
const path = require('path');
var colors = require('colors');

colors.enable();

const ENV_FILE = path.join(__dirname, '.env');
require('dotenv').config({ path: ENV_FILE });

if (process.env.NODE_ENV === 'development') {
    const httpsAgent = new https.Agent({
        rejectUnauthorized: false,
    })
    axios.defaults.httpsAgent = httpsAgent
    // eslint-disable-next-line no-console
    console.log(process.env.NODE_ENV, `RejectUnauthorized is disabled.`)
}

var azureRelayConfig = {
    namespace: process.env.namespace,
    path: process.env.hybridConnectionPath,
    policyName: process.env.policyName,
    policyKey: process.env.policyKey,
    targetHttp: process.env.targetHttp
};
  
// used to forward request to local server
const postData = async (url, data, headers) => {
    try {
        return await axios.post(url, data, { headers });
    } catch (e) {
        //console.error(e);
    }
};

// used to forward request to local server
// used for HTML Get requests
const getData = async (url, headers) => {
    return await axios.get(url, { headers });
};

const defaultLogs = [];

const writeDefaultLogs = (message) => {
    for (let i = 0; i < defaultLogs.length; i++) {
        console.log(defaultLogs[i]);
    }
}

const getLogMessage = (msg) => {
    let statusMessage = msg.method + "\t" + msg.path + "\t";
    if (msg.statusCode >= 500) {
        statusMessage += colors.red(msg.statusCode + " " + msg.statusText);
    } else if (msg.statusCode >= 300) {
        statusMessage += colors.yellow(msg.statusCode + " " + msg.statusText);
    } else {
        statusMessage += colors.green(msg.statusCode + " " + msg.statusText);
    }
    return statusMessage;
}

let logKeys = [];
let logs = {};
const maxLogs = 2;

const addLog = (id, log) => {
    if (logs[id] == undefined) {
        // if it doesn't exist add it
        logKeys.push(id);    
    }
    logs[id] = log;
    if (logKeys.length > maxLogs) {
        let removeLogId = logKeys.shift();
        delete logs[removeLogId];
    }
    console.clear();
    writeDefaultLogs();
    for (let i = logKeys.length - 1; i >= 0; i--) {
        console.log(getLogMessage(logs[logKeys[i]]));
    }
}


const relayPath = azureRelayConfig.namespace + "/" + azureRelayConfig.path;
console.clear();
defaultLogs.push(colors.green("nodePassage"));
defaultLogs.push("\n");
defaultLogs.push("Forwarding \t\t" + relayPath + " --> " + azureRelayConfig.targetHttp);
defaultLogs.push("\n");
defaultLogs.push("HTTP Requests");
defaultLogs.push("-------------");
writeDefaultLogs();

var uri = https.createRelayListenUri(azureRelayConfig.namespace, azureRelayConfig.path);
var server = https.createRelayedServer(
    {
        server : uri,
        token : () => https.createRelayToken(uri, azureRelayConfig.policyName, azureRelayConfig.policyKey)
    },
    async (relayRequest, relayResponse) => {
        const endpointPath = relayRequest.url.replace(`/${azureRelayConfig.path}`, '');
        const localServerEndpoint = `${azureRelayConfig.targetHttp}${endpointPath}`;
        
        const log = {
            method: relayRequest.method,
            path: relayRequest.url,
            statusCode: '',
            statusText: ''
        };
        addLog(relayResponse.requestId, log);
        //addLog(relayRequest.method + "\t" + relayRequest.url);
        relayRequest.setEncoding('utf8');
        let headers = relayRequest.headers;
        if (relayRequest.method == 'POST') {
            let rawData = '';
            // wait for all of the data to be received from a post
            relayRequest.on('data', (chunk) => { rawData += chunk; });
            relayRequest.on('end', () => {
            try {
                // parse the relayRequest body
                const parsedData = JSON.parse(rawData);
                // forward the data and headers to the local server
                postData(localServerEndpoint, parsedData, headers)
                .then((response) => {
                    try
                    {
                        const log = {
                            method: relayRequest.method,
                            path: relayRequest.url,
                            statusCode: response.status,
                            statusText: response.statusText
                        };
                        addLog(relayResponse.requestId, log);
                        // if the response is empty, return the relayResponse
                        if (response.data == '') return relayResponse.end();

                        // copy the data from the local server to the relay response
                        let json = JSON.stringify(response.data);
                        relayResponse.setHeader('Content-Type', response.headers["content-type"]);
                        relayResponse.setHeader('Content-Length', Buffer.byteLength(json));
                        relayResponse.end(json);
                    }catch (e)
                    {
                        //console.log(e);
                    }
                });

                //console.log(parsedData);
            } catch (e) {
                //console.error(e);
            }
            });
        } else {
            getData(localServerEndpoint, headers)
            .then((response) => {
                const log = {
                    method: relayRequest.method,
                    path: relayRequest.url,
                    statusCode: response.status,
                    statusText: response.statusText
                };
                addLog(relayResponse.requestId, log);
                relayResponse.setHeader('Content-Type', response.headers["content-type"]);
                relayResponse.end(response.data);
            }).catch((e) => {
                const log = {
                    method: relayRequest.method,
                    path: relayRequest.url,
                    statusCode: e.response.status,
                    statusText: e.response.statusText
                };
                addLog(relayResponse.requestId, log);
            });
        }
    });

server.listen( (err) => {
    if (err) {
        return console.log('something bad happened', err)
    }          
    console.log(`server is listening on ${port}`)
});

server.on('error', (err) => {
    console.log('error: ' + err);
});