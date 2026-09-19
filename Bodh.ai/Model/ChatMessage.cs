using System.ComponentModel;

public class ChatMessage
{
    [DisplayName("Id")]
    public int Id { get; set; }
    [DisplayName("Role")]
    public string Role { get; set; }
    [DisplayName("Message ID")]
    public string MessageId { get; set; }
    [DisplayName("Content")]
    public string Content { get; set; }
    [DisplayName("Is User")]
    public bool IsUser { get; set; }
    [DisplayName("Timestamp")]
    public DateTime Timestamp { get; set; }

}