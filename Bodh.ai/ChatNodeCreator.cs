using Bodh.ai;

public class ChatNodeCreator
{
    private readonly StackLayout _chatArea;
    private readonly IMySqlVectorRepository _mySqlVectorRepository;
    private readonly ILoggerService _logger = LoggerService.Instance;
    private bool _isNewChat;
    private string _chatId;
    private readonly ContextMenuCreator _contextMenuCreator;
    private readonly Image _bodhAiImage;

    // Developer code: avoid hard-coding repository implementations here so the same UI logic can be tested with a mock repository.
    public ChatNodeCreator(StackLayout chatArea, Image bodhAiImage, IMySqlVectorRepository mySqlVectorRepository, bool isNewChat, ContextMenuCreator contextMenuCreator)
    {
        _chatArea = chatArea;
        _bodhAiImage = bodhAiImage;
        _mySqlVectorRepository = mySqlVectorRepository;
        _isNewChat = isNewChat;
        _contextMenuCreator = contextMenuCreator;
    }
    public Border CreateChatNode(string strTitle, string strChatId, VerticalStackLayout chatList)
    {
        // Logic to create a new chat node with the given title and content
        var border = new Border
        {
            Padding = new Thickness(14, 10),
            BackgroundColor = Colors.Transparent,
            Stroke = Color.FromArgb("#363535b0"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(10)
            },
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.FillAndExpand,
            Margin = new Thickness(5)
        };

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += btnChat_Clicked;

        // Right click → show menu
        var rightTap = new TapGestureRecognizer { Buttons = ButtonsMask.Secondary };
        rightTap.Tapped += (s, e) =>
        {
            var tap = new TapGestureRecognizer { Buttons = ButtonsMask.Secondary };
            tap.Tapped += (s, e) =>
            {
                Point? pos = e.GetPosition(chatList);
                if (pos.HasValue)
                    _contextMenuCreator.ShowContextMenu(pos.Value.X, pos.Value.Y, border, strTitle, _mySqlVectorRepository, _chatId);
            };
            border.GestureRecognizers.Add(tap);


            //_contextMenuCreator.ShowContextMenu(border, strTitle, _mySqlVectorRepository, _chatId);
        };

        border.GestureRecognizers.Add(tapGesture);
        border.GestureRecognizers.Add(rightTap);

        var label = new Label
        {
            Text = strTitle,
            AutomationId = strChatId,
            FontSize = 20,
            TextColor = Colors.Black,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        border.Content = label;

        return border;
    }
    private async void btnChat_Clicked(object? sender, EventArgs e)
    {
        try
        {
            if (sender is Border border && border.Content is Label label)
            {
                string chatId = label.AutomationId;
                string chatTitle = label.Text;
                _chatId = chatId;
                _isNewChat = false;
                _logger.LogUserEvent($"User opened chat {chatId} titled '{chatTitle}'");
                List<ChatMessage> chatMessages = await _mySqlVectorRepository.GetChatMessagesAsync(chatId);
                _bodhAiImage.IsVisible = false;
                _chatArea.Children.Clear();
                foreach (var message in chatMessages)
                {
                    _chatArea.Children.Add(ChatBubbleBuilder.CreateChatBubble(message.Content, message.IsUser, message.Timestamp));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Chat node click handler failed.");
        }
    }
    public bool IsNewChat()
    {
        return _isNewChat;
    }
    public void SetNewChat(bool isNewChat)
    {
        _isNewChat = isNewChat;
    }
    public string GetChatIdFromNode()
    {
        return _chatId;
    }
}