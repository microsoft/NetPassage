// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// index.js is used to setup and configure your bot

// Import required pckages
const path = require("path")

// Read botFilePath and botFileSecret from .env file.
const ENV_FILE = path.join(__dirname, ".env")
require("dotenv").config({ path: ENV_FILE })

const restify = require("restify")

// Import required bot services.
// See https://aka.ms/bot-services to learn more about the different parts of a bot.
const {
	CloudAdapter,
	ConfigurationServiceClientCredentialFactory,
	createBotFrameworkAuthenticationFromConfiguration,
} = require("botbuilder")
const {
	TeamsMessagingExtensionsActionBot,
} = require("./bots/teamsMessagingExtensionsActionBot")

const credentialsFactory = new ConfigurationServiceClientCredentialFactory({
	MicrosoftAppId: process.env.MicrosoftAppId,
	MicrosoftAppPassword: process.env.MicrosoftAppPassword,
	MicrosoftAppType: process.env.MicrosoftAppType,
	MicrosoftAppTenantId: process.env.MicrosoftAppTenantId,
})

const botFrameworkAuthentication =
	createBotFrameworkAuthenticationFromConfiguration(null, credentialsFactory)

// Create adapter.
// See https://aka.ms/about-bot-adapter to learn more about how bots work.
const adapter = new CloudAdapter(botFrameworkAuthentication)

adapter.onTurnError = async (context, error) => {
	// This check writes out errors to console log .vs. app insights.
	// NOTE: In production environment, you should consider logging this to Azure
	//       application insights. See https://aka.ms/bottelemetry for telemetry
	//       configuration instructions.
	console.error(`\n [onTurnError] unhandled error: ${error}`)

	// Send a trace activity, which will be displayed in Bot Framework Emulator
	await context.sendTraceActivity(
		"OnTurnError Trace",
		`${error}`,
		"https://www.botframework.com/schemas/error",
		"TurnError",
	)

	// Send a message to the user
	await context.sendActivity("The bot encountered an error or bug.")
	await context.sendActivity(
		"To continue to run this bot, please fix the bot source code.",
	)
}

// Create the bot that will handle incoming messages.
const bot = new TeamsMessagingExtensionsActionBot()

// Create HTTP server.
const server = restify.createServer();
server.use(restify.plugins.acceptParser(server.acceptable));
server.use(restify.plugins.queryParser());
server.use(restify.plugins.bodyParser());

server.pre(restify.pre.userAgentConnection());
server.pre()

server.listen(process.env.port || process.env.PORT || 3978, function () {
	console.log(`\n${server.name} listening to ${server.url}`)
})

// Listen for incoming requests.
server.post("/api/messages", async (req, res) => {
	// Route received a request to adapter for processing
	await adapter.process(req, res, (context) => bot.run(context))
})

server.get(
	"/hello/",
	async (req, res) => {
		var body = 'hello world';
		res.write(body);
		res.end();
	}
)

server.get(
	"/*",
	restify.plugins.serveStatic({
		directory: "./src/pages",
	})
)
