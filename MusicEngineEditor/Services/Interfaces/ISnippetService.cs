using System.Collections.Generic;
using System.Threading.Tasks;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service interface for managing code snippets
/// </summary>
public interface ISnippetService
{
    /// <summary>
    /// Gets all loaded snippets
    /// </summary>
    IReadOnlyList<CodeSnippet> Snippets { get; }

    /// <summary>
    /// Loads snippets from the JSON file
    /// </summary>
    /// <returns>The loaded snippets</returns>
    Task<IReadOnlyList<CodeSnippet>> LoadSnippetsAsync();

    /// <summary>
    /// Saves all snippets to the JSON file
    /// </summary>
    Task SaveSnippetsAsync();

    /// <summary>
    /// Gets all snippets in a specific category
    /// </summary>
    /// <param name="category">The category to filter by</param>
    /// <returns>Snippets in the category</returns>
    IReadOnlyList<CodeSnippet> GetSnippetsByCategory(string category);

    /// <summary>
    /// Gets a snippet by its shortcut trigger
    /// </summary>
    /// <param name="shortcut">The shortcut text</param>
    /// <returns>The matching snippet or null</returns>
    CodeSnippet? GetSnippetByShortcut(string shortcut);

    /// <summary>
    /// Gets a snippet by its name
    /// </summary>
    /// <param name="name">The snippet name</param>
    /// <returns>The matching snippet or null</returns>
    CodeSnippet? GetSnippetByName(string name);

    /// <summary>
    /// Adds a new snippet
    /// </summary>
    /// <param name="snippet">The snippet to add</param>
    Task AddSnippetAsync(CodeSnippet snippet);

    /// <summary>
    /// Updates an existing snippet
    /// </summary>
    /// <param name="snippet">The snippet with updated values</param>
    Task UpdateSnippetAsync(CodeSnippet snippet);

    /// <summary>
    /// Deletes a snippet by name
    /// </summary>
    /// <param name="name">The name of the snippet to delete</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteSnippetAsync(string name);

    /// <summary>
    /// Searches snippets by name, description, or tags
    /// </summary>
    /// <param name="searchTerm">The search term</param>
    /// <returns>Matching snippets</returns>
    IReadOnlyList<CodeSnippet> SearchSnippets(string searchTerm);

    /// <summary>
    /// Gets all unique categories
    /// </summary>
    /// <returns>List of category names</returns>
    IReadOnlyList<string> GetCategories();

    /// <summary>
    /// Processes snippet code by replacing placeholders
    /// </summary>
    /// <param name="code">The snippet code with placeholders</param>
    /// <returns>Processed code, cursor position, and tab stops</returns>
    (string ProcessedCode, int CursorPosition, List<TabStop> TabStops) ProcessSnippetCode(string code);
}
