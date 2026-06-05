using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

/// <summary>
/// Organizasyon hiyerarşisi için merkezi lookup servisi.
/// Sayfalar bu servisi kullanarak AppDbContext'e doğrudan erişmekten kaçınır.
/// </summary>
public class OrgService(AppDbContext db)
{
    public List<Company> GetCompanies() =>
        [.. db.Companies.OrderBy(c => c.Name)];

    public List<Organization> GetOrganizations() =>
        [.. db.Organizations.Include(o => o.Company).OrderBy(o => o.Name)];

    public List<Department> GetDepartments(bool includeManager = false)
    {
        var q = db.Departments.AsQueryable();
        if (includeManager) q = q.Include(d => d.Manager);
        return [.. q.Include(d => d.Organization).ThenInclude(o => o!.Company).OrderBy(d => d.Name)];
    }

    public List<Department> GetDepartmentsForOrg(int orgId) =>
        [.. db.Departments
            .Where(d => d.OrganizationId == orgId)
            .OrderBy(d => d.Name)];

    public List<Department> GetDepartmentsForCompany(int companyId) =>
        [.. db.Departments
            .Include(d => d.Organization)
            .Where(d => d.Organization != null && d.Organization.CompanyId == companyId)
            .OrderBy(d => d.Name)];

    public List<User> GetActiveUsers() =>
        [.. db.Users
            .Include(u => u.UserRoles)
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)];

    public List<User> GetAuditorUsers()
    {
        var auditorIds = db.UserRoles
            .Where(ur => ur.RoleName == "auditor" || ur.RoleName == "audit_manager")
            .Select(ur => ur.UserId)
            .Distinct()
            .ToHashSet();
        return auditorIds.Count > 0
            ? [.. db.Users.Where(u => auditorIds.Contains(u.Id) && u.IsActive).OrderBy(u => u.FullName)]
            : GetActiveUsers();
    }
}
