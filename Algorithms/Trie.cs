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
    // Cada municipio se inserta una vez por palabra de su nombre, así "ostun"
    // encuentra "San Juan Ostuncalco" y "costa" encuentra "Flores Costa Cuca".
    public void Insert(HubNode hub)
    {
        string name = Normalize(hub.Name);
        for (int start = 0; start < name.Length; start++)
        {
            if (name[start] == ' ' || (start > 0 && name[start - 1] != ' ')) continue; // solo inicios de palabra
            var node = _root;
            for (int i = start; i < name.Length; i++)
            {
                if (!node.Children.TryGetValue(name[i], out var next)) node.Children.Add(name[i], next = new Nodo());
                node = next;
            }
            if (!Contains(node.Matches, hub)) node.Matches.Add(hub);
        }
    }

    private static bool Contains(ListaDinamica<HubNode> hubs, HubNode hub)
    {
        for (int i = 0; i < hubs.Count; i++)
            if (hubs[i].Id == hub.Id) return true;
        return false;
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
            for (int i = 0; i < current.Matches.Count; i++)
            {
                if (result.Count == limit) return;
                if (!Contains(result, current.Matches[i])) result.Add(current.Matches[i]);
            }
            // Hijos en orden alfabético: ordenamiento por inserción de las letras
            var letters = new char[current.Children.Count];
            int total = 0;
            foreach (var pair in current.Children)
            {
                int j = total++;
                while (j > 0 && letters[j - 1] > pair.Key) { letters[j] = letters[j - 1]; j--; }
                letters[j] = pair.Key;
            }
            for (int i = 0; i < letters.Length; i++)
            {
                if (result.Count == limit) return;
                Collect(current.Children[letters[i]]);
            }
        }
    }
}
