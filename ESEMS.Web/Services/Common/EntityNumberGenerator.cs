using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;

namespace ESEMS.Web.Services.Common;

public class EntityNumberGenerator : IEntityNumberGenerator
{
    private readonly ApplicationDbContext _context;

    public EntityNumberGenerator(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextNumberAsync(string prefix)
    {
        var year = DateTime.UtcNow.Year;
        var pattern = $"{prefix}-{year}-";

        int nextNumber = 1;

        if (prefix == "INC")
        {
            var last = await _context.Incidents
                .Where(i => i.IncidentNumber.StartsWith(pattern))
                .OrderByDescending(i => i.IncidentNumber)
                .FirstOrDefaultAsync();

            if (last != null)
            {
                var parts = last.IncidentNumber.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int lastNumber))
                    nextNumber = lastNumber + 1;
            }
        }
        else if (prefix == "PRB")
        {
            var last = await _context.Problems
                .Where(p => p.ProblemNumber.StartsWith(pattern))
                .OrderByDescending(p => p.ProblemNumber)
                .FirstOrDefaultAsync();

            if (last != null)
            {
                var parts = last.ProblemNumber.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int lastNumber))
                    nextNumber = lastNumber + 1;
            }
        }

        return $"{prefix}-{year}-{nextNumber:D4}";
    }
}
