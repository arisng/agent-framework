using AGUIDojoClient.Components.Pages.Chat;
using AGUIDojoClient.Tests.Infrastructure;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.AI;
using System.Reflection;

namespace AGUIDojoClient.Tests.Components.Pages.Chat;

public class ChatInputTests : AGUIDojoClientComponentTestBase
{
    [Fact]
    public async Task Submit_ClearsComposerBeforeSendCompletes()
    {
        // Arrange
        var sendStarted = new TaskCompletionSource<ChatMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSend = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        IRenderedComponent<ChatInput> component = this.Render<ChatInput>(parameters => parameters
            .Add(input => input.OnSend, async message =>
            {
                sendStarted.SetResult(message);
                return await releaseSend.Task;
            }));

        ((IHtmlTextAreaElement)component.Find("textarea")).Change("clear immediately");

        // Act
        component.Find("form").Submit();
        await sendStarted.Task;

        // Assert
        component.WaitForAssertion(() =>
            Assert.Equal(string.Empty, ((IHtmlTextAreaElement)component.Find("textarea")).Value));

        releaseSend.SetResult(true);
    }

    [Fact]
    public void Submit_WhenSendRejected_RestoresDraftText()
    {
        // Arrange
        IRenderedComponent<ChatInput> component = this.Render<ChatInput>(parameters => parameters
            .Add(input => input.OnSend, _ => Task.FromResult(false)));

        component.Find("textarea").Change("restore draft");

        // Act
        component.Find("form").Submit();

        // Assert
        FieldInfo messageTextField = typeof(ChatInput).GetField("messageText", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Expected ChatInput.messageText field to exist.");

        component.WaitForAssertion(() =>
            Assert.Equal("restore draft", messageTextField.GetValue(component.Instance)));
    }
}
