using System.ComponentModel;
public class ChatHistory
{
    [DisplayName("ID")]
    public int Id { get; set; }
    [DisplayName("Chat ID")]
    public string ChatId { get; set; }
    [DisplayName("Chat Title")]
    public string ChatTitle { get; set; }
    [DisplayName("Created At")]
    public DateTime CreatedAt { get; set; }
}