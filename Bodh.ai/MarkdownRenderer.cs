using Markdig;
public class MarkdownRenderer
{
    private static readonly MarkdownPipeline _mdPipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .Build();
    public string RenderMarkdown(string markdown)
    {
        string body = Markdig.Markdown.ToHtml(markdown, _mdPipeline);

        return $@"<html><body style=""background:transparent;margin:0;padding:0;font-family:Segoe UI,sans-serif;font-size:14px;line-height:1.5;color:#333"">{body}</body></html>";
    }
}