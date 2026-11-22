using Application.Common.Results;
using Application.Students;
using Domain.Entities;

namespace Application.Common.Abstractions;

public interface IStudentRepository
{
    void Add(Student student);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<StudentResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedResult<StudentResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}
