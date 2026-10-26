using System.Security.Cryptography;
using System.Text;

namespace Dataisland.Elasticsearch;

/// <summary>
/// Elasticsearch:StableSearchPreference (dialog 38). A search request names a shard COPY only through
/// <c>preference</c>; without it every request picks a copy anew (adaptive replica selection). A primary and
/// its replica hold the same live documents but merge their segments independently, so each counts a different
/// number of deleted (superseded) chunks — and BM25 reads those counts: N (documents), n (documents with the
/// term) and the average field length. The same query therefore scores ≈1% differently depending on which copy
/// answered. Measured on the production library: for one query list a file's score takes exactly one or two
/// values, never three (652 groups, F51 logs), and all files of one index move together.
/// <para>
/// The preference built here is a pure function of the request, so the same request goes to the same copies and
/// gets the same scores, while different requests still spread over the copies. Values starting with "_" are
/// reserved by Elasticsearch; this one never does. It changes only when the cluster's set of copies changes
/// (a node restart or a relocation), which moves a request to another copy once, not on every call.
/// </para>
/// </summary>
public static class StableSearchPreference
{
    private const char Separator = '\u001f';

    /// <summary>
    /// The preference for one search. <paramref name="kind"/> keeps different search shapes apart; the indices
    /// are taken in ordinal order, so the same set listed differently gets the same copies.
    /// </summary>
    public static string For(string kind, IEnumerable<string> indices, IEnumerable<string?> parts)
    {
        var text = new StringBuilder(kind);
        foreach (var index in indices.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct(StringComparer.Ordinal)
                     .OrderBy(i => i, StringComparer.Ordinal))
            text.Append(Separator).Append('i').Append(index);
        foreach (var part in parts)
            text.Append(Separator).Append('p').Append(part ?? string.Empty);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return "di-" + Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    /// <summary>The bytes of a query vector, so that only a bit-identical vector maps to the same copies.</summary>
    public static string VectorPart(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    /// <summary>File types in a fixed form: distinct and ascending; empty when there is no filter.</summary>
    public static string FileTypesPart(int[]? fileTypes) =>
        fileTypes is { Length: > 0 } ? string.Join(",", fileTypes.Distinct().Order()) : string.Empty;
}
