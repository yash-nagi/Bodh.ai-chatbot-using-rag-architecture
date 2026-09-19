using System.Drawing;
using Bodh.ai;
public class ContextMenuCreator
{
    private readonly MainPage.RemoveChatHistoryNodeDelegate _removeChatHistoryNodeDelegate;
    private readonly Grid _sidePanel;
    private readonly ILoggerService _logger = LoggerService.Instance;

    public ContextMenuCreator(MainPage.RemoveChatHistoryNodeDelegate removeChatHistoryNodeDelegate, Grid sidePanel)
    {
        _removeChatHistoryNodeDelegate = removeChatHistoryNodeDelegate;
        _sidePanel = sidePanel;
    }

    private View _currentMenu = null;
    private bool _isChatDeleted = false;

    public void ShowContextMenu(double x, double y, Border chat, string chatName, IMySqlVectorRepository mySqlVectorRepository, string chatId)
    {
        DismissMenu();

        // Get position relative to page (scroll-aware)
        //var point = GetPositionOnPage(chat);

        // Build menu
        var menu = new StackLayout
        {
            BackgroundColor = Colors.White,
            WidthRequest = 140,
            Padding = 0
        };

        var renameBtn = new Button
        {
            Text = "Rename",
            BackgroundColor = Colors.White,
            TextColor = Colors.Black,
            FontSize = 14,
            Padding = new Thickness(2)
        };
        renameBtn.Clicked += async (s, e) =>
        {
            try
            {
                DismissMenu();
                var newName = await Application.Current.MainPage.DisplayPromptAsync("Rename", null, "OK", "Cancel", chatName);
                if (newName != null && chat.Content is Label lbl)
                {
                    await mySqlVectorRepository.UpdateChatTitleAsync(chatId, newName);
                    lbl.Text = newName;
                    _logger.LogUserEvent($"Renamed chat {chatId} to {newName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, $"Failed to rename chat {chatId}");
            }
        };
        var deleteBtn = new Button
        {
            Text = "Delete",
            BackgroundColor = Colors.White,
            TextColor = Colors.Red,
            FontSize = 14,
            Padding = new Thickness(2)
        };
        deleteBtn.Clicked += async (s, e) =>
        {
            try
            {
                DismissMenu();
                var confirm = await Application.Current.MainPage.DisplayAlert("Delete", $"Delete \"{chatName}\"?", "Yes", "No");
                if (confirm)
                {
                    await mySqlVectorRepository.DeleteChatHistoryAsync(chatId);
                    await mySqlVectorRepository.DeleteChatTableAsync(chatId);
                    _logger.LogUserEvent($"User deleted chat {chatId}");
                }
                _isChatDeleted = true;
                _removeChatHistoryNodeDelegate(chat);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, $"Failed to delete chat {chatId}");
            }
        };

        menu.Children.Add(renameBtn);
        menu.Children.Add(deleteBtn);

        var menuFrame = new Frame
        {
            Content = menu,
            BackgroundColor = Colors.White,
            CornerRadius = 5,
            HasShadow = true,
            WidthRequest = 140
        };

        var overlay = new AbsoluteLayout
        {
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.FillAndExpand,
            VerticalOptions = LayoutOptions.FillAndExpand
        };

        AbsoluteLayout.SetLayoutBounds(menuFrame,
            new Microsoft.Maui.Graphics.Rect(x, y, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));

        AbsoluteLayout.SetLayoutFlags(menuFrame, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);

        Grid.SetRow(overlay, 0);
        Grid.SetRowSpan(overlay, 2);

        overlay.Children.Add(menuFrame);

        var dismissTap = new TapGestureRecognizer();
        dismissTap.Tapped += (s, e) => DismissMenu();
        overlay.GestureRecognizers.Add(dismissTap);

        _currentMenu = overlay;

        var rootGrid = _sidePanel;
        rootGrid.Children.Add(overlay);
    }
    private void DismissMenu()
    {
        if (_currentMenu != null)
        {
            ((Grid)_sidePanel).Children.Remove(_currentMenu);
            _currentMenu = null;
        }
    }

    private Microsoft.Maui.Graphics.Point GetPositionOnPage(VisualElement element)
    {
        double x = element.X;
        double y = element.Y;
        try
        {
            // Walk up the visual tree
            IElement current = element.Parent;
            while (current != null)
            {
                if (current is VisualElement ve)
                {
                    x += ve.X;
                    y += ve.Y;

                    // Subtract scroll offset if we hit the ScrollView
                    if (current is ScrollView sv)
                    {
                        y -= sv.ScrollY;
                    }
                }
                current = current.Parent;
            }
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "GetPositionOnPage failed while computing the context menu position.");
        }

        return new Microsoft.Maui.Graphics.Point(x, y);
    }
    public bool IsChatDeleted()
    {
        return _isChatDeleted;
    }
}