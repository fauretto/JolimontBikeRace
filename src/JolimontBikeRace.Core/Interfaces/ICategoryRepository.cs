using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Defines the persistence operations available for <see cref="Category"/> entities.
/// </summary>
public interface ICategoryRepository
{
    /// <summary>
    /// Retrieves every category stored in the database.
    /// </summary>
    /// <returns>A read-only list containing every known category.</returns>
    Task<IReadOnlyList<Category>> GetAllAsync();

    /// <summary>
    /// Inserts a new category into the database.
    /// </summary>
    /// <param name="category">The category to insert.</param>
    /// <returns>The unique identifier assigned to the newly inserted category.</returns>
    Task<long> AddAsync(Category category);

    /// <summary>
    /// Updates the details of an already existing category.
    /// </summary>
    /// <param name="category">The category holding the updated values, identified by its Identifier property.</param>
    Task UpdateAsync(Category category);

    /// <summary>
    /// Deletes a category from the database.
    /// </summary>
    /// <param name="identifier">The unique identifier of the category to delete.</param>
    Task DeleteAsync(long identifier);
}
