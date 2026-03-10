using System.ComponentModel.DataAnnotations;

namespace InvenTrack.ViewModels.Admin
{
    public class UserRolesIndexVM
    {
        public string? Query { get; set; }

        [Display(Name = "Page")]
        public int PageIndex { get; set; } = 1;

        [Display(Name = "Page Size")]
        public int PageSize { get; set; } = 10;

        public int TotalCount { get; set; }
        public int TotalPages { get; set; }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public List<UserRoleRowVM> Users { get; set; } = new();
    }
}