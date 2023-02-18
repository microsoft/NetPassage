/* eslint-disable max-len */
import https from "hyco-https";
import axios, { AxiosError } from "axios";
import colors from "colors";
import DisplayLog from "./models/displaylog";
import * as config from "./config/netPassage.json";
import RelayConfig from "./models/relayConfig";
import { exit } from "process";

colors.enable();

const azureRelayDomain = "servicebus.windows.net";

type LogRecord = Record<keyof DisplayLog, string>;

// ONLY IN DEVELOPMENT!!!
if (config.NodeEnv === "Development") {
  const httpsAgent = new https.Agent({
    rejectUnauthorized: false,
  });
  axios.defaults.httpsAgent = httpsAgent;
  // eslint-disable-next-line no-console
  console.log(config.NodeEnv, `RejectUnauthorized is disabled.`);
}

// construct Relay Config settings from the configuration JSON file
const azureRelayConfig: RelayConfig[] = [];

config.Relay.ConnectionSettings.forEach((connection) => {
  const relay: RelayConfig = {
    namespace: config.Relay.Namespace,
    path: connection.HybridConnection,
    policyName: connection.PolicyName,
    policyKey: connection.PolicyKey,
    targetHttp: connection.TargetHttp,
  };
  azureRelayConfig.push(relay);
});

if (azureRelayConfig.length === 0) {
  // eslint-disable-next-line max-len
  console.log(
    colors.red(
      "Either configuration file is missing or the configuration settings could not be properly read",
    ),
  );
  exit;
}

// used to forward request to local server
const postDataAsync = async (url: string, data?: any, headers?: any) => {
  return await axios.post(url, data, headers);
};

// used to forward request to local server
// used for HTML Get requests
const getDataAsync = async (url: string, headers?: any) => {
  return await axios.get(url, headers);
};

// Show the NetPassage Headers on the Display Output window
const displayHeader = (configs: RelayConfig[]): void => {
  const displayHeaders: string[] = [];
  console.clear();
  displayHeaders.push(
    `${colors.green("NetPassage")} by ${colors.green("Microsoft")}`,
  );
  displayHeaders.push(`${colors.green("Version: 1.0.0")}\n`);
  displayHeaders.push(
    `Azure Relay Namespace:\t\t ${colors.green(configs[0].namespace)}`,
  );

  configs.forEach((config) => {
    const relayPath = `https://${config.namespace}.${azureRelayDomain}/${config.path}`;
    displayHeaders.push(
      `Forwarding:           \t\t ${relayPath} ${colors.green("-->")} ${
        config.targetHttp
      }`,
    );
  });
  displayHeaders.push("\n");

  displayHeaders.push("NetPassage Relay Requests");
  displayHeaders.push("--------------------------");

  displayHeaders.forEach((headerLine) => {
    console.log(headerLine);
  });
};

// Construct the Log Message
const MAX_CONTENT_TO_DISPLAY = 40;
const getLogMessage = (log: LogRecord): string => {
  let statusMessage = `${log.method}\t${log.path}\t`;
  const statusCode = Number.parseInt(log.statusCode ?? "0");
  const contentLength = `${
    log.contentLength.length > 0
      ? // eslint-disable-next-line max-len
        `${colors.white("[Content-Length:")} ${colors.green(
          log.contentLength,
        )}${colors.white("; Data:")} ${colors.green(
          JSON.stringify(log.data.substring(0, MAX_CONTENT_TO_DISPLAY)),
        )}${colors.white(
          `${log.data.length > MAX_CONTENT_TO_DISPLAY ? "..." : ""}]`,
        )}`
      : ""
  }`;

  if (statusCode >= 500) {
    statusMessage += colors.red(
      `${statusCode} ${log.statusText} ${contentLength}`,
    );
  } else if (statusCode >= 300) {
    statusMessage += colors.yellow(
      `${statusCode} ${log.statusText} ${contentLength}`,
    );
  } else {
    statusMessage += colors.green(
      `${statusCode} ${log.statusText} ${contentLength}`,
    );
  }
  return statusMessage;
};

