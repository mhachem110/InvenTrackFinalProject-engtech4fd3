using Microsoft.AspNetCore.Identity;

namespace InvenTrack.Models
{
    public class ApplicationUser : IdentityUser
    {
        public int? AssignedStorageLocationId { get; set; }
    }
}