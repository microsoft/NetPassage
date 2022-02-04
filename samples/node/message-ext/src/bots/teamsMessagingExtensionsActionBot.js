// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

require('dotenv').config();
const { TeamsActivityHandler, CardFactory, TeamsInfo, MessageFactory } = require('botbuilder');
const env = process.env;
const baseurl = env.BaseUrl;

class TeamsMessagingExtensionsActionBot extends TeamsActivityHandler {
    async handleTeamsMessagingExtensionSubmitAction(context, action) {
        switch (action.commandId) {
        case 'createCard':
            return createCardCommand(context, action);
        case 'shareMessage':
            return shareMessageCommand(context, action);
        case 'webView':
            return echoUserInputResponse(context, action);
            //return await webViewResponse(action);
        }
    }

    async handleTeamsMessagingExtensionFetchTask(context, action) {
        switch (action.commandId) {
        case 'webView':
            return empDetails();
        case 'Static HTML':
            return dateTimeInfo();
        default:
            try {
                const member = await this.getSingleMember(context);
                return {
                    task: {
                        type: 'continue',
                        value: {
                            card: GetAdaptiveCardAttachment(),
                            height: 400,
                            title: `Hello ${ member }`,
                            width: 300
                        }
                    }
                };
            } catch (e) {
                if (e.code === 'BotNotInConversationRoster') {
                    return {
                        task: {
                            type: 'continue',
                            value: {
                                card: GetJustInTimeCardAttachment(),
                                height: 400,
                                title: 'Adaptive Card - App Installation',
                                width: 300
                            }
                        }
                    };
                }
                throw e;
            }
        }
    }

    async getSingleMember(context) {
        try {
            const member = await TeamsInfo.getMember(
                context,
                context.activity.from.id
            );
            return member.name;
        } catch (e) {
            if (e.code === 'MemberNotFoundInConversation') {
                context.sendActivity(MessageFactory.text('Member not found.'));
                return e.code;
            }
            throw e;
        }
    }
}

function GetJustInTimeCardAttachment() {
    return CardFactory.adaptiveCard({
        actions: [
            {
                type: 'Action.Submit',
                title: 'Continue',
                data: { msteams: { justInTimeInstall: true } }
            }
        ],
        body: [
            {
                text: 'Looks like you have not used Action Messaging Extension app in this team/chat. Please click **Continue** to add this app.',
                type: 'TextBlock',
                wrap: true
            }
        ],
        type: 'AdaptiveCard',
        version: '1.0'
    });
}

function GetAdaptiveCardAttachment() {
    return CardFactory.adaptiveCard({
        actions: [{ type: 'Action.Submit', title: 'Close' }],
        body: [
            {
                text: 'This app is installed in this conversation. You can now use it to do some great stuff!!!',
                type: 'TextBlock',
                isSubtle: false,
                wrap: true
            }
        ],
        type: 'AdaptiveCard',
        version: '1.0'
    });
}

function createCardCommand(context, action) {
    // The user has chosen to create a card by choosing the 'Create Card' context menu command.
    const data = action.data;
    const heroCard = CardFactory.heroCard("Title: " + data.title, data.text);
    heroCard.content.subtitle = data.subTitle;
    const attachment = { contentType: heroCard.contentType, content: heroCard.content, preview: heroCard };

    return {
        composeExtension: {
            type: 'result',
            attachmentLayout: 'list',
            attachments: [
                attachment
            ]
        }
    };
}

function shareMessageCommand(context, action) {
    // The user has chosen to share a message by choosing the 'Share Message' context menu command.
    let userName = 'unknown';
    if (action.messagePayload.from &&
            action.messagePayload.from.user &&
            action.messagePayload.from.user.displayName) {
        userName = action.messagePayload.from.user.displayName;
    }

    // This Messaging Extension example allows the user to check a box to include an image with the
    // shared message.  This demonstrates sending custom parameters along with the message payload.
    let images = [];
    const includeImage = action.data.includeImage;
    if (includeImage === 'true') {
        images = ['https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcQtB3AwMUeNoq4gUBGe6Ocj8kyh3bXa9ZbV7u1fVKQoyKFHdkqU'];
    }
    const heroCard = CardFactory.heroCard(`${ userName } originally sent this message:`,
        action.messagePayload.body.content,
        images);

    if (action.messagePayload.attachments && action.messagePayload.attachments.length > 0) {
        // This sample does not add the MessagePayload Attachments.  This is left as an
        // exercise for the user.
        heroCard.content.subtitle = `(${ action.messagePayload.attachments.length } Attachments not included)`;
    }

    const attachment = { contentType: heroCard.contentType, content: heroCard.content, preview: heroCard };

    return {
        composeExtension: {
            type: 'result',
            attachmentLayout: 'list',
            attachments: [
                attachment
            ]
        }
    };
}

function echoUserInputResponse(context, action) {
    const data = "Customized Data: " + action.data + " modified by client backend app";
    return data;
}

function empDetails() {
    return {
        task: {
            type: 'continue',
            value: {
                width: 350,
                height: 300,
                title: 'Task module WebView',
                url: `${ baseurl }/CustomForm.html`
            }
        }
    };
}

function dateTimeInfo() {
    return {
        task: {
            type: 'continue',
            value: {
                width: 450,
                height: 125,
                title: 'Task module Static HTML',
                url: `${ baseurl }/StaticPage2.html`
            }
        }
    };
}

async function webViewResponse(action) {
    // The user has chosen to create a card by choosing the 'Create Card' context menu command.
    const data = await action.data;
    const heroCard = CardFactory.heroCard(`ID: ${ data.EmpId }`, `E-Mail: ${ data.EmpEmail }`);
    heroCard.content.subtitle = `Name: ${ data.EmpName }`;
    const attachment = { contentType: heroCard.contentType, content: heroCard.content, preview: heroCard };
    return {
        composeExtension: {
            type: 'result',
            attachmentLayout: 'list',
            attachments: [
                attachment
            ]
        }
    };
}

module.exports.TeamsMessagingExtensionsActionBot = TeamsMessagingExtensionsActionBot;
