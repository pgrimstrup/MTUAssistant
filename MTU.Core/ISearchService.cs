using MTU.Core.Models;

namespace MTU.Core;

public interface ISearchService
{
    /// <summary>Minimum number of characters before a search is performed.</summary>
    const int MinimumTermLength = 3;

    /// <summary>
    /// Case-insensitive "contains" search across sector, subsector and system names.
    /// Returns an empty list when the term is shorter than <see cref="MinimumTermLength"/>.
    /// </summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(string term, CancellationToken cancellationToken = default);
}
