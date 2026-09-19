using System.ComponentModel;

public class EmbeddingModel
{
    [DisplayName("Id")]
    public int Id { get; set; }
    [DisplayName("Content")]
    public string Content { get; set; }
    [DisplayName("Embedding")]
    public float[] Embedding { get; set; }
}