let logs: LogRecord[] = [];
const maxLogs = process.env.MAX_LOGS ?? 20;

// Add display log record into the Logs dictionary with the message Id as a key
const addLog = (log: LogRecord): LogRecord => {
  // Return back if statusCode is empty
  if (log.statusCode === "" || log.id === undefined) return log;

  // if the data was not modified (e.g. HTTP Status Code == 304, return the last record with the same path)
  if (log.statusCode === "304") {
    const lastRecord = logs.filter((l) => l.path === log.path).pop()!;
    logs.push(lastRecord);
    return lastRecord;
  }

  if (log.id !== "" && logs.find((l) => l.id === log.id) !== undefined) {
    // the log record with the same Id found, replace it
    logs = logs.map((item) => {
      return item.id === log.id ? log : item;
    });
  } else {
    // if it doesn't exist, then add it
    logs.push(log);
  }

  // if the number of logs in the array is greater than the maximum allowed, delete the first log from the display
  if (logs.length > maxLogs) {
    logs.shift();
  }

  // Redraw the logs
  console.clear();
  displayHeader(azureRelayConfig);

  logs.forEach((log) => {
    console.log(getLogMessage(log));
  });

  return log;
};

// Construct HTTP Headers
const setHeaders = (relayResponse, headers) => {
  const keys = Object.keys(headers);
  keys.forEach((header) => {
    switch (header) {
      case "transfer-encoding":
      case "keep-alive":
        break;
      default:
        const value = headers[header];
        if (value) relayResponse.setHeader(header, value);
        break;
    }
  });

  // To support Web page rendering
  relayResponse.setHeader("content-type", "text/html; charset=UTF-8");
};

// Format data
const formatData = (headers, data) => {
  const contentType = headers["content-type"];
  switch (contentType) {
    case "application/json":
      return JSON.stringify(data);
    default:
      return data;
      break;
  }
};

