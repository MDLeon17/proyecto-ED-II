using System.Globalization;
using System.Text;
using UfgEnvios.Models;

namespace UfgEnvios.Algorithms;

public sealed class Trie
{
    private sealed class Nodo
    {
        public readonly TablaDispersion<char, Nodo> Children = new();
        public readonly ListaDinamica<HubNode> Matches = new();
    }
    private readonly Nodo _root = new();
    private static string Normalize(string text)
    {
        var result = new StringBuilder();
        foreach (char c in text.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) result.Append(c);
        return result.ToString();
    }
    public void Insert(HubNode hub)
    {
        var node = _root;
        foreach (char c in Normalize(hub.Name))
        {
            if (!node.Children.TryGetValue(c, out var next)) node.Children.Add(c, next = new Nodo());
            node = next;
        }
        if (!node.Matches.Any(h => h.Id == hub.Id)) node.Matches.Add(hub);
    }
    public ListaDinamica<HubNode> Search(string prefix, int limit = 30)
    {
        var result = new ListaDinamica<HubNode>();
        if (limit < 1) return result;
        var node = _root;
        foreach (char c in Normalize(prefix))
        {
            if (!node.Children.TryGetValue(c, out var next)) return result;
            node = next;
        }
        Collect(node);
        return result;
        void Collect(Nodo current)
        {
            foreach (var hub in current.Matches) { if (result.Count == limit) return; result.Add(hub); }
            foreach (var child in current.Children.OrderBy(pair => pair.Key)) { if (result.Count == limit) return; Collect(child.Value); }
        }
    }
}
