namespace ESEMS.Web.Services.Common;

public interface IEntityNumberGenerator
{
    Task<string> GenerateNextNumberAsync(string prefix);
}
