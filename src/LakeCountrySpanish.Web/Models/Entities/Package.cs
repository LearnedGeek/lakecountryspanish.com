namespace LakeCountrySpanish.Web.Models.Entities;

public class Package
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ClassCount { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual ICollection<StudentPackage> StudentPackages { get; set; } = new List<StudentPackage>();
}
