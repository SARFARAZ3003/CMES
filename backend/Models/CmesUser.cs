using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CMES.Models
{
    // Authorization table: dbo.CMES_USERS. Detected Windows WWID yahan active hona chahiye.
    [Table("CMES_USERS")]
    public class CmesUser
    {
        [Key]
        [Column("UserId")]    public int UserId { get; set; }
        [Column("Username")]  public string Username { get; set; } = "";  // WWID (uppercase)
        [Column("FullName")]  public string? FullName { get; set; }
        [Column("Role")]      public string Role { get; set; } = "Viewer";
        [Column("IsActive")]  public bool IsActive { get; set; }
        [Column("CreatedOn")] public DateTime CreatedOn { get; set; }
    }
}
