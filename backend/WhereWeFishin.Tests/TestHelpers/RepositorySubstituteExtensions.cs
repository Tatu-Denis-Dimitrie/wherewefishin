using System.Linq.Expressions;
using NSubstitute;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.Tests.TestHelpers;

internal static class RepositorySubstituteExtensions
{
    public static List<T> UseInMemoryStore<T>(this IRepository<T> repository, IEnumerable<T>? seed = null)
        where T : BaseEntity
    {
        var entities = seed?.ToList() ?? [];

        repository.FindAsync(Arg.Any<Expression<Func<T, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var predicate = callInfo.Arg<Expression<Func<T, bool>>>().Compile();
                IEnumerable<T> results = entities.Where(predicate).ToList();
                return Task.FromResult(results);
            });

        repository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var id = callInfo.Arg<int>();
                var entity = entities.FirstOrDefault(current => current.Id == id);
                return Task.FromResult(entity);
            });

        repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IEnumerable<T>>(entities.ToList()));

        repository.ExistsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var id = callInfo.Arg<int>();
                return Task.FromResult(entities.Any(current => current.Id == id));
            });

        repository.AddAsync(Arg.Any<T>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var entity = callInfo.Arg<T>();
                if (entity.Id == 0)
                {
                    entity.Id = entities.Count == 0 ? 1 : entities.Max(current => current.Id) + 1;
                }

                entities.Add(entity);
                return Task.FromResult(entity);
            });

        repository.UpdateAsync(Arg.Any<T>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        repository.DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var id = callInfo.Arg<int>();
                var entity = entities.FirstOrDefault(current => current.Id == id);
                if (entity != null)
                {
                    entity.IsDeleted = true;
                }

                return Task.CompletedTask;
            });

        return entities;
    }
}