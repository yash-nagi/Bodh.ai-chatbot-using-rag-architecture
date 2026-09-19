namespace Bodh.ai;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Platform;
using Indiko.Maui.Controls.Markdown;

public partial class MainPage : ContentPage
{
	private bool isNewChat = true;
	private readonly IOllamaService _ollamaService;
	private readonly IConfigurationService _configurationService;
	private readonly IMySqlVectorRepository _mySqlVectorRepository;
	private readonly ILoggerService _logger;
	private string strChatId = string.Empty;
	private readonly ChatNodeCreator _chatNodeCreator;
	private readonly ContextMenuCreator _contextMenuCreator;
	public delegate void RemoveChatHistoryNodeDelegate(Border chatHistoryNode);
	private readonly MarkdownRenderer _markdownRenderer;

	public MainPage()
	{
		InitializeComponent();
		_ollamaService = IPlatformApplication.Current.Services.GetRequiredService<IOllamaService>();
		_configurationService = IPlatformApplication.Current.Services.GetRequiredService<IConfigurationService>();
		_mySqlVectorRepository = IPlatformApplication.Current.Services.GetRequiredService<IMySqlVectorRepository>();
		_logger = IPlatformApplication.Current.Services.GetRequiredService<ILoggerService>();
		_contextMenuCreator = new ContextMenuCreator(RemoveChatHistoryNode, sidePanel);
		_chatNodeCreator = new ChatNodeCreator(chatArea, bodhAiImage, _mySqlVectorRepository, isNewChat, _contextMenuCreator);
		_markdownRenderer = new MarkdownRenderer();
	}

	private void btnToggleSidePanel_Clicked(object sender, EventArgs e)
	{
		if (sidePanel.IsVisible)
		{
			sidePanel.IsVisible = false;
			btnNewChat.IsVisible = true;
			sidePanelColumn.Width = new GridLength(0);
		}
		else
		{
			sidePanel.IsVisible = true;
			btnNewChat.IsVisible = false;
			sidePanelColumn.Width = new GridLength(300);
		}
	}

	private void btnNewChat_Clicked(object sender, EventArgs e)
	{
		// Developer code: new-chat state is reset here so the app always knows when to create a new session.
		isNewChat = true;
		_chatNodeCreator.SetNewChat(true);
		chatArea.Children.Clear();
		bodhAiImage.IsVisible = true;
	}

	private async void btnSend_Clicked(object sender, EventArgs e)
	{
		try
		{
			bodhAiImage.IsVisible = false;
			var userMessage = txtUserInput.Text ?? string.Empty;
			if (string.IsNullOrWhiteSpace(userMessage))
			{
				return;
			}

			_logger.LogUserEvent($"User submitted message: {userMessage}");
			var timestamp = DateTime.Now;
			txtUserInput.Text = string.Empty;
			isNewChat = _chatNodeCreator.IsNewChat();

			if (isNewChat)
			{
				strChatId = Guid.NewGuid().ToString().ToUpper().Replace("-", "");
				chatList.Children.Add(_chatNodeCreator.CreateChatNode(userMessage, strChatId, chatList));
				isNewChat = false;
				_chatNodeCreator.SetNewChat(false);
				await _mySqlVectorRepository.CreateNewChatTableAsync(strChatId);
				await _mySqlVectorRepository.SaveChatHistoryAsync(new ChatHistory
				{
					ChatId = strChatId,
					ChatTitle = userMessage,
					CreatedAt = timestamp
				});
			}
			else
			{
				strChatId = _chatNodeCreator.GetChatIdFromNode();
			}

			chatArea.Children.Add(ChatBubbleBuilder.CreateChatBubble(userMessage, isUser: true, timestamp));
			var userMessageId = Guid.NewGuid().ToString().ToUpper().Replace("-", "");
			await _mySqlVectorRepository.SaveMessageAsync(new ChatMessage
			{
				MessageId = userMessageId,
				Role = "User",
				Content = userMessage,
				IsUser = true,
				Timestamp = timestamp
			}, strChatId);

			await _mySqlVectorRepository.SaveEmbeddingAsync(new EmbeddingModel
			{
				Content = userMessage,
				Embedding = await _ollamaService.GetEmbeddingAsync(userMessage)
			}, strChatId);

			if (chatArea.Children.Count == 0 || !(chatArea.Children.Last() is Border lastBubble && lastBubble.BackgroundColor == Color.FromArgb("#2D2D30")))
			{
				chatArea.Children.Add(ChatBubbleBuilder.CreateChatBubble("Replying...", isUser: false, DateTime.Now));
			}

			MarkdownView? markdownView = null;
			if (chatArea.Children.Last() is Border aiResponseBubble && aiResponseBubble.Content is VerticalStackLayout aiContentStack && aiContentStack.Children[0] is MarkdownView mdView)
			{
				markdownView = mdView;
				string aiResponse = await TimerFlushesAccumulatedTextToUI(userMessage, markdownView);
				_logger.LogAiEvent($"AI generated reply for chat {strChatId}. Response length: {aiResponse.Length}");
				timestamp = DateTime.Now;
				if (chatArea.Children.Last() is Border aiMsgBubble && aiMsgBubble.Content is VerticalStackLayout responseContentStack && responseContentStack.Children[1] is Label timeStampLabel)
				{
					timeStampLabel.Text = timestamp.ToString("hh:mm tt");
				}

				await _mySqlVectorRepository.SaveMessageAsync(new ChatMessage
				{
					MessageId = Guid.NewGuid().ToString().ToUpper().Replace("-", ""),
					Role = "AI",
					Content = aiResponse,
					IsUser = false,
					Timestamp = timestamp
				}, strChatId);

				await _mySqlVectorRepository.SaveEmbeddingAsync(new EmbeddingModel
				{
					Content = aiResponse,
					Embedding = await _ollamaService.GetEmbeddingAsync(aiResponse)
				}, strChatId);
			}
		}
		catch (Exception ex)
		{
			_logger.LogException(ex, "btnSend_Clicked failed while processing user request.");
			await DisplayAlert("Error", $"Something went wrong while sending your message. {ex.Message}", "OK");
		}
	}

