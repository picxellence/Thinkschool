using System.Collections.Generic;

namespace QuotesApi.Models;

public class Quote
{
    public int Id { get; private set; }
    public string Author { get; private set; }
    public string Text { get; private set; }
    public bool IsDeleted { get; private set; }

    private Quote() { }

    private Quote(string author, string text)
    {
        Author = author;
        Text = text;
        IsDeleted = false;
    }

    public static Result<Quote> Create(string author, string text)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(author))
        {
            errors["author"] = new[] { "Author is required." };
        }
        else if (author.Length > 200)
        {
            errors["author"] = new[] { "Author must be 1-200 characters." };
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            errors["text"] = new[] { "Text is required." };
        }
        else if (text.Length > 1000)
        {
            errors["text"] = new[] { "Text must be 1-1000 characters." };
        }

        if (errors.Count > 0)
            return Result<Quote>.Failure(errors);

        return Result<Quote>.Success(new Quote(author.Trim(), text.Trim()));
    }

    public void SoftDelete()
    {
        IsDeleted = true;
    }
}
