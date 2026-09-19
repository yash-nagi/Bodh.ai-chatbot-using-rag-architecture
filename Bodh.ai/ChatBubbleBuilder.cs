using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Indiko.Maui.Controls.Markdown;
using Indiko.Maui.Controls.Markdown.Theming;
public static class ChatBubbleBuilder
{
    public static Border CreateChatBubble(string messageText, bool isUser, DateTime timestamp)
    {
        // 1. Message Text Label
        //var messageLabel = new Label
        //{
        //    Text = messageText,
        //    TextColor = Colors.White,
        //    FontSize = 15,
        //    LineBreakMode = LineBreakMode.WordWrap
        //};
        var customTheme = new MarkdownTheme();

        // ✅ Colors go under .Palette
        customTheme.Palette.TextPrimary = Colors.White;
        customTheme.Palette.H1Color = Colors.White;
        customTheme.Palette.H2Color = Colors.White;
        customTheme.Palette.H3Color = Colors.White;
        customTheme.Palette.H4Color = Colors.White;
        customTheme.Palette.H5Color = Colors.White;
        customTheme.Palette.H6Color = Colors.White;
        customTheme.Palette.HyperlinkColor = Colors.LightBlue;
        customTheme.Palette.CodeBlockBackground = Color.FromArgb("#2D2D30");
        customTheme.Palette.CodeBlockText = Colors.LightGreen;
        customTheme.Palette.BlockQuoteBackground = Color.FromArgb("#2D2D30");
        customTheme.Palette.BlockQuoteText = Colors.LightGray;

        // Typography (optional)
        customTheme.Typography.BodyFontSize = 15;
        var markdownView = new MarkdownView
        {
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.Fill,
            MarkdownText = messageText,
            TextFontSize = 15,
            TextColor = Colors.White,
            Theme = customTheme,
            BackgroundColor = Colors.Transparent,
            LineBreakModeText = LineBreakMode.WordWrap
        };

        // 2. Timestamp Label
        var timeLabel = new Label
        {
            Text = (messageText != "Replying...") ? timestamp.ToString("hh:mm tt") : string.Empty,
            FontSize = 10,
            TextColor = isUser ? Colors.White : Color.FromArgb("#A0A0A0"),
            HorizontalOptions = isUser ? LayoutOptions.End : LayoutOptions.Start,
            Margin = new Thickness(0, 4, 0, 0)
        };

        // 3. Inner Container (Holds Text + Timestamp)
        var contentStack = new VerticalStackLayout
        {
            Children = { markdownView, timeLabel }
        };

        // 4. Outer Chat Bubble (Border Control)
        var bubbleBorder = new Border
        {
            Content = contentStack,
            Padding = new Thickness(14, 10),
            StrokeThickness = 0,

            // Align User bubbles to the right, AI bubbles to the left
            HorizontalOptions = isUser ? LayoutOptions.End : LayoutOptions.Start,

            // Add margin to prevent bubbles from stretching full-width
            Margin = isUser
                ? new Thickness(60, 4, 10, 4)  // Push User bubble right
                : new Thickness(10, 4, 60, 4), // Push AI bubble left

            // Distinct bubble colors
            BackgroundColor = isUser
                ? Color.FromArgb("#007ACC")   // Accent blue for User
                : Color.FromArgb("#2D2D30"),  // Dark gray for AI

            // Asymmetrical corner radii for a modern messaging look
            StrokeShape = new RoundRectangle
            {
                CornerRadius = isUser
                    ? new CornerRadius(18, 18, 18, 4)  // Pointed bottom-right
                    : new CornerRadius(18, 18, 4, 18)  // Pointed bottom-left
            }
        };

        return bubbleBorder;
    }
}