	private void ScrollToEnd()
	{
		chatListScrollView.ScrollToAsync(0, double.MaxValue, false);
	}

	private async Task<string> TimerFlushesAccumulatedTextToUI(string userMessage, MarkdownView markdownView)
	{
		// Agent code: build contextual prompt before the AI call to keep the retrieval step attached with the user request instead of a global search.
		float[] userEmbedding = await _ollamaService.GetEmbeddingAsync(userMessage);
		List<string> similarMessages = await _mySqlVectorRepository.SearchSimilarMessagesAsync(userEmbedding, strChatId);
		string context = string.Join("\n", similarMessages.Select((msg, index) => $"Context {index + 1}: {msg}"));
		string contextualPrompt = string.IsNullOrWhiteSpace(context) ? userMessage : $"{context}\n\nUser: {userMessage}";

		var timer = new System.Timers.Timer(80);
		timer.AutoReset = true;
		string pendingText = string.Empty;
		string fullMarkdown = string.Empty;
		bool firstToken = true;

		timer.Elapsed += (s, e) =>
		{
			if (!string.IsNullOrEmpty(pendingText))
			{
				var text = pendingText;
				pendingText = string.Empty;
				MainThread.BeginInvokeOnMainThread(() => markdownView.MarkdownText = text);
			}
		};

		timer.Start();
		await foreach (var token in _ollamaService.SendMessageStreamAsync(contextualPrompt))
		{
			fullMarkdown = firstToken ? token : fullMarkdown + token;
			firstToken = false;
			pendingText = fullMarkdown;
		}

		timer.Stop();
		var finalText = fullMarkdown;
		MainThread.BeginInvokeOnMainThread(() => markdownView.MarkdownText = finalText);
		return finalText;
	}

	private async void OnAddAttachmentClicked(object sender, EventArgs e)
	{
		string action = await DisplayActionSheet("Add Attachment", "Cancel", null,
			"📷 Photos",
			"🎥 Videos",
			"📄 PDF",
			"📎 Other File");

		if (string.IsNullOrEmpty(action) || action == "Cancel")
			return;

		try
		{
			PickOptions options = new PickOptions();

			if (action == "📷 Photos")
			{
				options.FileTypes = FilePickerFileType.Images;
				options.PickerTitle = "Select a Photo";
			}
			else if (action == "🎥 Videos")
			{
				options.FileTypes = FilePickerFileType.Videos;
				options.PickerTitle = "Select a Video";
			}
			else if (action == "📄 PDF")
			{
				options.FileTypes = FilePickerFileType.Pdf;
				options.PickerTitle = "Select a PDF";
			}
			else if (action == "📎 Other File")
			{
				options.PickerTitle = "Select a File";
			}

#pragma warning disable CS8600
			FileResult? result = await FilePicker.Default.PickAsync(options);
#pragma warning restore CS8600

			if (result != null)
			{
				string filePath = result.FullPath;
				string fileName = result.FileName;
				_ = filePath;
				_ = fileName;
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Could not pick file: {ex.Message}", "OK");
		}
	}

	private async void ContentPage_Loaded(object sender, EventArgs e)
	{
		// Developer code: ensure startup migration is done before replaying persisted chats.
		await _mySqlVectorRepository.CreateChatHistoryTableIfNotExistsAsync();
		await _mySqlVectorRepository.CreateEmbeddingTableIfNotExistsAsync();
		var chatSessions = await _mySqlVectorRepository.GetChatHistoryAsync();
		foreach (var session in chatSessions)
		{
			chatList.Children.Add(_chatNodeCreator.CreateChatNode(session.ChatTitle, session.ChatId, chatList));
		}
	}

	public void RemoveChatHistoryNode(Border chatHistoryNode)
	{
		if (chatList.Dispatcher.IsDispatchRequired)
		{
			chatList.Dispatcher.Dispatch(() => chatList.Children.Remove(chatHistoryNode));
		}
		else
		{
			chatList.Children.Remove(chatHistoryNode);
		}

		isNewChat = true;
		_chatNodeCreator.SetNewChat(true);
		chatArea.Children.Clear();
		bodhAiImage.IsVisible = true;
	}
}
