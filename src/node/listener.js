const https = require('hyco-https')
const axios = require('axios');
const path = require('path');

const ENV_FILE = path.join(__dirname, '.env');
require('dotenv').config({ path: ENV_FILE });

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
        const res = await axios.post(url, data, { headers });
        console.log(`Status: ${res.status}`);
        console.log('Body: ', res.data);
        return res;
    } catch (e) {
        console.error(e);
    }
};

// used to forward request to local server
// used for HTML Get requests
const getData = async (url, headers) => {
    try {
        const res = await axios.get(url, { headers });
        console.log(`Status: ${res.status}`);
        console.log('Body: ', res.data);
        return res;
    } catch (e) {
        console.error(e);
    }
};

var uri = https.createRelayListenUri(azureRelayConfig.namespace, azureRelayConfig.path);
var server = https.createRelayedServer(
    {
        server : uri,
        token : () => https.createRelayToken(uri, azureRelayConfig.policyName, azureRelayConfig.policyKey)
    },
    async (relayRequest, relayResponse) => {
        const endpointPath = relayRequest.url.replace(`/${azureRelayConfig.path}`, '');
        const localServerEndpoint = `${azureRelayConfig.targetHttp}${endpointPath}`;

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
                        // if the response is empty, return the relayResponse
                        if (response.data == '') return relayResponse.end();
                        
                        // copy the data from the local server to the relay response
                        let json = JSON.stringify(response.data);
                        relayResponse.setHeader('Content-Type', response.headers["content-type"]);
                        relayResponse.setHeader('Content-Length', Buffer.byteLength(json));
                        relayResponse.end(json);
                    }catch (e)
                    {
                        console.log(e);
                    }
                });

                console.log(parsedData);
            } catch (e) {
                console.error(e);
            }
            });
        } else {
            try {
                getData(localServerEndpoint, headers)
                .then((response) => {
                    try
                    {
                        relayResponse.setHeader('Content-Type', response.headers["content-type"]);
                        relayResponse.end(response.data);
                    }catch (ex)
                    {
                        var a = ex;
                    }
                            
                });

            } catch (e) {
                console.error(e);
            }
        }
        console.log('request accepted: ' + relayRequest.method + ' on ' + relayRequest.url);
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