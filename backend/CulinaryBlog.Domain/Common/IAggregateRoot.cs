namespace CulinaryBlog.Domain.Common;

/// <summary>
/// Marker interface định danh Aggregate Root trong Domain-Driven Design (DDD).
/// Các thực thể Category và Recipe là Aggregate Roots quản lý vòng đời của các entities con.
/// </summary>
public interface IAggregateRoot
{
}