// Create a valid Azure Relay Hybrid Connection listener URI for the given namespace and path.
// namespaceName (required) - the domain-qualified name of the Azure Relay namespace to use
// path (required) - the name of an existing Azure Relay Hybrid Connection in that namespace
// token (optional) - a previously issued Relay access token that shall be embedded in the listener URI (see below)
// id (optional) - a tracking identifier that allows end-to-end diagnostics tracking of requests
azureRelayConfig.forEach((config) => {
  const listenUri = https.createRelayListenUri(
    `${config.namespace}.${azureRelayDomain}`,
    config.path,
  );

  const tokenExpired = process.env.TOKEN_EXPIRE_SECS ?? null;
  // Create a server that does not listen on the local network, but delegates listening to the Azure Relay.
  const server = https.createRelayedServer(
    {
      server: listenUri,
      token: () =>
        // Create an Azure Relay Shared Access Signature (SAS) token for the given target URI, SAS rule,
        // and SAS rule key that is valid for the given number of seconds or for an hour
        // from the current instant if the expiry argument is omitted.
        https.createRelayToken(
          listenUri,
          config.policyName,
          config.policyKey,
          tokenExpired,
        ),
    },
    async (relayRequest, relayResponse) => {
      const endpointPath = relayRequest.url.replace(`/${config.path}`, "");
      const localServerEndpoint = `${config.targetHttp}${endpointPath}`;

      const log: LogRecord = {
        id: relayResponse.requestId,
        method: relayRequest.method,
        path: relayRequest.url,
        statusCode: "",
        statusText: "",
        contentLength: "",
        data: "",
        headers: "",
      };
      addLog(log);

      // Process data
      try {
        relayRequest.setEncoding("utf8");
        const headers = relayRequest.headers;
        if (relayRequest.method === "POST") {
          let rawData = "";
          // wait for all of the data to be received from a post
          relayRequest.on("data", (chunk) => {
            rawData += chunk;
          });
          relayRequest.on("end", async () => {
            // parse the relayRequest body
            const parsedData = JSON.parse(rawData);
            try {
              // forward the data and headers to the local server
              const response = await postDataAsync(
                localServerEndpoint,
                parsedData,
                { headers },
              );

              // Display log
              const log: LogRecord = {
                id: relayResponse.requestId,
                method: relayRequest.method,
                path: relayRequest.url,
                statusCode: response.status.toString(),
                statusText: response.statusText,
                contentLength: parsedData.text
                  ? parsedData?.text?.length?.toString()
                  : parsedData?.length.toString(),
                data: parsedData.text ?? parsedData,
                headers: relayResponse.getHeaders(),
              };
              addLog(log);

              // if the response is empty, return the relayResponse
              if (response.data === "") return relayResponse.end();

              // copy the data from the local server to the relay response
              setHeaders(relayResponse, response.headers);
              const data = formatData(response.headers, response.data);
              relayResponse.end(data);
            } catch (error) {
              const log: LogRecord = {
                id: relayResponse.requestId,
                method: relayRequest.method,
                path: relayRequest.url,
                statusCode:
                  error.response !== undefined
                    ? error.response.status.toString()
                    : "502",
                statusText:
                  error.response !== undefined
                    ? error.response.statusText
                    : "Bad Gateway",
                contentLength: relayResponse.data?.length?.toString() ?? "",
                data: relayResponse.data,
                headers: relayResponse.getHeaders(),
              };
              const record = addLog(log);

              if ((error as AxiosError)?.response?.status === 304) {
                //relay previously stored data to the remote app
                setHeaders(relayResponse, record.headers);
                relayResponse.end(record.data);
              }
            }
          });
        } // HTTP GET
        else {
          try {
            const response = await getDataAsync(localServerEndpoint, {
              headers,
            });

            // relay data to the remote app
            setHeaders(relayResponse, response.headers);
            const data = formatData(response.headers, response.data);
            relayResponse.end(data);

            const log: LogRecord = {
              id: relayResponse.requestId,
              method: relayRequest.method,
              path: relayRequest.url,
              statusCode: response.status.toString(),
              statusText: response.statusText,
              contentLength: data.length?.toString() ?? "",
              data: data,
              headers: relayResponse.getHeaders(),
            };
            addLog(log);
          } catch (error: any) {
            const log: LogRecord = {
              id: relayResponse.requestId,
              method: relayRequest.method,
              path: relayRequest.url,
              statusCode:
                error.response !== undefined
                  ? error.response.status.toString()
                  : "502",
              statusText:
                error.response !== undefined
                  ? error.response.statusText
                  : "Bad Gateway",
              contentLength: relayResponse.data?.length?.toString() ?? "",
              data: relayResponse.data,
              headers: relayResponse.getHeaders(),
            };
            const record = addLog(log);

            if ((error as AxiosError)?.response?.status === 304) {
              //relay previously stored data to the remote app
              setHeaders(relayResponse, record.headers);
              relayResponse.end(record.data);
            }
          }
        }
      } catch (error) {
        const log: LogRecord = {
          id: "",
          method: relayRequest.method,
          path: relayRequest.url,
          statusCode:
            error.response !== undefined
              ? error.response.status.toString()
              : "502",
          statusText:
            error.response !== undefined
              ? error.response.statusText
              : "Bad Gateway",
          contentLength: "",
          data: "",
          headers: "",
        };
        addLog(log);
      }
    },
  );

  // Display Out the Header
  displayHeader(azureRelayConfig);

  server.listen((err) => {
    if (err) {
      return console.log("something bad happened", err);
    }
    console.log(`server is listening on ${process.env.targetHttp}`);
  });

  server.on("error", (err) => {
    console.log("error: " + err.message);
  });
});
