namespace InterLan.Domain.Crm;

public sealed record CrmDeliverable
{
    public CrmDeliverable(string id, string text)
    {
        Id = CrmRequirement.NormalizeKey(id, nameof(id));
        Text = CrmRequirement.NormalizeText(text, nameof(text));
    }

    public string Id { get; }
    public string Text { get; }
}
